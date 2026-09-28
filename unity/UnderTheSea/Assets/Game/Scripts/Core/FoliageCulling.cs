using UnityEngine;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **멀리 있는 풀은 그리지 않는다.**
    ///
    /// <b>왜 필요한가.</b> 로비 섬에는 메시가 9,600개쯤 있고 그 대부분이 풀과 작은 바위다.
    /// 화면에 들어오기만 하면 전부 그린다. 실측으로 RTX 4050 에서 GPU 100% 를 쓰고
    /// 120fps 목표에 91fps 밖에 못 냈다.
    ///
    /// <code>
    ///   풀 덤불    882   (294개 배치 x 3장)
    ///   작은 바위  841
    ///   중간 풀  1,962   (세 종류 합계)
    /// </code>
    ///
    /// <b>Frustum Culling 이 있는데 왜.</b> 그건 <b>카메라 뒤</b>를 빼 줄 뿐이다.
    /// 탁 트인 섬에서는 앞쪽 수백 미터가 전부 시야에 들어와 그대로 다 그려진다.
    /// 거리로 한 번 더 걸러야 한다.
    ///
    /// <b>어떻게.</b> <see cref="Camera.layerCullDistances"/> 는 레이어마다 다른 거리를
    /// 줄 수 있다. 풀만 <see cref="FoliageLayerName"/> 레이어에 모아 두고 그 레이어에만
    /// 짧은 거리를 준다. 나무·절벽은 Default 에 남아 있어 멀리서도 보인다.
    ///
    /// ⚠ <b>구형 컬링은 URP 에서 안 된다.</b> 거리를 카메라 평면까지로 재기 때문에,
    ///    고개를 옆으로 돌리면 같은 풀이 들어왔다 나갔다 하며 깜빡인다.
    ///    구(球)로 재면 회전해도 안 변하므로 <see cref="Camera.layerCullSpherical"/> 를
    ///    켜 봤는데, Unity 가 이렇게 알려 주고 무시한다.
    /// <code>
    ///   Your project uses a scriptable render pipeline.
    ///   You can use Camera.layerCullSpherical only with the built-in renderer.
    /// </code>
    ///    값은 저장되지만(<c>get</c> 이 <c>true</c> 를 돌려준다) 실제 컬링에는 안 쓰인다.
    ///    그래서 그 줄은 뺐다. 새 카메라마다 경고 한 줄을 남길 뿐 하는 일이 없었다.
    ///    <b>거리 컬링(<see cref="Camera.layerCullDistances"/>) 자체는 URP 에서도 동작한다.</b>
    ///    깜빡임이 거슬리면 거리를 늘리는 쪽이 지금으로선 유일한 수단이다.
    ///
    /// ⚠ <b>한 번 걸고 끝낼 수 없다.</b> Fusion 이 Lobby 를 네트워크 씬으로 올리고,
    ///    그 뒤 <c>LocalPlayerView</c> 가 스폰된 캐릭터에 카메라를 붙인다. 그 카메라는
    ///    <c>FusionRunner (Client)_[Player:N]</c> 이라는 런타임 씬에 있어서 씬 로드
    ///    시점에는 존재하지 않는다. 그래서 주기적으로 다시 건다.
    ///
    /// 실행 중에 값을 바꾸려면 <see cref="DevTuningPanel"/> 을 쓴다. (개발 빌드 전용)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoliageCulling : MonoBehaviour
    {
        /// <summary>풀·작은 바위를 모아 둔 레이어. TagManager 의 9번.</summary>
        public const string FoliageLayerName = "Foliage";

        /// <summary>
        /// 이 거리(m) 밖의 풀은 그리지 않는다.
        ///
        /// 60m 는 눈으로 보며 정했다. 이보다 짧으면 걸을 때 풀이 눈앞에서 돋아나는 것이
        /// 보이기 시작한다. 60m 에서도 GPU 40% 라 더 줄여 보기를 희생할 이유가 없었다.
        /// </summary>
        private const int DefaultDistance = 60;

        /// <summary>다시 빌드하지 않고 시험할 수 있게 열어 둔다. <c>-foliagedistance 20</c></summary>
        private const string DistanceKey = "-foliagedistance";

        /// <summary>몇 초마다 카메라를 다시 살피는가. 카메라는 씬 전환·스폰 때만 바뀐다.</summary>
        private const float Interval = 1f;

        private const string HostName = "[풀 컬링]";

        private static FoliageCulling instance;

        private int layer = -1;
        private float next;

        /// <summary>지금 쓰는 거리(m). 0 이면 컬링하지 않는다.</summary>
        public static float Distance { get; private set; }

        /// <summary>레이어가 있어서 컬링을 걸 수 있는 상태인가.</summary>
        public static bool Available => instance != null && instance.layer >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 서버는 아무것도 그리지 않는다. 카메라도 이미 꺼져 있다.
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<FoliageCulling>();
        }

        private void Awake()
        {
            layer = LayerMask.NameToLayer(FoliageLayerName);
            Distance = FusionLaunchArguments.GetInt(DistanceKey, DefaultDistance);

            if (layer < 0)
            {
                Debug.LogWarning($"[풀 컬링] \"{FoliageLayerName}\" 레이어가 없습니다. 컬링하지 않습니다.");
                enabled = false;
                return;
            }

            Debug.Log(Distance > 0f
                ? $"[풀 컬링] {Distance}m 밖의 풀을 그리지 않습니다. (바꾸려면 {DistanceKey} <미터>, 끄려면 {DistanceKey} 0)"
                : $"[풀 컬링] 꺼져 있습니다. ({DistanceKey} 0)");
        }

        /// <summary>
        /// 거리를 바꾼다. 0 이면 컬링을 끈다.
        ///
        /// ⚠ **끌 때는 0 을 넣는 것으로 끝나지 않는다.** 이미 걸어 둔 카메라의 값을
        ///    되돌려야 한다. 그래서 여기서 곧바로 모든 카메라에 다시 적용한다.
        /// </summary>
        public static void SetDistance(float metres)
        {
            Distance = Mathf.Max(0f, metres);

            if (instance != null)
            {
                instance.ApplyToAllCameras();
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }

            next = Time.unscaledTime + Interval;
            ApplyToAllCameras();
        }

        private void ApplyToAllCameras()
        {
            if (layer < 0)
            {
                return;
            }

            // 0 은 Unity 에서 "이 레이어는 카메라의 far plane 을 그대로 쓴다" 는 뜻이라,
            // 끄는 것과 같은 효과가 난다.
            float wanted = Distance;

            foreach (Camera camera in Camera.allCameras)
            {
                if (camera == null || camera.orthographic)
                {
                    continue;
                }

                float[] distances = camera.layerCullDistances;
                if (distances == null || distances.Length != 32)
                {
                    distances = new float[32];
                }

                if (Mathf.Approximately(distances[layer], wanted))
                {
                    continue;
                }

                distances[layer] = wanted;

                camera.layerCullDistances = distances;
            }
        }
    }
}

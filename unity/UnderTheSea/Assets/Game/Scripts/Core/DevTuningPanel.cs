#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **성능 설정을 게임 도중에 바꿔 보는 창.** 개발 빌드에서만 뜬다.
    ///
    /// <b>왜 만들었나.</b> 로비 GPU 부하를 잡으면서 "프레임 제한 때문인지 풀 컬링 때문인지"
    /// 를 가리지 못했다. 설정을 바꾸려면 껐다 켜야 했고, 다시 켜면 <b>서 있는 자리와 보는
    /// 방향이 달라져서</b> 숫자를 비교할 수 없었다. 실제로 컬링을 껐는데 GPU 가 더 낮게
    /// 나온 적이 있다. 시점이 달랐기 때문이다.
    ///
    /// 한자리에 서서 키만 눌러 바꾸면 그 문제가 사라진다.
    ///
    /// <code>
    ///   F9    이 창 열기 / 닫기
    ///   F10   풀 컬링 켜기 / 끄기
    ///   F11   컬링 거리 -5m
    ///   F12   컬링 거리 +5m
    ///   F8    프레임 제한 순환  30 → 60 → 90 → 120 → 무제한
    /// </code>
    ///
    /// ⚠ <b>비교할 때는 프레임 제한을 풀어라.</b> 60 으로 묶어 두면 어떤 설정이든 60 이
    ///    나와서 차이가 안 보인다. 무제한으로 두면 프레임 수가 곧 비용이다.
    ///
    /// ⚠ <b>Release 빌드에는 들어가지 않는다.</b> 파일 전체가 <c>DEVELOPMENT_BUILD</c>
    ///    안에 있다. 시연용 Showcase 빌드에서는 이 창이 존재하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevTuningPanel : MonoBehaviour
    {
        private const string HostName = "[성능 조절판]";

        /// <summary>프레임 제한 후보. 0 은 무제한.</summary>
        private static readonly int[] FrameRates = { 30, 60, 90, 120, 0 };

        private bool open = true;
        private int frameIndex;

        /// <summary>컬링을 껐다 켤 때 되돌릴 거리.</summary>
        private float lastDistance = 35f;

        // 프레임 수를 매 프레임 그대로 쓰면 숫자가 너무 튀어 읽을 수가 없다.
        private float smoothedFps;
        private float worstMs;
        private float worstResetAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            host.AddComponent<DevTuningPanel>();
        }

        private void Start()
        {
            // 지금 걸려 있는 제한이 후보 중 어디쯤인지 찾아 둔다.
            int current = Application.targetFrameRate;
            for (int i = 0; i < FrameRates.Length; i++)
            {
                if (FrameRates[i] == current || (current <= 0 && FrameRates[i] == 0))
                {
                    frameIndex = i;
                    break;
                }
            }

            if (FoliageCulling.Distance > 0f)
            {
                lastDistance = FoliageCulling.Distance;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float fps = 1f / dt;
                smoothedFps = smoothedFps <= 0f ? fps : Mathf.Lerp(smoothedFps, fps, 0.05f);

                // 최악 프레임은 3초마다 새로 잰다. 한 번 튄 값이 계속 남으면 쓸모가 없다.
                float ms = dt * 1000f;
                if (Time.unscaledTime > worstResetAt)
                {
                    worstMs = ms;
                    worstResetAt = Time.unscaledTime + 3f;
                }
                else if (ms > worstMs)
                {
                    worstMs = ms;
                }
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                open = !open;
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                ToggleCulling();
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                Nudge(-5f);
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                Nudge(+5f);
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                CycleFrameRate();
            }
        }

        private void ToggleCulling()
        {
            if (FoliageCulling.Distance > 0f)
            {
                lastDistance = FoliageCulling.Distance;
                FoliageCulling.SetDistance(0f);
            }
            else
            {
                FoliageCulling.SetDistance(lastDistance);
            }
        }

        private void Nudge(float delta)
        {
            if (FoliageCulling.Distance <= 0f)
            {
                return;
            }

            FoliageCulling.SetDistance(Mathf.Clamp(FoliageCulling.Distance + delta, 5f, 400f));
            lastDistance = FoliageCulling.Distance;
        }

        private void CycleFrameRate()
        {
            frameIndex = (frameIndex + 1) % FrameRates.Length;
            int wanted = FrameRates[frameIndex];

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = wanted <= 0 ? -1 : wanted;
        }

        private void OnGUI()
        {
            if (!open)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 22f), "F9  성능 조절판");
                return;
            }

            int limit = FrameRates[frameIndex];
            string limitText = limit <= 0 ? "무제한" : limit + " fps";
            string cullText = FoliageCulling.Distance > 0f
                ? FoliageCulling.Distance.ToString("0") + " m"
                : "꺼짐";

            if (!FoliageCulling.Available)
            {
                cullText = "레이어 없음";
            }

            var box = new Rect(10f, 10f, 290f, 168f);
            GUI.Box(box, "성능 조절판");

            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 26f, box.width - 24f, box.height - 36f));

            GUILayout.Label($"프레임      {smoothedFps:0} fps   (최악 {worstMs:0.0} ms)");
            GUILayout.Label($"프레임 제한  {limitText}          [F8]");
            GUILayout.Label($"풀 컬링     {cullText}           [F10]");
            GUILayout.Label($"거리 조절    F11 −5m   F12 +5m");
            GUILayout.Space(6f);
            GUILayout.Label("비교할 땐 F8 로 무제한을 고르세요.");

            GUILayout.EndArea();
        }
    }
}
#endif

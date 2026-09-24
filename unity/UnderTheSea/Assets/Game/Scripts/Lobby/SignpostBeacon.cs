using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **이정표 위에 화살표를 띄워 "여기는 만질 수 있다" 를 알린다.** 연출만 한다.
    ///
    /// <b>왜 필요한가.</b> 이정표는 모래 위에 놓인 회색-갈색 물건이라 배경에 묻힌다.
    /// 미니게임 입구는 <c>ProximityPortal</c> 이 소용돌이를 열어 주지만, 이정표에 그걸
    /// 그대로 쓰면 <b>들어가면 게임이 시작되는 줄</b> 안다. 역할이 달라서 연출도 달라야 한다.
    ///
    /// <code>
    ///   미니게임 입구   소용돌이가 열린다   →  여기로 들어가면 게임이 시작된다
    ///   이정표          화살표가 떠 있다     →  여기서 다른 곳으로 이동한다
    /// </code>
    ///
    /// <b>왜 파티클이 아닌가.</b> 파티클은 서버에서도 시뮬레이션이 돈다. 화면을 그리지
    /// 않는데도 그렇다. 전에 로비 DS 에서 파티클 406개가 <b>코어 1.67개</b>를 먹은 적이 있다.
    /// 여기서 하는 일은 쿼드 하나를 사인파로 올렸다 내리는 것뿐이라 그 문제가 없다.
    ///
    /// <b>비용.</b> 삼각형 2개 · 드로우콜 1개 · Transform 하나. 로비는 지금 삼각형이
    /// 344만 개에 메시가 1만 개다. 측정 잡음(±1ms)의 100분의 1도 안 된다.
    ///
    /// ⚠ <b>발광은 Bloom 이 해 준다.</b> 로비 전역 프로파일에 Bloom 이 이미 켜져 있고
    ///    임계값이 0.9 다. 머티리얼 색을 그보다 밝게 주면 저절로 번진다. 너무 밝게 주면
    ///    주변이 뿌옇게 번져 지저분해지니 눈으로 보며 맞춘다.
    ///
    /// ⚠ <b>서버에서는 돌지 않는다.</b> 서버는 화면을 안 그리므로 이 연출이 의미가 없다.
    /// </summary>
    /// <remarks>
    /// <b><see cref="ExecuteAlways"/> 인 이유.</b> 에디터에서 보이는 자리와 실행 중 자리가
    /// 달라서는 안 된다. 프리팹에 저장된 지역 좌표로 두면 씬 인스턴스의 배율이 곱해지는데
    /// 실행 중 계산은 그걸 안 타서, 실제로 Stop 했을 때와 Play 중일 때 화살표가 다른
    /// 높이에 있었다. <b>같은 코드가 양쪽에서 돌면</b> 그런 일이 없다.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SignpostBeacon : MonoBehaviour
    {
        [Header("무엇을 띄울까")]
        [Tooltip("이정표 위에 띄울 것. 비워 두면 아무것도 하지 않는다.")]
        [SerializeField] private Transform marker;

        [Header("어디에")]
        [Tooltip("이정표 원점(바닥)에서 얼마나 위에 띄울지(m). 실행 중에 바꿔도 바로 반영된다.")]
        [SerializeField] private float height = 0.8f;

        [Header("어떻게 움직일까")]
        [Tooltip("위아래로 흔들리는 폭(m). 0 이면 가만히 있는다.")]
        [SerializeField] private float bobDistance = 0.12f;

        [Tooltip("한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField] private float bobSeconds = 2f;

        [Tooltip("초당 몇 도 도는가. 0 이면 안 돈다.")]
        [SerializeField] private float spinDegrees = 45f;

        [Header("카메라 바라보기")]
        [Tooltip("켜면 늘 카메라를 향한다. 납작한 아이콘이면 켠다. 입체면 끈다.")]
        [SerializeField] private bool faceCamera;

        /// <summary>돌기 시작하는 위상을 이정표마다 다르게 준다. 여럿이 같이 움직이면 어색하다.</summary>
        private float phase;

        /// <summary>
        /// 지금 연출을 돌려야 하는가.
        ///
        /// ⚠ <b>스스로를 <c>enabled = false</c> 로 끄면 안 된다.</b> 이 클래스는
        ///    <see cref="ExecuteAlways"/> 라 에디터에서도 <c>Awake</c> 가 돈다. 설치
        ///    스크립트가 <c>AddComponent</c> 한 직후에는 <see cref="marker"/> 가 아직
        ///    비어 있는데, 그때 스스로를 꺼 버리면 <b>꺼진 상태가 프리팹에 저장</b>된다.
        ///    그 뒤에 marker 를 연결해도 켜지지 않아, 인스펙터에서 값을 바꿔도 아무 일이
        ///    일어나지 않는다. 실제로 그렇게 됐다.
        ///
        ///    그래서 끄지 않고 <b>매번 물어본다.</b> 조건이 갖춰지면 저절로 동작한다.
        /// </summary>
        private bool ShouldRun =>
            marker != null && !FusionLaunchArguments.IsDedicatedServerProcess();

        private void Awake()
        {
            // 위치를 씨앗으로 쓴다. 같은 자리면 늘 같은 위상이라 보기에 안정적이다.
            phase = Mathf.Repeat(transform.position.x + transform.position.z, Mathf.PI * 2f);

            if (ShouldRun)
            {
                Place(0f);
            }
        }

        /// <summary>
        /// 화살표를 이정표 위에 놓는다. 평범한 지역 좌표다.
        ///
        /// <b>한때 이 자리에 상쇄 계산이 있었다.</b> 임포트된 FBX 의 루트에 X축 -90도와
        /// 100배가 붙어 있어서, 지역 좌표로 올리면 화살표가 위가 아니라 옆으로 가고
        /// 크기도 100배가 됐다. 월드 좌표로 바꿔 상쇄했더니 이번에는 에디터와 실행 중
        /// 위치가 어긋났다.
        ///
        /// <b>지금은 원인을 없앴다.</b> 프리팹을 만들 때 회전도 크기도 없는 빈 루트를
        /// 씌우고 모델을 그 자식으로 넣는다(<c>SignpostSetup</c>). 루트가 깨끗하니
        /// 여기서는 아무것도 상쇄할 것이 없다.
        /// </summary>
        private void Place(float bob)
        {
            marker.localPosition = Vector3.up * (height + bob);

            if (!faceCamera)
            {
                marker.localRotation = Quaternion.identity;
            }
        }


        private void LateUpdate()
        {
            if (!ShouldRun)
            {
                return;
            }

            float bob = 0f;
            if (bobDistance > 0f && bobSeconds > 0f)
            {
                float t = Time.time * (Mathf.PI * 2f / bobSeconds) + phase;
                bob = Mathf.Sin(t) * bobDistance;
            }

            Place(bob);

            if (faceCamera)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    // 위아래로는 안 기울인다. 기울이면 아이콘이 누워 보인다.
                    Vector3 to = marker.position - camera.transform.position;
                    to.y = 0f;

                    if (to.sqrMagnitude > 0.0001f)
                    {
                        marker.rotation = Quaternion.LookRotation(to);
                    }
                }
            }
            else if (spinDegrees != 0f)
            {
                // 프리팹 루트가 깨끗해서 지역 Y축이 곧 위쪽이다.
                marker.Rotate(Vector3.up, spinDegrees * Time.deltaTime, Space.Self);
            }
        }
    }
}

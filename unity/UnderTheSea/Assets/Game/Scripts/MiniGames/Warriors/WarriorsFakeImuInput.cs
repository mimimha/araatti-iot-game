using UnityEngine;
using UnityEngine.InputSystem;

namespace Warriors
{
    /// <summary>
    /// 🧪 <b>장치 없이 IoT 경로를 통째로 시험한다.</b> 개발용이다.
    ///
    /// <c>Alt</c> 를 누른 채 <c>J</c> · <c>K</c> · <c>L</c> 을 치면 그 동작에 해당하는
    /// <b>가짜 IMU 값</b>을 만들어 <see cref="WarriorsIoTInput.SubmitImuSample"/> 에 넣는다.
    ///
    /// <code>
    ///   Alt + J   가로베기   각속도 y 를 임계 위로
    ///   Alt + K   세로베기   각속도 x 를 임계 위로
    ///   Alt + L   찌르기     가속도 z 를 임계 위로
    /// </code>
    ///
    /// <b>왜 필요한가.</b> 장치가 붙는 날 문제가 생기면 <b>장치 쪽인지 게임 쪽인지</b> 가리는 데
    /// 시간을 다 쓴다. 이것으로 미리 "게임 쪽은 뚫려 있다" 를 확인해 두면, 그날은 전송 계층만
    /// 보면 된다.
    ///
    /// <b>⚠ 키를 흉내내는 것이 아니다.</b> 그냥 <c>J</c> 를 누르는 것과 지나는 길이 다르다.
    /// <code>
    ///   J        키보드 → WarriorsInputProvider            (장치를 거치지 않는다)
    ///   Alt + J  가짜 IMU → WarriorsIoTInput → 규칙 분류 → AttackRequested → 같은 provider
    /// </code>
    /// 아래쪽이 <b>실제 장치가 탈 길과 같다.</b> 임계값 · 쿨다운 · 최소 세기 · 번호 필터를
    /// 전부 지나므로, 여기서 되면 장치가 같은 값을 보낼 때도 된다.
    ///
    /// ⚠ <b>릴리스 빌드에서는 스스로 꺼진다.</b> 손님 손에서 Alt 조합이 공격이 되면 안 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsFakeImuInput : MonoBehaviour
    {
        [Header("어느 검인 척할 것인가")]
        [Tooltip("보낼 사람 번호. 네트워크 판에서는 내 번호와 같아야 공격이 나간다.")]
        [SerializeField, Range(0, 3)] private int playerId;

        [Tooltip("켜 두면 위 번호 대신 WarriorsIoTInput 이 아는 '이 화면의 주인' 번호로 보낸다. " +
                 "네트워크 판에서는 서버가 번호를 정하므로 이쪽이 안전하다.")]
        [SerializeField] private bool followLocalOwner = true;

        [Header("얼마나 세게 휘두른 척할 것인가")]
        [Tooltip("임계값의 몇 배로 보낼지. 1 보다 커야 분류를 통과한다.")]
        [SerializeField, Range(1f, 3f)] private float overshoot = 1.6f;

        [Header("키")]
        [Tooltip("이 키를 누른 채로 J · K · L 을 쳐야 한다. 그냥 J 는 평소대로 키보드 공격이다.")]
        [SerializeField] private Key modifier = Key.LeftAlt;

        private WarriorsIoTInput device;

        /// <summary>
        /// 임계값과 같은 값이어야 한다. <c>WarriorsIoTInput</c> 의 기본값을 따른다.
        ///
        /// ⚠ 저쪽 인스펙터에서 임계값을 바꾸면 여기도 같이 봐야 한다. 낮추는 쪽으로 바꾸면
        ///    이 값이 여전히 통과하므로 조용히 어긋나지는 않는다.
        /// </summary>
        private const float AngularThreshold = 3.2f;

        private const float ThrustThreshold = 5.5f;

        private void Awake()
        {
            // 릴리스 빌드에서는 존재만 하고 아무것도 하지 않는다.
            // 컴파일에서 빼지 않는 이유는, 프리팹에 남은 참조가 "Missing" 이 되기 때문이다.
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !Application.isFocused) return;
            if (modifier != Key.None && !keyboard[modifier].isPressed) return;

            if (keyboard.jKey.wasPressedThisFrame) Send(WarriorsAttackDirection.HorizontalSlash);
            else if (keyboard.kKey.wasPressedThisFrame) Send(WarriorsAttackDirection.VerticalSlash);
            else if (keyboard.lKey.wasPressedThisFrame) Send(WarriorsAttackDirection.Thrust);
        }

        /// <summary>
        /// 그 동작으로 분류될 만한 IMU 값을 지어내 넣는다.
        ///
        /// <b>분류는 우리가 하지 않는다.</b> 방향을 직접 <c>OnSwing</c> 에 넣으면 규칙 분류기를
        /// 건너뛰어, 정작 확인하고 싶은 부분(임계값 · 축 선택)이 시험되지 않는다.
        /// 값만 만들어 넣고 <b>무엇으로 읽히는지는 저쪽이 정하게</b> 둔다.
        /// </summary>
        private void Send(WarriorsAttackDirection intended)
        {
            if (device == null)
            {
                device = FindFirstObjectByType<WarriorsIoTInput>(FindObjectsInactive.Include);

                if (device == null)
                {
                    Debug.LogWarning(
                        "[가짜 IMU] WarriorsIoTInput 을 찾지 못했습니다. " +
                        "WarriorsGameRoot 가 이 씬에 있는지 확인해 주세요.", this);
                    return;
                }
            }

            Vector3 angular = Vector3.zero;
            Vector3 linear = Vector3.zero;

            switch (intended)
            {
                case WarriorsAttackDirection.HorizontalSlash:
                    angular.y = AngularThreshold * overshoot;
                    break;

                case WarriorsAttackDirection.VerticalSlash:
                    angular.x = AngularThreshold * overshoot;
                    break;

                default:
                    linear.z = ThrustThreshold * overshoot;
                    break;
            }

            int who = followLocalOwner ? device.LocalPlayerId : playerId;

            // 시각은 사람마다 단조 증가해야 한다. 쿨다운이 이 값으로 계산된다.
            bool accepted = device.SubmitImuSample(who, linear, angular, Time.unscaledTimeAsDouble);

            Debug.Log(
                $"[가짜 IMU] {who + 1}P · {intended} 로 읽히도록 보냈습니다 — " +
                $"{(accepted ? "받아들여짐" : "거절됨 (쿨다운 · 최소 세기 확인)")}", this);
        }
    }
}

using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 서버가 봇 한 명의 가상 장치 입력을 만드는 통로.
    ///
    /// 아직 스스로 판단하지 않는다. 다음 단계의 봇 두뇌가 이 컴포넌트에 이동과 행동을
    /// 명령하면, 기존 <see cref="ShipCoopNetworkedController"/>가 사람 입력과 똑같이
    /// <see cref="IPlayerController"/> 형태로 작업 시스템에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipCoopNetworkedController))]
    public sealed class ShipCoopBotController : MonoBehaviour
    {
        private ShipCoopInputData input;

        /// <summary>현재 입력을 한 틱분 꺼낸다. 순간 동작은 꺼낸 직후 지운다.</summary>
        public ShipCoopInputData ConsumeInput()
        {
            ShipCoopInputData current = input;
            input.Buttons.Set((int)ShipCoopButton.RightSwing, false);
            return current;
        }

        private void Awake()
        {
            // 봇은 항상 양손 장치가 있는 것으로 취급한다. 그래야 기존 양손 작업 규칙을 그대로 탄다.
            input.Buttons.Set((int)ShipCoopButton.TwoDevices, true);
        }

        private void Start()
        {
            ShipCoopNetworkedController controller = GetComponent<ShipCoopNetworkedController>();

            if (controller == null || controller.Object == null || !controller.HasStateAuthority)
            {
                enabled = false;
                return;
            }

            controller.BindBot(this);
        }

        /// <summary>봇의 이동 스틱과 이동 기준 각도를 정한다.</summary>
        public void SetMove(Vector2 move, float lookYaw, bool sprint)
        {
            input.Move = Vector2.ClampMagnitude(move, 1f);
            input.LookYaw = lookYaw;
            input.Buttons.Set((int)ShipCoopButton.LeftButton2, sprint);
        }

        /// <summary>오른손 스틱을 정한다. 이후 대포 조준에 사용한다.</summary>
        public void SetLook(Vector2 look)
        {
            input.Look = Vector2.ClampMagnitude(look, 1f);
        }

        /// <summary>손의 기울기와 회전을 정한다. 조타와 돛 조작이 이 값을 사용한다.</summary>
        public void SetHands(float leftTilt, float rightTilt, float leftRotation, float rightRotation)
        {
            input.LeftTilt = Mathf.Clamp(leftTilt, -1f, 1f);
            input.RightTilt = Mathf.Clamp(rightTilt, -1f, 1f);
            input.LeftRotation = Mathf.Clamp(leftRotation, -1f, 1f);
            input.RightRotation = Mathf.Clamp(rightRotation, -1f, 1f);
        }

        /// <summary>버튼을 누르거나 뗀다. 누른 순간 판정은 기존 컨트롤러가 담당한다.</summary>
        public void SetButton(ShipCoopButton button, bool held)
        {
            input.Buttons.Set((int)button, held);
        }

        /// <summary>다음 틱 한 번만 망치 내리치기를 보낸다.</summary>
        public void Swing()
        {
            input.Buttons.Set((int)ShipCoopButton.RightSwing, true);
        }

        public void Stop()
        {
            input = default;
            input.Buttons.Set((int)ShipCoopButton.TwoDevices, true);
        }
    }
}

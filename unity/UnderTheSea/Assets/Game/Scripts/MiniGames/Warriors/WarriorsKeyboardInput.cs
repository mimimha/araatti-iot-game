using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warriors
{
    /// <summary>
    /// IoT 장치 없이 전투를 검증하기 위한 로컬 입력 구현이다.
    /// IoT 입력 구현도 IWarriorsInputSource를 통해 같은 공격 요청을 전달한다.
    /// </summary>
    public sealed class WarriorsKeyboardInput : MonoBehaviour, IWarriorsInputSource
    {
        public event Action<WarriorsAttackDirection, float> AttackRequested;
        public event Action DodgeRequested;

        public WarriorsAttackDirection? LastRequestedAttack { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                RequestAttack(WarriorsAttackDirection.HorizontalSlash);
            }

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                RequestAttack(WarriorsAttackDirection.VerticalSlash);
            }

            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                RequestAttack(WarriorsAttackDirection.Thrust);
            }
        }

        public void RequestAttack(WarriorsAttackDirection direction)
        {
            LastRequestedAttack = direction;
            AttackRequested?.Invoke(direction, 1f);
        }
    }
}

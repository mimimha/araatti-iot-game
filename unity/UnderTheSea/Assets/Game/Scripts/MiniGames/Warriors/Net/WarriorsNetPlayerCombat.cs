using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 공격 입력을 서버로 옮긴다. **벤 결과는 서버만 정한다.**
    ///
    /// <code>
    ///   서버    받은 버튼으로 WarriorsPlayerCombat.RequestAttack 을 부른다
    ///           칼 히트박스가 몬스터에 닿으면 거기서 처치가 확정된다
    ///   모두    휘두르는 동작은 각자 재생한다 (연출)
    /// </code>
    ///
    /// <b>왜 클라이언트도 휘두르게 두는가.</b> 칼을 휘두르는 모습이 왕복 시간만큼 늦으면
    /// 손맛이 죽는다. 그래서 동작은 각자 바로 내고, <b>맞았는지는 서버만</b> 정한다.
    /// 그 차단은 <c>WarriorsTarget.TryReceiveAttack</c> 한 곳에서 이뤄진다 —
    /// 클라이언트가 아무리 휘둘러도 그 함수가 거짓을 돌려주므로 아무 일도 일어나지 않는다.
    ///
    /// ⚠ 쓰러진 사람은 휘두르지 못한다. 부활이 없으므로 그대로 구경만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WarriorsPlayerCombat))]
    public sealed class WarriorsNetPlayerCombat : NetworkBehaviour
    {
        /// <summary>직전 틱에 눌려 있던 것. 여기서 "새로 눌린 순간" 을 만들어 낸다.</summary>
        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        private WarriorsPlayerCombat combat;
        private WarriorsPlayerLife life;

        public override void Spawned()
        {
            combat = GetComponent<WarriorsPlayerCombat>();
            life = GetComponent<WarriorsPlayerLife>();
        }

        public override void FixedUpdateNetwork()
        {
            // 서버와 내 캐릭터만 입력을 받는다. 남의 캐릭터 복사본은 여기서 걸러진다.
            if (combat == null || !GetInput(out WarriorsInputData input)) return;

            // 쓰러진 사람의 입력은 버린다.
            if (life != null && life.IsDown) return;

            // ⚠ 되돌려 다시 계산하는 틱에서는 "눌린 순간" 을 만들지 않는다.
            //    Fusion 은 같은 틱을 여러 번 굴린다. 그대로 두면 한 번 누른 것이
            //    여러 번 눌린 것으로 처리되어 칼이 두 번 나간다.
            if (!Runner.IsForward) return;

            NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.Buttons;

            if (!TryReadSwing(pressed, out WarriorsAttackDirection direction)) return;

            combat.RequestAttack(direction);

            // 3페이즈에서는 이 스윙이 **노트 판정**이기도 하다.
            // 자기 레인의 노트만 본다. 다른 때는 아무 일도 하지 않는다.
            WarriorsPhase3Director rhythm = WarriorsPhase3Director.Current;
            if (rhythm != null && life != null) rhythm.ReportSwing(life.PlayerIndex, direction);
        }

        /// <summary>눌린 버튼 하나를 공격 방향으로 옮긴다. 한 틱에 하나만 나간다.</summary>
        private static bool TryReadSwing(NetworkButtons pressed, out WarriorsAttackDirection direction)
        {
            if (pressed.IsSet((int)WarriorsButton.HorizontalSlash))
            {
                direction = WarriorsAttackDirection.HorizontalSlash;
                return true;
            }

            if (pressed.IsSet((int)WarriorsButton.VerticalSlash))
            {
                direction = WarriorsAttackDirection.VerticalSlash;
                return true;
            }

            if (pressed.IsSet((int)WarriorsButton.Thrust))
            {
                direction = WarriorsAttackDirection.Thrust;
                return true;
            }

            direction = WarriorsAttackDirection.None;
            return false;
        }
    }
}

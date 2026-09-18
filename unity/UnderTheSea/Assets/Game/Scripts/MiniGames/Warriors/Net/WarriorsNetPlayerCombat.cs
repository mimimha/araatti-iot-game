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

        /// <summary>
        /// **서버가 인정한 공격의 번호.** 하나 오를 때마다 새 공격이다.
        ///
        /// 번호를 두는 이유는 같은 방향으로 연달아 벨 때다. 종류만 복제하면
        /// 값이 그대로라 두 번째 스윙을 알아챌 수 없다.
        /// </summary>
        [Networked] public int AttackSeq { get; private set; }

        /// <summary>그 공격의 종류. <see cref="WarriorsAttackDirection"/> 의 숫자값.</summary>
        [Networked] public int AttackKind { get; private set; }

        private WarriorsPlayerCombat combat;
        private WarriorsPlayerLife life;
        private int shownSeq;

        /// <summary>마지막으로 처리한 스윙 번호. 같은 번호는 다시 휘두르지 않는다.</summary>
        private byte lastSwingSerial;

        public override void Spawned()
        {
            combat = GetComponent<WarriorsPlayerCombat>();
            life = GetComponent<WarriorsPlayerLife>();

            // 늦게 들어온 사람이 지나간 공격을 한 번 재생하지 않게 지금 값에서 시작한다.
            shownSeq = AttackSeq;

            // **서버에서만** 결과를 듣는다. 협동 게이지와 측정은 서버가 정한다.
            if (HasStateAuthority && combat != null) combat.AttackResolved += HandleAttackResolved;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (combat != null) combat.AttackResolved -= HandleAttackResolved;
        }

        /// <summary>
        /// 한 번 휘두른 결과가 나왔다. <paramref name="accepted"/> 는 실제로 맞은 대상 수다.
        ///
        /// 지금은 측정 로그에 남기는 것이 전부다 — 협동 게이지가 규칙에서 빠졌다.
        /// </summary>
        private void HandleAttackResolved(WarriorsAttackDirection direction, int accepted)
        {
            if (!HasStateAuthority || life == null) return;

            WarriorsTelemetry.Swing(life.PlayerIndex, accepted > 0 ? 0 : 2, false);

            // ⚠ 예전에는 여기서 "쓰러진 동료 곁에서 휘두르면 구조" 를 처리했다.
            //    구조·부활은 규칙에서 빠졌다. 쓰러진 사람은 그 판에서 끝이다.

        }

        public override void FixedUpdateNetwork()
        {
            // 서버와 내 캐릭터만 입력을 받는다. 남의 캐릭터 복사본은 여기서 걸러진다.
            if (combat == null || !GetInput(out WarriorsInputData input)) return;

            // ⚠ **휘두를지 말지는 서버만 정한다.**
            //    예전에는 내 화면에서 미리 휘두르고 끝냈다. 그래서 그 스윙이
            //    이 컴퓨터 밖으로 나가지 않았고, 남의 화면에서는 아무도 칼을 들지 않았다.
            //    이제는 서버가 인정한 것만 AttackSeq 로 복제되고, 그리는 일은 Render 가 한다.
            if (!HasStateAuthority) return;

            // 쓰러진 사람의 입력은 버린다.
            if (life != null && life.IsDown) return;

            // 일시정지 중에는 휘두르지 않는다. 다만 "누르고 있던 것" 은 기억해 둔다 —
            // 그러지 않으면 멈춘 동안 누른 키가 재개 순간에 한 번 나간다.
            if (WarriorsMatchState.PausedNow)
            {
                PreviousButtons = input.Buttons;
                return;
            }

            // ⚠ 되돌려 다시 계산하는 틱에서는 "눌린 순간" 을 만들지 않는다.
            //    Fusion 은 같은 틱을 여러 번 굴린다. 그대로 두면 한 번 누른 것이
            //    여러 번 눌린 것으로 처리되어 칼이 두 번 나간다.
            if (!Runner.IsForward) return;

            NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.Buttons;

            // **스윙 번호가 먼저다.** IoT 검은 버튼을 누르고 있는 것이 아니라 사건 하나를 보낸다.
            // 번호가 바뀌었으면 그것이 새 스윙이고, 세기와 친 시각이 같이 실려 온다.
            // 번호가 그대로면 예전처럼 버튼이 눌린 순간을 본다(키보드 호환).
            WarriorsAttackDirection direction;
            float strength = 1f;

            if (input.SwingSerial != 0 && input.SwingSerial != lastSwingSerial)
            {
                lastSwingSerial = input.SwingSerial;
                direction = (WarriorsAttackDirection)input.SwingType;
                strength = Mathf.Clamp01(input.SwingStrength / 255f);
            }
            else if (!TryReadSwing(pressed, out direction))
            {
                return;
            }

            // 인정했다. 모든 화면이 이 번호를 보고 같은 스윙을 낸다.
            AttackSeq++;
            AttackKind = (int)direction;

            // 세기를 그대로 넘긴다. 키보드는 늘 1이다.
            combat.RequestAttack(direction, strength);

            // 3페이즈에서는 이 스윙이 **노트 판정**이기도 하다.
            // 자기 레인의 노트만 본다. 다른 때는 아무 일도 하지 않는다.
            WarriorsPhase3Director rhythm = WarriorsPhase3Director.Current;
            if (rhythm != null && life != null) rhythm.ReportSwing(life.PlayerIndex, direction);
        }

        /// <summary>
        /// **서버가 인정한 공격을 모든 화면에서 재생한다.** 내 캐릭터도, 상대 캐릭터도.
        ///
        /// 로컬 입력이나 로컬 Animator 상태는 보지 않는다. 오직 복제된 번호만 본다.
        /// 그래서 서버가 인정하지 않은 입력은 어느 화면에도 스윙으로 보이지 않는다.
        ///
        /// ⚠ 서버에서는 돌지 않는다. 위에서 이미 휘둘렀으므로 여기서 또 부르면
        ///    <c>ApplyAreaAttack</c> 이 한 번 더 돌아 피해가 두 번 들어간다.
        /// </summary>
        public override void Render()
        {
            if (HasStateAuthority || combat == null) return;
            if (AttackSeq == shownSeq) return;

            shownSeq = AttackSeq;
            combat.RequestAttack((WarriorsAttackDirection)AttackKind);
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

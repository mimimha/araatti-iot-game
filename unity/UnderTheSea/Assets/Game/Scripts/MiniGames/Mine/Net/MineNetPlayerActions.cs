using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 채굴 · 되메우기 · 힌트를 서버로 옮긴다. **결과는 서버만 정한다.**
    ///
    /// <code>
    ///   클라이언트  F · C · V 를 눌렀다는 사실만 보낸다
    ///   서버        지금 턴인 사람인지 보고, 맞으면 격자를 고친다
    ///   모두        고쳐진 격자를 MineGridSync 로 받아 그린다
    /// </code>
    ///
    /// <b>왜 <c>MineDigger</c> 를 쓰지 않는가.</b> 그쪽은 <c>IPlayerController</c> 에서
    /// 직접 키를 읽고, <c>Consume</c> 계열은 한 번 읽으면 스스로 지운다. 주석이
    /// <b>"읽는 곳은 이 컴포넌트 하나여야 한다"</b> 고 못박아 두었다. 네트워크에서는
    /// 키를 읽는 곳이 클라이언트이고 판단하는 곳이 서버라 그 구조와 맞지 않는다.
    ///
    /// 대신 <b>규칙은 그대로 쓴다</b> — 돌이 몇 번에 깨지는지, 금이 어떻게 남는지는
    /// 전부 <c>MineGrid.Hit</c> 안에 있고, 이 부품은 그 함수를 부를 뿐이다.
    ///
    /// ⚠ 되돌려 다시 계산하는 틱에서는 "눌린 순간" 을 만들지 않는다. Fusion 은 같은 틱을
    ///    여러 번 굴린다. 그대로 두면 한 번 누른 것이 여러 번으로 처리되어 두 칸이 파인다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MineNetPlayer))]
    public sealed class MineNetPlayerActions : NetworkBehaviour
    {
        /// <summary>직전 틱에 눌려 있던 것. 여기서 "새로 눌린 순간" 을 만들어 낸다.</summary>
        [Networked]
        private NetworkButtons PreviousButtons { get; set; }

        private MineNetPlayer _who;
        private MineJump _hop;

        private MineNetPlayerMover _body;

        public override void Spawned()
        {
            _who = GetComponent<MineNetPlayer>();
            _hop = GetComponent<MineJump>();
            _body = GetComponent<MineNetPlayerMover>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _who == null) return;

            // 조준 칸은 입력과 상관없이 매 틱 갱신한다. **채굴이 쓰는 것과 같은 계산**이라
            // 표시된 칸과 실제로 파이는 칸이 어긋나지 않는다.
            //
            // ⚠ **지금 턴인 사람만 기록한다.** 이제 넷이 다 걸어다니므로 모두가 적으면
            //   화면에 표시가 넷이 되고, 팔 수 없는 사람의 발밑까지 "여기를 판다" 로
            //   읽힌다. 턴이 아닌 사람은 -1(표시 없음)로 덮어써 둔다 — 그래야 턴이
            //   넘어가는 순간 앞사람의 표시가 스스로 사라진다.
            //
            //   서버가 정하므로 네 화면이 같은 칸 하나를 본다. 카운트다운에는
            //   CurrentSlot 이 -1 이라 아무도 해당되지 않아 표시가 아예 없다.
            _who.RecordFocus(_who.IsMyTurn ? ResolveFocusCell() : -1);

            if (!GetInput(out MineInputData input)) return;

            // ⚠ 되감기 틱에서는 눌린 순간을 만들지 않는다.
            if (!Runner.IsForward) return;

            NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.Buttons;

            MineMatchState match = MineMatchState.Current;
            if (match == null) return;

            // **지금 턴인 사람만 판을 건드린다.** 관전자의 F · C · V 는 여기서 버려진다.
            if (!_who.IsMyTurn) return;

            // 목표를 보는 동안에는 판이 멈춘다. 발밑이 안 보이는데 파이면
            // 어디를 팠는지도 모르고 파인다. (MINE.md 6장 — 힌트는 탑뷰와 함께 온다)
            if (match.ShowingTarget) return;

            if (pressed.IsSet((int)MineButton.Swing)) Dig();
            if (pressed.IsSet((int)MineButton.Restore)) Restore(match);
            if (pressed.IsSet((int)MineButton.Hint)) Hint(match);
        }

        /// <summary>발밑이 몇 번 칸인가. 판 밖이면 -1.</summary>
        private int ResolveFocusCell()
        {
            MineGridSync board = MineGridSync.Current;
            MineGrid grid = board != null ? board.Grid : null;

            if (grid == null) return -1;

            return grid.WorldToCell(transform.position, out int x, out int y)
                ? y * grid.Size + x
                : -1;
        }

        /// <summary>발밑을 한 번 친다. 무른 돌은 깨지고 단단한 돌은 처음에 금만 간다.</summary>
        private void Dig()
        {
            MineGridSync board = MineGridSync.Current;
            if (board == null) return;

            MineHitResult result = board.ServerDig(transform.position);

            // 발밑이 꺼졌으니 폴짝 뛴다. 손맛일 뿐 규칙은 아니다.
            // 금만 갔을 때는 안 뛴다 — 아직 발밑이 그대로이기 때문이다.
            if (result != MineHitResult.Broke) return;

            // ⚠ 네트워크에서는 <c>MineJump</c> 만으로는 부족하다.
            //
            //   그쪽은 Update(-50) 에서 CharacterMover 에 jump=true 를 쓰는데,
            //   같은 프레임에 <c>MineNetPlayerMover</c> 가 틱마다 jump=false 를
            //   다시 써서 지워버린다. 점프는 매 프레임 읽히는 bool 하나라
            //   나중에 쓴 쪽이 이긴다. 그래서 그 부품에게 직접 알린다.
            if (_body != null) _body.RequestHop();
            else if (_hop != null) _hop.Hop();
        }

        /// <summary>
        /// 발밑을 되메운다.
        ///
        /// 순서가 중요하다. **먼저 격자를 고쳐 보고, 실제로 되메워졌을 때만** 블록을 깎는다.
        /// 안 파인 칸에서 잘못 누른 것 때문에 귀한 블록이 날아가면 안 된다.
        /// </summary>
        private void Restore(MineMatchState match)
        {
            MineGridSync board = MineGridSync.Current;
            if (board == null) return;

            if (match.RestoresLeft <= 0)
            {
                Debug.Log("[MineActions] 복구 블록이 남아 있지 않습니다.");
                return;
            }

            if (!board.ServerRestore(transform.position))
            {
                Debug.Log("[MineActions] 발밑이 파여 있지 않아 되메울 것이 없습니다.");
                return;
            }

            match.ServerUseRestore();
        }

        /// <summary>목표를 다시 보여 준다. **사람마다 한 번뿐이다.**</summary>
        private void Hint(MineMatchState match)
        {
            if (_who.HintUsed)
            {
                Debug.Log($"[MineActions] P{_who.Slot + 1} 은(는) 힌트를 이미 썼습니다.");
                return;
            }

            _who.MarkHintUsed();
            match.ServerShowHint(_who.Slot);
        }
    }
}

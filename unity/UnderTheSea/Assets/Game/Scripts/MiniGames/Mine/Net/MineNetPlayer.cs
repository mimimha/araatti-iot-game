using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 이 사람의 **자리와 시점**. 서버가 정하고 모두가 본다.
    ///
    /// <code>
    ///   Slot        P1~P4 중 몇 번인가. -1 이면 이번 판의 참가자가 아니다(관전 전용)
    ///   JoinTick    접속 순서를 가리는 값. 자리를 다시 나눌 때 이 순서를 쓴다
    ///   HintUsed    힌트를 썼는가. **사람마다 하나다** (MINE.md 2·4장)
    ///   CameraYaw   이 사람이 보고 있는 좌우 각도. 관전자가 이 값으로 같은 곳을 본다
    ///   CameraPitch 상하 각도
    /// </code>
    ///
    /// <b>왜 <c>MineDigger</c> 를 고치지 않고 따로 두는가.</b>
    /// 그쪽은 "휘두르면 발밑을 판다" 는 손이고, 여기 있는 것은 <b>판의 규칙</b>이다.
    /// 섞으면 혼자 하는 씬에서도 슬롯과 카메라 각도를 들고 다니게 된다.
    /// <c>HintUsed</c> 만 두 곳에 생기는데, 2단계에서 격자를 옮길 때 이쪽을 진짜로 삼고
    /// <c>MineDigger</c> 쪽은 표시용으로 정리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineNetPlayer : NetworkBehaviour
    {
        /// <summary>P1~P4 중 몇 번인가. **-1 이면 이번 판의 참가자가 아니다.**</summary>
        [Networked] public int Slot { get; private set; }

        /// <summary>몇 번째 틱에 들어왔는가. 자리를 다시 나눌 때의 순서가 된다.</summary>
        [Networked] public int JoinTick { get; private set; }

        /// <summary>이번 판에 힌트를 썼는가. **사람마다 하나.** (2단계에서 실제로 쓰인다)</summary>
        [Networked] public NetworkBool HintUsed { get; private set; }

        /// <summary>이 사람이 보고 있는 좌우 각도(도).</summary>
        [Networked] public float CameraYaw { get; private set; }

        /// <summary>이 사람이 보고 있는 상하 각도(도).</summary>
        [Networked] public float CameraPitch { get; private set; }

        /// <summary>
        /// 지금 조준 중인 칸. <c>y * size + x</c> 이고 판 밖이면 -1.
        ///
        /// <b>왜 복제하는가.</b> 표시만이면 각자 자기 화면의 캐릭터 자리로 계산해도 된다.
        /// 그런데 그 자리는 보간된 값이라 칸 경계에서 한 칸씩 어긋나고, 그러면
        /// <b>표시된 칸과 실제로 파이는 칸이 달라진다.</b> 서버가 고른 칸을 그대로
        /// 받아 그리면 관전자까지 같은 칸을 본다.
        /// </summary>
        [Networked] public int FocusCell { get; private set; }

        /// <summary>
        /// 이 사람의 화면이 **판을 볼 수 있게 됐는가.** 서버가 카운트다운을 여는 조건이다.
        ///
        /// <b>왜 필요한가.</b> 서버는 접속한 순간(<c>Runner.ActivePlayers</c>) 인원에 넣는데,
        /// 클라이언트는 그때부터 씬을 불러오고 판을 받는다. 인원만 보고 세면 마지막 사람은
        /// 3 을 못 보고 2.2 쯤에서 들어온다(실측). 그래서 **로딩 화면을 걷는 순간**
        /// (<c>MineLocalView.FinishLoadingWhenPlayable</c>) 자기 몸으로 서버에 알린다.
        ///
        /// 접속할 때 거짓으로 시작한다(<see cref="Spawned"/>). 판을 되돌리는 것은 아무도 없을 때뿐이라
        /// (<c>MineMatchState.ResetToWaiting</c>) 다음 판의 사람은 늘 새 몸으로 들어온다 —
        /// 지난 판의 참이 남을 자리가 없다.
        /// </summary>
        [Networked] public NetworkBool SceneReady { get; private set; }

        /// <summary>이번 판의 참가자인가. 늦게 들어온 사람은 거짓이다.</summary>
        public bool InRoster => Slot >= 0;

        /// <summary>
        /// 지금 이 몸이 <b>움직일 수 있는가.</b>
        ///
        /// 자기 턴이거나, 목표를 보여 주는 동안 곳 첫 턴을 받을 사람이다.
        /// 공개 때 미리 자리를 잡을 수 있게 하려는 것이다.
        ///
        /// <b>카운트다운과 턴에는 참가자 전원이 참이다.</b> (<see cref="MineMatchState.FreeRoam"/>)
        /// 내 턴이 아니어도 걷고 달릴 수 있다. 그동안 넷이 다 보이고 서로 부딪힌다.
        ///
        /// ⚠ <b>파는 것은 여기에 걸리지 않는다.</b> <see cref="MineNetPlayerActions"/> 가
        ///   <see cref="IsMyTurn"/> 과 <c>ShowingTarget</c> 으로 따로 막는다.
        ///   <b>움직이는 것과 파는 것은 다른 문이다</b> — 넷이 같이 걸어다녀도
        ///   파는 것은 언제나 한 사람뿐이다. 카운트다운에는 <c>CurrentSlot</c> 이 -1 이라
        ///   아무도 <see cref="IsMyTurn"/> 이 아니어서 한 명도 못 판다.
        /// </summary>
        public bool CanMoveNow
        {
            get
            {
                if (IsMyTurn) return true;

                MineMatchState match = MineMatchState.Current;
                if (match == null || Slot < 0) return false;

                return match.FreeRoam || match.WarmupSlot == Slot;
            }
        }

        /// <summary>
        /// 지금 이 몸이 <b>격자 위에 보이는가.</b> <see cref="CanMoveNow"/> 와 <b>따로 논다.</b>
        ///
        /// 공개 7초가 그 둘이 갈라지는 자리다 — 넷이 다 서 있되 첫 턴 예정자만 걷는다.
        /// 나머지 셋은 굳은 채로 같이 도안을 본다.
        ///
        /// ⚠ <b>움직임 판정을 여기에 섞으면 안 된다.</b> 힌트처럼 잠깐 멈추는 것까지
        ///   보이기에 엮으면 그때마다 캐릭터가 사라진다. 실제로 겪은 문제다.
        ///   (<see cref="WatchingOwnHint"/> 주석)
        /// </summary>
        public bool ShowBody
        {
            get
            {
                if (Slot < 0) return false;

                MineMatchState match = MineMatchState.Current;
                return match != null && match.CrewOnBoard;
            }
        }

        /// <summary>
        /// <b>내 힌트를 보는 중인가.</b> 그동안에는 몸을 굴리지 않는다.
        ///
        /// 탑뷰로 올라가 발밑이 안 보이는데 그대로 움직이면 어디로 가는지 모른다.
        /// 게다가 정답 보기가 파인 칸을 0.25m 끌어올려서 콜라이더가 캐릭터를 떠민다 —
        /// 솔로에서 실제로 토글마다 점프했다. (<c>MineGame.SyncFrozen</c>)
        ///
        /// ⚠ <b><see cref="CanMoveNow"/> 에 넣으면 안 된다.</b> 그 값은 이동 판정만
        ///   하는 것이 아니라 <c>ApplyPresence</c> 로 <b>렌더러를 켜고 끄는 데도</b>
        ///   쓰인다. 거기에 힌트 조건을 넣었다가 힌트 토글마다 캐릭터가 사라졌다.
        ///   막을 것은 몸을 굴리는 것뿐이므로 <c>MineNetPlayerMover</c> 만 이걸 본다.
        /// </summary>
        public bool WatchingOwnHint
        {
            get
            {
                if (Slot < 0) return false;

                MineMatchState match = MineMatchState.Current;
                return match != null && match.HintLeft > 0f && match.HintSlot == Slot;
            }
        }

        /// <summary>지금 이 사람의 턴인가.</summary>
        public bool IsMyTurn
        {
            get
            {
                MineMatchState match = MineMatchState.Current;
                return match != null && Slot >= 0 && match.CurrentSlot == Slot;
            }
        }

        private Renderer[] _skins;
        private Collider[] _hitboxes;
        private CharacterController _capsule;

        /// <summary>외형을 입히는 부품. 있으면 감추기도 이쪽에 맡긴다.</summary>
        private UnderTheSea.Character.CharacterAppearanceApplier _applier;
        private bool? _shownVisible;

        public override void Spawned()
        {
            _skins = GetComponentsInChildren<Renderer>(true);
            _hitboxes = GetComponentsInChildren<Collider>(true);
            _capsule = GetComponent<CharacterController>();
            _applier = GetComponent<UnderTheSea.Character.CharacterAppearanceApplier>();

            if (!HasStateAuthority) return;

            Slot = -1;
            JoinTick = Runner.Tick;
            FocusCell = -1;
            SceneReady = false;
        }

        /// <summary>
        /// **내 화면이 준비됐다** 고 서버에 알린다. 로딩 화면을 걷는 순간 내 몸에서 한 번 부른다.
        ///
        /// 입력 권한이 있는 몸만 보낼 수 있다(<see cref="RpcSources.InputAuthority"/>).
        /// 남의 복사본에서 불러도 Fusion 이 보내지 않으므로, 남의 준비를 대신 알릴 수 없다.
        /// 두 번 와도 한 번만 적는다.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_ReportReady()
        {
            if (SceneReady) return;

            SceneReady = true;
            Debug.Log($"[MineNetPlayer] {Object.InputAuthority} 화면 준비 완료 — 접속 후 " +
                      $"{SecondsSinceJoin:F2}초", this);
        }

        /// <summary>접속한 지 몇 초 지났는가. 서버의 틱으로 잰다.</summary>
        public float SecondsSinceJoin => Runner != null ? Mathf.Max(0f, (Runner.Tick - JoinTick) * Runner.DeltaTime) : 0f;

        /// <summary>
        /// 준비 알림이 끝내 안 오면 **이만큼 기다린 뒤 준비된 것으로 친다.** 서버만 부른다.
        ///
        /// 알림이 빠지는 일은 없어야 하지만(로딩 화면 쪽에도 상한이 있다), 빠지면 판이 영영
        /// 시작하지 않는다. 멈추는 것보다 3 을 놓치는 편이 낫다.
        /// ⚠ 이 로그가 정상 테스트에서 나오면 알림이 어딘가에서 끊긴 것이다.
        /// </summary>
        public bool ServerReadyOrTimedOut(float timeoutSeconds)
        {
            if (!HasStateAuthority) return SceneReady;
            if (SceneReady) return true;
            if (SecondsSinceJoin < timeoutSeconds) return false;

            SceneReady = true;
            Debug.LogWarning($"[MineNetPlayer] {Object.InputAuthority} 준비 알림이 {timeoutSeconds:F0}초 안에 " +
                             "오지 않아 timeout 으로 준비된 것으로 칩니다.", this);
            return true;
        }

        /// <summary>서버가 자리를 정한다. 스포너와 매치 상태가 부른다.</summary>
        public void AssignSlot(int slot)
        {
            if (!HasStateAuthority) return;
            Slot = slot;
        }

        /// <summary>서버가 힌트 사용을 적는다. (2단계에서 쓰인다)</summary>
        public void MarkHintUsed()
        {
            if (!HasStateAuthority) return;
            HintUsed = true;
        }

        /// <summary>서버가 조준 칸을 기록한다. 채굴 대상과 같은 계산에서 나온 값이다.</summary>
        public void RecordFocus(int cell)
        {
            if (!HasStateAuthority) return;
            FocusCell = cell;
        }

        /// <summary>서버가 이 사람의 시점을 기록한다. 관전자들이 이 값을 받아 본다.</summary>
        public void RecordLook(float yaw, float pitch)
        {
            if (!HasStateAuthority) return;

            CameraYaw = yaw;
            CameraPitch = pitch;
        }

        /// <summary>
        /// **판이 도는 동안에는 참가자 넷이 다 보인다.** 카운트다운 · 공개 · 턴.
        /// (<see cref="ShowBody"/>) 움직일 수 있는가는 여기서 보지 않는다 —
        /// 공개 7초에는 넷이 다 서 있고 걷는 것은 첫 턴 예정자뿐이다.
        ///
        /// 숨기는 때는 둘이다. <b>대기</b>는 아직 스폰 높이에 떠 있어서(카운트다운에
        /// 떨어진다), <b>결과</b>는 완성된 그림을 위에서 보여 주는 시간이라 몸이 가리면
        /// 안 되어서다.
        ///
        /// 콜라이더까지 같이 끄는 이유 — <b>보이지 않는 몸이 길을 막으면 안 된다.</b>
        /// 사람끼리의 충돌도 같은 값을 따라간다.
        /// (<c>MineNetPlayerMover.ApplyCrowdCollision</c>)
        ///
        /// ⚠ 서버에서도 판단은 같다. 규칙(충돌)이 걸려 있어 표시만의 문제가 아니다.
        ///
        /// ⚠ <b><c>CharacterController</c> 는 여기서 건드리지 않는다.</b> 그것은
        ///    <c>MineNetPlayerMover.SetSimulated</c> 가 <c>CharacterMover</c> 와 <b>함께</b>
        ///    켜고 끈다. 컨트롤러만 따로 끄면 Mover 가 계속 <c>Move</c> 를 불러
        ///    "inactive controller" 오류가 프레임마다 쏟아진다.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            ApplyPresence(ShowBody);
        }

        public override void Render()
        {
            ApplyPresence(ShowBody);
        }

        private void ApplyPresence(bool visible)
        {
            if (_shownVisible == visible) return;
            _shownVisible = visible;

            // ⚠ **`enabled` 로 감추면 안 된다.** `PeerMode.Multiple` 에서 Fusion 의
            //    `RunnerVisibilityLink` 가 그 값을 자기 것으로 여기고, 다른 NetworkObject 가
            //    스폰될 때마다 **스폰 순간의 값(= 전부 켜짐)으로 되돌려 놓는다.**
            //    그러면 대기 중인 사람이 도로 나타나는데, 이 함수는 `_shownVisible` 에
            //    "이미 껐다" 고 적어 두었으므로 **다시 끄지 않는다.**
            //    실제로 두 명이 들어가면 대기자가 판 위에 서 있었다.
            //
            //    `forceRenderingOff` 는 Fusion 이 건드리지 않는다. 그래서 외형 부품과 같은
            //    스위치를 쓰되, 이유는 따로 들고 간다(`SetPresenceHidden`).
            if (_applier != null)
            {
                _applier.SetPresenceHidden(!visible);
            }
            else if (_skins != null)
            {
                // 외형 부품이 없는 구성(단순 모델)에서는 예전 방식으로 감춘다.
                foreach (Renderer skin in _skins)
                    if (skin != null) skin.forceRenderingOff = !visible;
            }

            // 캡슐(CharacterController)은 건드리지 않는다. Mover 가 짝지어 관리한다.
            if (_hitboxes != null)
                foreach (Collider hitbox in _hitboxes)
                    if (hitbox != null && hitbox != _capsule) hitbox.enabled = visible;
        }
    }
}

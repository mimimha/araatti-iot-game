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

        /// <summary>이번 판의 참가자인가. 늦게 들어온 사람은 거짓이다.</summary>
        public bool InRoster => Slot >= 0;

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
        private bool? _shownVisible;

        public override void Spawned()
        {
            _skins = GetComponentsInChildren<Renderer>(true);
            _hitboxes = GetComponentsInChildren<Collider>(true);
            _capsule = GetComponent<CharacterController>();

            if (!HasStateAuthority) return;

            Slot = -1;
            JoinTick = Runner.Tick;
            FocusCell = -1;
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
        /// **지금 턴인 사람만 격자 위에 보인다.**
        ///
        /// 네 명이 다 서 있으면 서로의 몸이 도안을 가린다. 채굴 위치를 읽을 수 없게 되고,
        /// 무엇보다 이 게임은 "지금 누가 파고 있는가" 가 화면의 전부다.
        ///
        /// 콜라이더까지 끄는 이유는 가리는 것 말고도 하나 더 있다 — 관전자의 몸이
        /// 남아 있으면 지금 턴인 사람이 거기에 걸려 못 지나간다.
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
            ApplyPresence(IsMyTurn);
        }

        public override void Render()
        {
            ApplyPresence(IsMyTurn);
        }

        private void ApplyPresence(bool visible)
        {
            if (_shownVisible == visible) return;
            _shownVisible = visible;

            if (_skins != null)
                foreach (Renderer skin in _skins)
                    if (skin != null) skin.enabled = visible;

            // 캡슐(CharacterController)은 건드리지 않는다. Mover 가 짝지어 관리한다.
            if (_hitboxes != null)
                foreach (Collider hitbox in _hitboxes)
                    if (hitbox != null && hitbox != _capsule) hitbox.enabled = visible;
        }
    }
}

using System;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 심장 제단의 상호작용 지점. **가까이 가면 안내가 뜨고, 누르면 봉헌 창이 열린다.**
    ///
    /// <code>
    ///   가까이 가면   "[E] 조각 봉헌" 안내가 뜬다
    ///   누르면        봉헌 창이 열린다
    ///   멀어지면      안내가 사라지고 눌러도 아무 일 없다
    /// </code>
    ///
    /// <b>이 부품은 화면을 만들지 않는다.</b> "가까워졌다 / 눌렀다" 는 사실만 알리고,
    /// 실제로 무엇을 띄울지는 <see cref="AltarOfferingInstaller"/> 가 정한다.
    /// 그래서 이 파일에는 Canvas · Text · 프리팹 이름이 하나도 없다.
    /// (<c>MiniGamePortal</c> 이 <c>ProximityPortal</c> 과 나뉘어 있는 것과 같은 이유다)
    ///
    /// <b>여기서 하지 않는 일</b> — 서버 호출 · 보유량 차감 · 제단 수치 계산 ·
    /// Fusion RPC · Blue VFX. 전부 다른 계층의 몫이다.
    ///
    /// ⚠ <b>Trigger Collider 를 쓰지 않는다.</b> <see cref="LocalPlayer"/> 등록소와의 거리로 잰다.
    ///    Trigger 는 남의 캐릭터도 들어오고, 상대 쪽 Rigidbody 구성에 의존하게 된다.
    ///    (설계 문서 5.2절 — <c>MiniGamePortal</c> · <c>ProximityPortal</c> 도 같은 방식이다)
    ///
    /// ⚠ <b>이 오브젝트는 계단 위 <c>Pedestal</c> 근처에 두어야 한다.</b>
    ///    프리팹 루트에 두면 계단 아래에서도 반응한다. Scene 뷰의 와이어 구로 확인한다.
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 2.6 · 5.2 · 5.3 · 5.5절
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarInteraction : MonoBehaviour
    {
        [Header("가까워야 상호작용할 수 있다")]
        [Tooltip("이 거리 안으로 들어오면 안내가 뜬다(m).")]
        [SerializeField, Min(0.5f)] private float openDistance = 4f;

        [Tooltip("이 거리 밖으로 나가야 안내가 사라진다(m). 여는 거리보다 커야 경계에서 깜빡이지 않는다.")]
        [SerializeField, Min(0.5f)] private float closeDistance = 6f;

        [Tooltip("높이 차이는 보지 않는다. 제단이 계단 위에 있으므로 켜 둔다.")]
        [SerializeField] private bool ignoreHeight = true;

        [Header("입력")]
        [Tooltip("봉헌 창을 여는 키. 미니게임 포탈은 F 를 쓰므로 겹치지 않는다.")]
        [SerializeField] private Key interactKey = Key.E;

        [Tooltip("열린 창을 닫는 키.")]
        [SerializeField] private Key closeKey = Key.Escape;

        /// <summary>범위에 들어오거나 나갔다. 화면 쪽이 이것만 듣는다.</summary>
        public static event Action<bool> NearChanged;

        /// <summary>범위 안에서 상호작용 키를 눌렀다.</summary>
        public static event Action InteractPressed;

        /// <summary>범위 안에서 닫기 키를 눌렀다.</summary>
        public static event Action ClosePressed;

        /// <summary>
        /// 지금 로비에 있는 제단 상호작용 지점. 없으면 null.
        ///
        /// 화면 쪽이 안내 문구를 만들 때 <see cref="PromptText"/> 를 읽으려고 둔다.
        /// </summary>
        public static AltarInteraction Active { get; private set; }

        /// <summary>지금 들어갈 수 있는가. 안내를 켜고 끄는 기준이다.</summary>
        public bool PlayerIsNear { get; private set; }

        /// <summary>안내에 띄울 문구. 인스펙터에서 키를 바꾸면 문구도 따라 바뀐다.</summary>
        public string PromptText => $"[{interactKey}] 조각 등록";

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            // 씬을 떠나며 꺼지는 경우다. 안내가 켜진 채로 남지 않게 한 번 내려 준다.
            if (PlayerIsNear)
            {
                PlayerIsNear = false;
                NearChanged?.Invoke(false);
            }
        }

        private void Update()
        {
            bool near = ResolveNear();

            if (near != PlayerIsNear)
            {
                PlayerIsNear = near;
                NearChanged?.Invoke(near);
            }

            if (!near)
            {
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys == null)
            {
                return;
            }

            // 봉헌 창이 열려 있거나 채팅을 치는 중이면 E 로 열지 않는다.
            // ⚠ Esc 는 막지 않는다. 봉헌 창도 이 잠금을 걸기 때문에 막으면 Esc 로 못 닫는다.
            if (keys[interactKey].wasPressedThisFrame && !ChatFocus.Typing)
            {
                InteractPressed?.Invoke();
            }
            else if (keys[closeKey].wasPressedThisFrame)
            {
                ClosePressed?.Invoke();
            }
        }

        /// <summary>
        /// 내 캐릭터가 범위 안에 있는가.
        ///
        /// ⚠ <b>오직 <see cref="LocalPlayer.Transform"/> 만 본다.</b> 다른 플레이어를 찾지 않는다.
        ///    남이 제단에 다가갔다고 내 화면에 안내가 뜨면 안 되기 때문이다.
        ///    등록은 <c>HasInputAuthority</c> 를 가진 피어에서만 일어나므로 이것으로 충분하다.
        ///
        /// <code>
        ///   내 클라이언트      내 캐릭터
        ///   남의 클라이언트    그 사람의 캐릭터 (내 것이 아니다)
        ///   Dedicated Server   null → 언제나 "멀다"
        /// </code>
        ///
        /// 들어오는 거리와 나가는 거리를 다르게 둔다(히스테리시스). 같으면 경계에 서 있을 때
        /// 안내가 깜빡인다. <c>ProximityPortal</c> 이 <c>6 / 8</c> 로 같은 일을 한다.
        /// </summary>
        private bool ResolveNear()
        {
            Transform local = LocalPlayer.Transform;
            if (local == null)
            {
                // 아직 접속 중이거나 전용 서버다. 둘 다 "멀다" 로 친다.
                return false;
            }

            Vector3 gap = local.position - transform.position;
            if (ignoreHeight)
            {
                gap.y = 0f;
            }

            float limit = PlayerIsNear ? closeDistance : openDistance;
            return gap.sqrMagnitude <= limit * limit;
        }

        private void OnValidate()
        {
            // 닫는 거리가 여는 거리보다 작으면 히스테리시스가 뒤집혀 안내가 깜빡인다.
            if (closeDistance < openDistance)
            {
                closeDistance = openDistance;
            }
        }

        /// <summary>
        /// Scene 뷰에서 범위를 눈으로 확인한다.
        ///
        /// ⚠ 이 오브젝트를 프리팹 루트에 두면 계단 아래까지 구가 덮는다. 그러면 계단 밑에서도
        ///    안내가 뜬다. 구가 <c>Pedestal</c> 주변만 감싸는지 보고 위치를 잡는다.
        ///    (<c>MiniGamePortal.OnDrawGizmosSelected</c> 와 같은 목적이다)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, openDistance);

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, closeDistance);
        }
    }
}

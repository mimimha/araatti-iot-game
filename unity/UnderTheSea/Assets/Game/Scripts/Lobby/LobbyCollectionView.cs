using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 로비 도감. 오른쪽 위 버튼으로 열고, X · Esc · 창 바깥 클릭으로 닫는다.
    ///
    /// <code>
    ///   LobbyCollection        이 부품이 붙어 있다
    ///     OpenButton           오른쪽 위 책 버튼. 누를 때마다 열고 닫는다
    ///     Window               열렸을 때만 켜진다
    ///       Backdrop           어둡게 깐 바닥. 누르면 닫힌다
    ///       Panel              도감 그림
    ///         CloseButton      그림 속 X 위에 겹친 투명 버튼
    /// </code>
    ///
    /// ⚠ <b>지금 창은 그림 한 장이다.</b> 칸마다 적힌 퍼센트도 그림에 그려진 것이라 실제 값이
    ///    아니다. 그림을 고친 뒤 칸 · 숫자를 부품으로 나눌 때 이 클래스에 그리기를 더한다.
    ///
    /// 만드는 곳: <c>Tools/아라아띠/로비 도감 프리팹 만들기</c> (LobbyCollectionSetup)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyCollectionView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);

            if (window != null) window.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // ⚠ 채팅칸이 켜져 있으면 그 Esc 는 채팅 몫이다. 제단 창과 같은 규칙이다.
            //    ChatFocus.Typing 은 나 자신도 보유자라 언제나 참이므로 쓰면 안 된다.
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && !ChatFocus.HeldByOther(this))
            {
                Close();
            }
        }

        /// <summary>
        /// 로비를 벗어나 설치기가 통째로 끄면 여기로 온다.
        /// 창을 연 채로 미니게임에 들어갔다 오면 도감이 떠 있지 않게 닫아 둔다.
        /// </summary>
        private void OnDisable()
        {
            if (window != null) window.SetActive(false);
            ReleaseFocus();
        }

        private void OnDestroy()
        {
            ReleaseFocus();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (window == null || IsOpen)
            {
                return;
            }

            window.SetActive(true);
            AcquireFocus();
        }

        /// <summary>닫는 길은 이것 하나다. X · Esc · 바깥 클릭 · 버튼이 모두 여기로 온다.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            window.SetActive(false);
            ReleaseFocus();
        }

        // ------------------------------------------------------------
        // 이동 잠금 — 열려 있는 동안 캐릭터가 걸어가지 않게 한다.
        // ⚠ 돌려주는 것을 빼먹으면 영영 못 움직인다. 모든 닫는 길이 ReleaseFocus 를 지난다.
        // ------------------------------------------------------------

        private bool holdsFocus;

        private void AcquireFocus()
        {
            if (holdsFocus)
            {
                return;
            }

            ChatFocus.Begin(this);
            holdsFocus = true;
        }

        private void ReleaseFocus()
        {
            if (!holdsFocus)
            {
                return;
            }

            ChatFocus.End(this);
            holdsFocus = false;
        }
    }
}

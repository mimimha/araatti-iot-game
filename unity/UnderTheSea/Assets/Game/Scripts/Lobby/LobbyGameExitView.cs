using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 로비 게임 종료 확인 창. ` 키로 열고, 종료 버튼으로 게임을 끈다.
    /// 취소 · ` · Esc · 창 바깥 클릭은 닫기만 한다.
    ///
    /// <code>
    ///   LobbyGameExit          이 부품이 붙어 있다
    ///     Window               열렸을 때만 켜진다
    ///       Backdrop           어둡게 깐 바닥. 누르면 닫힌다
    ///       Panel              "게임 종료 / 정말 게임을 종료하시겠습니까?" 그림
    ///         ConfirmButton    종료
    ///         CancelButton     취소
    /// </code>
    ///
    /// 만드는 곳: <c>Tools/아라아띠/로비 게임 종료 프리팹 만들기</c> (LobbyGameExitSetup)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyGameExitView : MonoBehaviour
    {
        [SerializeField] private GameObject window;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button backdropButton;

        public bool IsOpen => window != null && window.activeSelf;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(QuitGame);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);

            if (window != null) window.SetActive(false);
        }

        private void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null)
            {
                return;
            }

            // ⚠ 채팅칸 · 제단 창이 떠 있으면 그 입력은 그쪽 몫이다.
            //    채팅에 ` 를 치는데 종료 창이 뜨면 안 된다.
            if (ChatFocus.HeldByOther(this))
            {
                return;
            }

            if (keys.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
            }
            else if (IsOpen && keys.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        /// <summary>
        /// 로비를 벗어나 설치기가 통째로 끄면 여기로 온다.
        /// 창을 연 채로 미니게임에 들어갔다 오면 떠 있지 않게 닫아 둔다.
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

        /// <summary>닫는 길은 이것 하나다. 취소 · ` · Esc · 바깥 클릭이 모두 여기로 온다.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            window.SetActive(false);
            ReleaseFocus();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("[LobbyGameExitView] 게임 종료 요청 — 에디터에서는 Play 를 멈춥니다");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

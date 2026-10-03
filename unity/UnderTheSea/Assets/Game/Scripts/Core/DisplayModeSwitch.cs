using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **전체화면으로 시작하고, Alt+Enter 로 창 ↔ 전체화면을 오간다.**
    ///
    /// <code>
    ///   시작          전체화면 (모니터 기본 해상도, 테두리 없는 FullScreenWindow)
    ///   Alt+Enter     전체화면 → 1600×900 창     (예전에 게임시작.bat 이 띄우던 크기)
    ///                 창       → 전체화면
    /// </code>
    ///
    /// <b>왜 Unity 기본 Alt+Enter 를 쓰지 않는가.</b> 기본 전환(Player Settings 의
    /// <c>Allow Fullscreen Switch</c>)은 전체화면에서 창으로 갈 때 <b>해상도를 그대로 둔다.</b>
    /// 모니터만 한 창이 되어 화면 밖으로 넘친다. 1600×900 창으로 돌아오려면 크기를 직접 정해야 한다.
    /// 그래서 기본 전환은 끄고(<c>allowFullscreenSwitch: 0</c>) 여기서 한다. 둘 다 켜 두면 한 번 누를 때
    /// 두 번 바뀐다.
    ///
    /// 해상도가 바뀌어도 3D 그림판 크기는 <see cref="RenderScaleAuto"/> 가 1초 안에 따라 맞춘다.
    ///
    /// ⚠ 에디터 · 화면 없는 서버에서는 돌지 않는다. 에디터의 Game 뷰 크기를 건드리면 안 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayModeSwitch : MonoBehaviour
    {
        /// <summary>창 모드 크기. 예전 <c>게임시작.bat</c> 의 <c>-screen-width 1600 -screen-height 900</c>.</summary>
        private const int WindowWidth = 1600;
        private const int WindowHeight = 900;

        /// <summary>모니터가 창보다 작을 때 창이 모니터의 이만큼만 차지하게 줄인다.</summary>
        private const float MaxWindowFraction = 0.9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            var host = new GameObject("[화면 모드]");
            DontDestroyOnLoad(host);
            host.AddComponent<DisplayModeSwitch>();
        }

        private void Start()
        {
            GoFullscreen();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (!alt || !enter) return;

            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                GoFullscreen();
            }
            else
            {
                GoWindowed();
            }
        }

        /// <summary>
        /// 모니터 기본 해상도로. <c>Screen.currentResolution</c> 이 아니라 <c>Display.main.systemWidth</c>
        /// 를 쓴다 — 창 모드일 때 전자는 창 크기를 돌려줄 수 있다.
        /// </summary>
        private static void GoFullscreen()
        {
            int w = Display.main.systemWidth;
            int h = Display.main.systemHeight;

            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            Debug.Log($"[화면 모드] 전체화면 {w}x{h} (Alt+Enter 로 창 모드)");
        }

        /// <summary>1600×900 창. 모니터가 그보다 작으면 비율을 지키며 줄인다.</summary>
        private static void GoWindowed()
        {
            float fit = Mathf.Min(
                1f,
                Display.main.systemWidth * MaxWindowFraction / WindowWidth,
                Display.main.systemHeight * MaxWindowFraction / WindowHeight);

            int w = Mathf.RoundToInt(WindowWidth * fit);
            int h = Mathf.RoundToInt(WindowHeight * fit);

            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            Debug.Log($"[화면 모드] 창 {w}x{h} (Alt+Enter 로 전체화면)");
        }
    }
}

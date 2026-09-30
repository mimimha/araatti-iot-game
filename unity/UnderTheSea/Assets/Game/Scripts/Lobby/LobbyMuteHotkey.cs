using UnderTheSea.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 🔇 <b>로비에서 F1 로 음소거를 켜고 끈다.</b>
    ///
    /// <code>
    ///   소리 켜짐  F1 →  음소거
    ///   음소거     F1 →  타이틀 설정 슬라이더에 맞춰 둔 크기로 돌아온다
    /// </code>
    ///
    /// 크기를 새로 정하지 않는다. 슬라이더 값은 그대로 두고 음소거 깃발만 바꾼다(<see cref="AudioHub.ToggleMute"/>).
    /// 타이틀의 소리 아이콘과 같은 저장값이라 타이틀로 돌아가도 상태가 이어진다.
    ///
    /// ⚠ <b>로비에서만 받는다.</b> 광산 · 배 협동은 F1 이 디버그 화면이다(MineDebugHud · ShipCoopDebugHud).
    ///
    /// <c>Lobby.unity</c> 를 건드리지 않으려고 스스로 설치된다. (<see cref="LobbyDevMode"/> 와 같은 방식)
    /// </summary>
    public sealed class LobbyMuteHotkey : MonoBehaviour
    {
        private const Key ToggleKey = Key.F1;

        private bool inLobby;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 프로세스(Dedicated Server)에는 소리도 없다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            var host = new GameObject("[로비 음소거 F1]");
            DontDestroyOnLoad(host);
            host.AddComponent<LobbyMuteHotkey>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            inLobby = LobbyDevMode.InLobby();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        // 씬이 바뀔 때만 다시 본다. 매 프레임 찾지 않는다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => inLobby = LobbyDevMode.InLobby();

        private void OnSceneUnloaded(Scene scene) => inLobby = LobbyDevMode.InLobby();

        private void Update()
        {
            if (!inLobby) return;

            // F1 은 글자를 치지 않아 채팅 중에도 받는다.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[ToggleKey].wasPressedThisFrame) return;

            AudioHub hub = AudioHub.Instance;
            if (hub == null) return;

            hub.ToggleMute();
            Debug.Log($"[로비] F1 — {(hub.IsMuted ? "음소거" : $"소리 켬 ({AudioListener.volume:P0})")}", this);
        }
    }
}

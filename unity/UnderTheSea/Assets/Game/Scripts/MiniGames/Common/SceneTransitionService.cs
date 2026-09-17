using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 씬을 여는 일을 한 곳으로 모아 둔 얇은 층.
    ///
    /// 버튼이 직접 씬을 열면, 나중에 네트워크 씬 로드로 바꿀 때 버튼을 전부 찾아다녀야 한다.
    /// 여는 방법이 바뀌어도 부르는 쪽은 그대로이게 이 한 겹을 둔다.
    ///
    /// ⚠ 프로젝트 규칙상 <b>씬 전환은 SceneFlow(민화) 가 관리</b>한다 (GAME_STRUCTURE.md 3장).
    ///    그래서 여기서 <c>SceneManager.LoadScene</c> 을 직접 부르지 않는다. 지금은 무엇을
    ///    열려고 했는지 로그만 남기는 <b>스텁</b>이고, 실제 연결은 아래 세 함수 안쪽만
    ///    SceneFlow 호출로 갈아끼우면 된다.
    ///
    /// ── 서버/씬 담당자가 채울 곳 ──────────────────────────────────
    ///
    ///     LoadMiniGame(sceneName)  → SceneFlow 로 미니게임 씬 열기
    ///     ReloadMiniGame()         → SceneFlow.RestartCurrent() (이미 있음)
    ///     LoadLobby()              → SceneFlow 에 로비로 가는 public 함수가 필요하다 (아직 없음)
    /// </summary>
    public sealed class SceneTransitionService : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("로비 씬 이름. 하드코딩하지 않도록 여기서 관리한다.")]
        private string lobbySceneName = "Lobby";

        [SerializeField]
        [Tooltip("켜 두면 실제로 씬을 열지 않고 로그만 남긴다. 테스트 씬에서 쓴다.")]
        private bool stubOnly = true;

        /// <summary>마지막으로 열려고 한 미니게임 씬. [다시 하기] 가 본다.</summary>
        public string CurrentMiniGameScene { get; private set; }

        public string LobbySceneName => lobbySceneName;

        /// <summary>미니게임 씬을 연다. 이름이 비어 있으면 아직 Scene List 에 없다는 뜻이다.</summary>
        public void LoadMiniGame(string sceneName)
        {
            CurrentMiniGameScene = sceneName;

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.Log("[SceneTransition] 이동할 씬 이름이 비어 있습니다. " +
                          "MiniGameConfig.sceneName 을 채우면 여기서 열립니다.");
                return;
            }

            if (stubOnly)
            {
                Debug.Log($"[SceneTransition] (스텁) 미니게임 씬 '{sceneName}' 을 열 차례입니다. " +
                          "실제 이동은 SceneFlow 에 연결하세요.");
                return;
            }

            // ⚠ 씬을 직접 열지 않는다. Lobby Runner 를 먼저 끄지 않으면 미니게임 런처가
            //    "이미 돌고 있는 Runner 가 있다" 며 조용히 세션을 시작하지 않는다.
            //    그 순서는 MiniGameTransition 이 지킨다.
            //
            //    세션 이름은 넘기지 않는다 — 비워 두면 미니게임의 기본 세션으로 간다.
            //    진짜 매칭이 붙기 전까지는 고정 세션이고, 그 사실을 감추지 않는다.
            MiniGameTransition.Enter(sceneName);
        }

        /// <summary>같은 미니게임을 다시 연다. [다시 하기].</summary>
        public void ReloadMiniGame()
        {
            if (stubOnly)
            {
                Debug.Log($"[SceneTransition] (스텁) '{CurrentMiniGameScene}' 재시작. " +
                          "실제로는 SceneFlow.RestartCurrent() 를 부르면 됩니다.");
                return;
            }

            SceneFlow.RestartCurrent();
        }

        /// <summary>로비로 돌아간다.</summary>
        public void LoadLobby()
        {
            if (stubOnly)
            {
                Debug.Log($"[SceneTransition] (스텁) 로비 씬 '{lobbySceneName}' 으로 돌아갈 차례입니다.");
                return;
            }

            // 미니게임 Runner 를 끄고, 들어올 때 기억해 둔 채널로 다시 붙는다.
            // 채널에 붙으면 Fusion 이 Lobby 를 올리므로 여기서 씬을 따로 열지 않는다.
            MiniGameTransition.ReturnToLobby();
        }
    }
}

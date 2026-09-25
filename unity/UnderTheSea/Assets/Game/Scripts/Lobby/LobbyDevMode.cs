using System.Collections;
using System.Text;
using TMPro;
using UnderTheSea.Account;
using UnderTheSea.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 🛠 <b>로비 개발자 모드.</b> 광산 · 배 · 검 게임과 같은 방식이다.
    ///
    /// <code>
    ///   P   켜기 / 끄기
    ///   ]   섬 회복도 +1  (조각 없이. 목표가 100 이면 1%)
    ///   [   섬 회복도 -1
    /// </code>
    ///
    /// <b>화면에서 값을 직접 바꾸지 않는다.</b> 섬 회복도는 모두가 같이 보는 서버 DB 값이라
    /// API 에 부탁하고(<see cref="HttpApiConfig.AltarDevRecoveryPath"/>), 돌려받은 상태를 봉헌 · 조회와 같은
    /// 길(<see cref="AltarState.ApplyStateSnapshot"/>)로 넣는다.
    ///
    /// <b>다른 사람 화면은 바로 따라온다.</b> 바꾼 뒤 <see cref="AltarOfferingRelay.AnnounceStateChanged"/>
    /// 로 세션 전체에 "상태가 바뀌었다" 만 알리고, 받은 화면이 스스로 다시 조회한다(봉헌 알림과 같은 길,
    /// 펄스만 없다). 알림이 유실돼도 30초 주기 조회가 따라잡는다.
    ///
    /// ⚠ <b>클라이언트는 <c>-devmode</c>, API 는 <c>--devmode</c> 가 있어야 돈다.</b>
    ///    (<see cref="DevMode"/>, 에디터 · 로컬 API 는 항상) API 가 꺼져 있으면 404 가 난다.
    ///
    /// <c>Lobby.unity</c> 를 건드리지 않으려고 스스로 설치되고, 로비에 있을 때만 키를 받는다.
    /// </summary>
    public sealed class LobbyDevMode : MonoBehaviour
    {
        private const int FontSize = 14;
        private const float Margin = 10f;

        /// <summary>지금 켜져 있는가.</summary>
        public bool IsOn { get; private set; }

        private bool inLobby;
        private bool busy;
        private string lastResult = string.Empty;

        private GUIStyle style;
        private readonly StringBuilder text = new StringBuilder(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 프로세스(Dedicated Server)에는 만들지 않는다. -devmode 가 없으면 키도 화면도 없다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !DevMode.Enabled)
            {
                return;
            }

            var host = new GameObject("[로비 개발자 모드]");
            DontDestroyOnLoad(host);
            host.AddComponent<LobbyDevMode>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshInLobby();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshInLobby();

        private void OnSceneUnloaded(Scene scene) => RefreshInLobby();

        /// <summary>
        /// 씬이 바뀔 때만 다시 본다. 매 프레임 찾지 않는다.
        ///
        /// 미니게임에도 자기 개발자 패널이 같은 P 키로 있다. 로비가 아니면 이 패널은 닫고 키를 받지 않는다.
        /// </summary>
        private void RefreshInLobby()
        {
            inLobby = InLobby();

            if (!inLobby)
            {
                IsOn = false;
            }
        }

        /// <summary>
        /// 로비인가. ⚠ <see cref="SeaHeartCounterInstaller"/> 의 것과 같은 내용이다(그쪽이 private).
        /// 한쪽을 고치면 같이 고친다.
        /// </summary>
        private static bool InLobby()
        {
            Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);
            if (lobby.IsValid() && lobby.isLoaded)
            {
                return true;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name == SceneFlow.Lobby || go.name == "[" + SceneFlow.Lobby + "]")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Update()
        {
            if (!inLobby) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTyping()) return;

            if (keyboard[DevMode.PanelKey].wasPressedThisFrame)
            {
                IsOn = !IsOn;
                Debug.Log($"[로비 개발자] {(IsOn ? "켜짐" : "꺼짐")}", this);
                return;
            }

            if (!IsOn) return;

            if (busy) return;

            if (keyboard[Key.RightBracket].wasPressedThisFrame)
            {
                StartCoroutine(AdjustRecoveryRoutine(+1));
            }
            else if (keyboard[Key.LeftBracket].wasPressedThisFrame)
            {
                StartCoroutine(AdjustRecoveryRoutine(-1));
            }
        }

        /// <summary>
        /// 채팅 · 입력칸에 글을 쓰는 중인가. 로비에는 채팅이 있어서, 이걸 안 보면
        /// "p" 나 "]" · "[" 를 치는 순간 패널이 열리거나 회복도가 바뀐다.
        /// </summary>
        private static bool IsTyping()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;

            if (selected.TryGetComponent(out TMP_InputField tmp) && tmp.isFocused) return true;
            if (selected.TryGetComponent(out InputField legacy) && legacy.isFocused) return true;
            return false;
        }

        private IEnumerator AdjustRecoveryRoutine(int delta)
        {
            // 가짜 제단(오프라인 개발)이면 붙을 서버가 없다.
            if (AccountServiceLocator.Altar is not HttpAltarService)
            {
                Report("실제 계정 서버가 아니라 바꾸지 않습니다.");
                yield break;
            }

            IAuthService auth = AccountServiceLocator.Auth;
            string token = auth != null ? auth.AccessToken : string.Empty;
            if (string.IsNullOrEmpty(token))
            {
                Report("로그인하지 않아 바꾸지 못했습니다.");
                yield break;
            }

            busy = true;
            try
            {
                // 늦게 온 응답이 새 값을 덮지 않도록 순번을 먼저 받는다. (조회 · 봉헌과 같다)
                int sequence = AltarState.IssueSequence();

                string url = HttpApiConfig.Combine(null, HttpApiConfig.AltarDevRecoveryPath);
                string body = "{\"delta\":" + delta + "}";
                HttpJsonResult result = default;
                yield return HttpJson.Send(url, "POST", body, token, r => result = r);

                if (!result.IsSuccess)
                {
                    Report(result.StatusCode == 404
                        ? "API 가 개발자 모드가 아닙니다 (--devmode 없이 떴습니다)."
                        : $"실패 (HTTP {result.StatusCode}) — {result.FailureMessage}");
                    yield break;
                }

                if (!HttpJson.TryParse(result.Body, out AltarStateDto snapshot, out string parseFailure))
                {
                    Report($"응답을 읽지 못했습니다 — {parseFailure}");
                    yield break;
                }

                AltarState.ApplyStateSnapshot(sequence, snapshot);

                // 다른 사람 화면도 지금 다시 조회하게 한다. 실패해도 30초 주기 조회가 따라잡는다.
                bool announced = AltarOfferingRelay.AnnounceStateChanged();

                string where = $"{snapshot.recoveryPercent:0.#}% ({snapshot.totalOffered}/{snapshot.targetOffering})";
                bool atEdge = delta > 0
                    ? snapshot.totalOffered >= snapshot.targetOffering
                    : snapshot.totalOffered <= 0;
                Report((atEdge ? $"끝에 닿았습니다 — {where}" : $"섬 회복도 {where}")
                    + (announced ? string.Empty : "  (다른 사람에게 알리지 못함)"));
            }
            finally
            {
                busy = false;
            }
        }

        private void Report(string message)
        {
            lastResult = message;
            Debug.Log($"[로비 개발자] {message}", this);
        }

        private void OnGUI()
        {
            if (!IsOn || !inLobby) return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = FontSize,
                richText = false,
                padding = new RectOffset(10, 10, 8, 8),
            };

            text.Clear();
            text.AppendLine($"── 로비 개발자 모드 ({DevMode.PanelKey} 로 끈다) ──");
            text.AppendLine(AltarState.HasValue
                ? $"섬 회복도   {AltarState.RecoveryPercent:0.#}%   ({AltarState.TotalOffered}/{AltarState.TargetOffering})"
                : "섬 회복도   —");
            text.AppendLine();
            text.AppendLine("  ]   섬 회복도 +1");
            text.AppendLine("  [   섬 회복도 -1");

            if (!string.IsNullOrEmpty(lastResult))
            {
                text.AppendLine();
                text.Append(lastResult);
            }

            // 높이를 0 으로 넘기면 상자가 잘린다. 내용에 맞게 재서 오른쪽 위에서 아래로 자라게 한다.
            GUIContent content = new GUIContent(text.ToString());
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect(Screen.width - size.x - Margin, Margin, size.x, size.y), content, style);
        }
    }
}

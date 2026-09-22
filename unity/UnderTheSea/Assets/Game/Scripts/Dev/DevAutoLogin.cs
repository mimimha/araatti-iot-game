#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnderTheSea.Account;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Dev
{
    /// <summary>
    /// <b>에디터에서만</b> 동작하는 자동 로그인.
    ///
    /// Multiplayer Play Mode 로 창을 여러 개 띄우면 창마다 손으로 아이디를 쳐야 한다.
    /// 이 파일은 <b>가상 플레이어에 붙인 태그</b>로 계정을 골라 Title → Login → ChannelSelect
    /// 까지 스스로 눌러 준다.
    ///
    /// <b>씬을 하나도 고치지 않는다.</b> <c>RuntimeInitializeOnLoadMethod</c> 로 스스로 깨어나
    /// 씬이 올라올 때마다 그 씬의 화면 컨트롤러를 찾아 공개 메서드를 부른다. 씬 파일은
    /// 병합 충돌이 가장 심한 파일이라 개발 편의 기능이 손대서는 안 된다.
    ///
    /// <b>계정표가 없으면 아무 일도 하지 않는다.</b> 태그가 없어도, 태그가 표에 없어도 마찬가지다.
    /// 그래서 이 파일이 저장소에 있어도 평소 수동 로그인에는 영향이 없다.
    ///
    /// <b>계정표</b> — 프로젝트 폴더(Assets 바깥)의 <c>DevAccounts.local.json</c>
    /// <code>
    /// {
    ///   "accounts": [
    ///     { "tag": "p1", "email": "...", "password": "...", "channel": 0, "delaySeconds": 0 },
    ///     { "tag": "p2", "email": "...", "password": "...", "channel": 0, "delaySeconds": 12 }
    ///   ]
    /// }
    /// </code>
    /// 비밀번호가 들어가므로 <b>저장소에 올리지 않는다.</b> <c>.git/info/exclude</c> 로 막는다 —
    /// <c>.gitignore</c> 와 달리 추적되지 않아 브랜치를 옮겨도 따라다니고, 팀원에게도 안 퍼진다.
    ///
    /// ⚠ <b>delaySeconds 는 멋으로 있는 값이 아니다.</b>
    ///    닉네임과 외형은 PlayerPrefs 에 저장되는데, Windows 에서 PlayerPrefs 는
    ///    <c>HKCU\Software\(회사)\(제품)</c> 하나다. 가상 플레이어는 ProjectSettings 를
    ///    심볼릭 링크로 공유하므로 회사·제품 이름이 같고, 따라서 <b>PlayerPrefs 도 공유한다.</b>
    ///    두 계정이 동시에 로그인하면 나중 사람이 앞사람의 닉네임과 외형을 덮어쓴다.
    ///
    ///    로비에 들어가는 순간 외형은 네트워크 속성으로 복사되므로, <b>앞사람이 로비에 완전히
    ///    들어간 뒤</b>에 뒷사람이 로그인하면 서로 침범하지 않는다. 그래서 두 번째 계정부터는
    ///    넉넉히 지연을 준다.
    /// </summary>
    public static class DevAutoLogin
    {
        /// <summary>계정표 파일 이름. 프로젝트 폴더(Assets 의 부모)에 둔다.</summary>
        private const string TableFileName = "DevAccounts.local.json";

        /// <summary>채널 목록이 올라오기를 기다리는 한계. 넘으면 포기하고 손에 맡긴다.</summary>
        private const float ChannelWaitSeconds = 20f;

        private static Account _account;

        /// <summary>시작 지연은 실행당 한 번만 쓴다. 씬을 옮길 때마다 기다리면 안 된다.</summary>
        private static bool _delayUsed;

        // ------------------------------------------------------------
        // 계정표
        // ------------------------------------------------------------

        [Serializable]
        private class Account
        {
            public string tag;
            public string email;
            public string password;

            /// <summary>채널 목록에서 고를 순번. 0 이 첫 번째다.</summary>
            public int channel;

            /// <summary>로그인을 시작하기 전에 기다릴 시간. 위의 ⚠ 참고.</summary>
            public float delaySeconds;
        }

        [Serializable]
        private class Table
        {
            public Account[] accounts = Array.Empty<Account>();
        }

        // ------------------------------------------------------------
        // 시작
        // ------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            _account = null;
            _delayUsed = false;

            string[] tags = CurrentPlayerTags();
            if (tags.Length == 0)
            {
                // 태그가 없는 창은 평소대로 손으로 로그인한다. 조용히 물러난다.
                return;
            }

            Table table = LoadTable();
            if (table == null)
            {
                return;
            }

            _account = Match(table, tags);

            if (_account == null)
            {
                Debug.Log(
                    $"[자동로그인] 태그 [{string.Join(", ", tags)}] 에 맞는 계정이 " +
                    $"{TableFileName} 에 없습니다. 손으로 로그인해 주세요.");
                return;
            }

            Debug.Log(
                $"[자동로그인] 태그 \"{_account.tag}\" → {_account.email}, " +
                $"채널 {_account.channel}번, {_account.delaySeconds}초 뒤 시작합니다.");

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 이 창에 붙은 Multiplayer Play Mode 태그.
        ///
        /// 메인 에디터에도 태그를 붙일 수 있다. 태그를 다는 곳은
        /// <c>Window &gt; Multiplayer &gt; Multiplayer Play Mode</c> 의 플레이어별 Tags 칸이다.
        /// </summary>
        private static string[] CurrentPlayerTags()
        {
            try
            {
                return global::Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags()
                       ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                // Multiplayer Play Mode 가 꺼져 있는 등. 자동 로그인을 포기할 뿐 실행은 막지 않는다.
                Debug.Log($"[자동로그인] 태그를 읽지 못해 건너뜁니다 — {e.Message}");
                return Array.Empty<string>();
            }
        }

        private static Table LoadTable()
        {
            string path = TablePath();

            if (path.Length == 0)
            {
                Debug.Log(
                    $"[자동로그인] {TableFileName} 을 찾지 못해 건너뜁니다. " +
                    $"프로젝트 폴더(Assets 의 형제)에 두면 됩니다. " +
                    $"여기서부터 위로 올라가며 찾았습니다 — {Application.dataPath} " +
                    "(DevAutoLogin.cs 주석에 예시가 있습니다)");
                return null;
            }

            Debug.Log($"[자동로그인] 계정표를 찾았습니다 — {path}");

            try
            {
                Table table = JsonUtility.FromJson<Table>(File.ReadAllText(path));

                if (table?.accounts == null || table.accounts.Length == 0)
                {
                    Debug.LogWarning($"[자동로그인] {TableFileName} 에 계정이 하나도 없습니다.");
                    return null;
                }

                return table;
            }
            catch (Exception e)
            {
                Debug.LogError($"[자동로그인] {TableFileName} 을 읽지 못했습니다 — {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 계정표를 찾는다. 없으면 빈 문자열.
        ///
        /// ⚠ <b>가상 플레이어에서는 <c>Application.dataPath</c> 가 진짜 프로젝트를 가리키지 않는다.</b>
        ///    가상 플레이어는 <c>Library/VP/mppm(해시)/</c> 안에서 돌면서 <c>Assets</c> 를
        ///    심볼릭 링크로만 공유한다. 그래서 <c>dataPath/..</c> 는 그 클론 폴더다.
        ///
        ///        진짜 프로젝트 : C:\...\UnderTheSea\
        ///        가상 플레이어 : C:\...\UnderTheSea\Library\VP\mppm3dd14a4c\
        ///
        ///    실제로 이것 때문에 Player 2 만 계정표를 못 찾고 빈 로그인 화면에서 멈췄다.
        ///
        /// 그래서 <b>위로 거슬러 올라가며 찾는다.</b> 클론 폴더에서 시작해도
        /// <c>mppm… → VP → Library → 프로젝트</c> 세 칸이면 닿는다. 경로에서
        /// "Library/VP" 같은 문자열을 잘라내는 방법은 Unity 가 클론 위치를 바꾸면
        /// 조용히 깨지므로 쓰지 않는다.
        /// </summary>
        private static string TablePath()
        {
            var dir = new DirectoryInfo(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));

            // 클론에서 프로젝트까지 세 칸이다. 여유를 두되 디스크 전체를 훑지는 않는다.
            for (int i = 0; i < 6 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, TableFileName);
                if (File.Exists(candidate)) return candidate;

                dir = dir.Parent;
            }

            return string.Empty;
        }

        /// <summary>표에서 이 창의 태그와 맞는 첫 계정. 대소문자는 가리지 않는다.</summary>
        private static Account Match(Table table, string[] tags)
        {
            foreach (Account account in table.accounts)
            {
                if (account == null || string.IsNullOrWhiteSpace(account.tag)) continue;

                foreach (string tag in tags)
                {
                    if (string.Equals(tag, account.tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return account;
                    }
                }
            }

            return null;
        }

        // ------------------------------------------------------------
        // 화면마다 대신 눌러 주기
        // ------------------------------------------------------------

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_account == null) return;

            if (scene.name == SceneFlow.Title) Runner.Run(DriveTitle());
            else if (scene.name == SceneFlow.Login) Runner.Run(DriveLogin());
            else if (scene.name == SceneFlow.ChannelSelect) Runner.Run(DriveChannelSelect());
        }

        private static IEnumerator DriveTitle()
        {
            yield return WaitMyTurn();

            StartMenuController menu = null;
            yield return WaitFor(() => menu = UnityEngine.Object.FindAnyObjectByType<StartMenuController>(), 5f);

            if (menu == null)
            {
                Debug.LogWarning("[자동로그인] Title 에서 StartMenuController 를 찾지 못했습니다.");
                yield break;
            }

            Debug.Log("[자동로그인] Title — [시작] 을 누릅니다.");
            menu.StartGame();
        }

        private static IEnumerator DriveLogin()
        {
            yield return WaitMyTurn();

            LoginScreenController login = null;
            yield return WaitFor(() => login = UnityEngine.Object.FindAnyObjectByType<LoginScreenController>(), 5f);

            if (login == null)
            {
                Debug.LogWarning("[자동로그인] Login 에서 LoginScreenController 를 찾지 못했습니다.");
                yield break;
            }

            // 계정 서비스가 아직 안 올라왔으면 Submit 이 예전 경로로 빠진다. 준비될 때까지 기다린다.
            yield return WaitFor(() => AccountServiceLocator.IsReady ? login : null, 10f);

            if (!Fill(login, "emailField", _account.email) ||
                !Fill(login, "passwordField", _account.password))
            {
                yield break;
            }

            Debug.Log($"[자동로그인] Login — {_account.email} 로 로그인합니다.");
            login.SelectLoginTab();
            login.Submit();
        }

        private static IEnumerator DriveChannelSelect()
        {
            ChannelSelectController channels = null;
            yield return WaitFor(() => channels = UnityEngine.Object.FindAnyObjectByType<ChannelSelectController>(), 5f);

            if (channels == null)
            {
                Debug.LogWarning("[자동로그인] ChannelSelect 에서 컨트롤러를 찾지 못했습니다.");
                yield break;
            }

            // ⚠ **내 닉네임을 지금 붙잡아 둔다.**
            //    닉네임은 PlayerPrefs 에 있고, 가상 플레이어는 그것을 메인 에디터와 공유한다.
            //    (맨 위 ⚠ 참고) 뒤에 로그인한 쪽이 앞사람 값을 덮어쓰므로, 로그인 직후의
            //    내 값을 들고 있다가 입장 직전에 되돌려 놓는다. 기다리는 것보다 확실하다.
            string myNickname = SceneFlow.Nickname;

            // 채널 목록은 서버 응답을 기다려야 채워진다. 채워질 때까지 골라 보며 기다린다.
            float deadline = Time.realtimeSinceStartup + ChannelWaitSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                channels.Select(_account.channel);

                if (!string.IsNullOrEmpty(channels.SelectedServerId))
                {
                    // Join() 이 SceneFlow.NicknameOrDefault 를 읽는다. 바로 앞에서 맞춰 둔다.
                    if (!string.IsNullOrWhiteSpace(myNickname))
                    {
                        PlayerPrefs.SetString(SceneFlow.NicknameKey, myNickname);
                    }

                    Debug.Log(
                        $"[자동로그인] ChannelSelect — \"{channels.SelectedServerId}\" 에 입장합니다. " +
                        $"이름 \"{SceneFlow.NicknameOrDefault}\"");

                    channels.Join();
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Debug.LogWarning(
                $"[자동로그인] {ChannelWaitSeconds}초 동안 {_account.channel}번 채널이 목록에 없었습니다. " +
                "Dedicated Server 가 떠 있는지 확인해 주세요.");
        }

        // ------------------------------------------------------------
        // 거들기
        // ------------------------------------------------------------

        /// <summary>
        /// 내 차례를 기다린다. 실행당 한 번만 기다리고, 그다음 화면부터는 곧바로 진행한다.
        /// 앞사람과 PlayerPrefs 가 겹치지 않게 하는 것이 목적이다. (맨 위 ⚠ 참고)
        /// </summary>
        private static IEnumerator WaitMyTurn()
        {
            if (_delayUsed || _account.delaySeconds <= 0f)
            {
                _delayUsed = true;
                yield break;
            }

            _delayUsed = true;
            Debug.Log($"[자동로그인] 앞 플레이어가 자리를 잡도록 {_account.delaySeconds}초 기다립니다.");

            // ⚠ 기다린 뒤에도 반드시 한 줄 남긴다. 이 줄이 없으면 "대기 중" 과 "멈췄다" 를
            //    로그에서 구별할 수 없다. 실제로 그 구별이 안 돼 원인을 한참 못 찾았다.
            float began = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(_account.delaySeconds);

            Debug.Log($"[자동로그인] 기다림 끝 ({Time.realtimeSinceStartup - began:F1}초 지남). 이어서 진행합니다.");
        }

        /// <summary>
        /// <paramref name="probe"/> 가 null 이 아닌 값을 돌려줄 때까지 기다린다.
        /// 시간이 지나면 그냥 끝난다 — 부르는 쪽이 null 을 보고 판단한다.
        /// </summary>
        private static IEnumerator WaitFor(Func<UnityEngine.Object> probe, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (probe() != null) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// 로그인 화면의 입력칸에 글자를 넣는다.
        ///
        /// 칸은 <c>[SerializeField] private</c> 라 리플렉션으로 집는다. 화면 코드는 공용이라
        /// 개발 편의 기능을 위해 공개 API 를 늘리지 않는 쪽을 택했다. 대신 <b>이름이 바뀌면
        /// 조용히 실패하지 않고</b> 어디를 고쳐야 하는지 찍는다.
        /// </summary>
        private static bool Fill(LoginScreenController login, string fieldName, string text)
        {
            FieldInfo info = typeof(LoginScreenController).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (info == null || info.GetValue(login) is not TMP_InputField field)
            {
                Debug.LogError(
                    $"[자동로그인] LoginScreenController 의 \"{fieldName}\" 칸을 찾지 못했습니다. " +
                    "이름이 바뀌었다면 DevAutoLogin 의 Fill 호출부도 같이 고쳐 주세요.");
                return false;
            }

            field.text = text;
            return true;
        }

        // ------------------------------------------------------------
        // 코루틴을 돌릴 자리
        //
        // static 이라 MonoBehaviour 가 없다. 씬을 넘어도 살아남는 빈 오브젝트 하나를 만든다.
        // Hierarchy 에 보이지 않게 숨겨 개발자가 실수로 지우거나 헷갈리지 않게 한다.
        // ------------------------------------------------------------

        private class Host : MonoBehaviour { }

        private static Host _host;

        private static class Runner
        {
            public static void Run(IEnumerator routine)
            {
                if (_host == null)
                {
                    var go = new GameObject("[자동로그인]") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _host = go.AddComponent<Host>();
                }

                _host.StartCoroutine(routine);
            }
        }
    }
}
#endif

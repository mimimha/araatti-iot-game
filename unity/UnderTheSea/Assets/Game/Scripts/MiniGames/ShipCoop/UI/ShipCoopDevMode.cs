using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnderTheSea.MiniGames.ShipCoop.Net;

/// <summary>
/// 🛠 개발자 모드. 기다리지 않고 원하는 상황을 바로 만든다.
///
/// 왜 필요한가
///   페이즈가 진행도로 갈리기 때문에, 뒷 구간을 보려면 배를 거기까지 몰고 가야 합니다.
///   최저 속도로는 목적지까지 480초입니다. 사건 하나 확인하려고 그걸 기다릴 수 없습니다.
///   사건도 10~20초 간격으로 무작위로 오니, 파도를 보려는데 암초만 세 번 올 수 있습니다.
///
/// ⚠ **출시(Release) 빌드에도 들어갑니다.** 예전에는 `UNITY_EDITOR || DEVELOPMENT_BUILD` 로
///    감싸 개발 빌드에서만 켜졌는데, 시연 · 테스트를 Release 빌드로 하므로 풀었습니다.
///    켜기 전까지는 어떤 키도 먹지 않고 화면에 안내 한 줄만 뜹니다.
///
/// ⚠ **실행 인자 `-devmode` 가 있어야 뜹니다.** (`UnderTheSea.Core.DevMode`, 에디터는 항상)
///    없으면 이 화면을 만들지도 않고, 안내 한 줄도 안 뜨고, P 를 눌러도 아무 일이 없습니다.
///    서버도 `-devmode` 없이 떴으면 개발자 명령을 받지 않습니다(`ShipCoopEventSync.Rpc_DevCommand`).
///
/// 사용법
///   **씬에 아무것도 붙이지 않아도 됩니다.** 플레이를 누르면 스스로 뜹니다.
///   P 또는 F9 로 켜고 끕니다.
///   꺼져 있으면 어떤 키도 먹지 않습니다.
///
/// 왜 스스로 뜨는가
///   빈 오브젝트를 만들어 붙이는 단계가 있으면, 붙였는지 안 붙였는지가
///   "왜 안 켜지지" 의 첫 번째 원인이 됩니다. 게다가 씬을 저장해야 남습니다.
///   개발 도구가 준비물을 요구하면 안 됩니다.
///
/// 왜 F2 가 아닌가
///   **F2 는 유니티 에디터의 "이름 바꾸기" 단축키입니다.** 에디터가 먼저 먹어서
///   게임까지 오지 않습니다. F1 도 도움말로 잡히는 자리가 있습니다.
///   P 와 F9 는 에디터가 쓰지 않습니다.
///
/// 키가 게임 조작과 겹치지 않게 골랐습니다.
/// 게임은 W · A · S · D · Shift · Space · J · K · L · C 와 마우스 우클릭을 씁니다.
/// 여기는 숫자 · 기호와 P · O · R · G 만 씁니다.
/// </summary>
public class ShipCoopDevMode : MonoBehaviour
{
    /// <summary>씬에 이미 하나 있는지. 손으로 붙인 것과 겹쳐 뜨지 않게 한다.</summary>
    private static ShipCoopDevMode _instance;

    /// <summary>
    /// 플레이를 누르면 스스로 뜬다. 씬에 붙일 것이 없다.
    ///
    /// 배 협동 게임이 있는 씬에서만 뜹니다. 로비나 다른 미니게임에서는 뜨지 않습니다.
    /// </summary>
    ///
    /// ⚠ **네트워크 빌드에서는 씬이 바뀔 때마다 다시 확인해야 합니다.**
    ///
    ///    `AfterSceneLoad` 는 게임이 시작할 때 **첫 씬 딱 한 번**만 불립니다. 에디터에서 바로
    ///    `ShipCoopTest.unity` 를 열고 플레이하면 그 씬이 첫 씬이라 곧바로 뜨지만, 네트워크 빌드는
    ///    첫 씬이 아무것도 없는 시작 씬(`ShipCoopBoot`)입니다. 그때는 `ShipCoopGame` 이 없어 안 뜨고,
    ///    Fusion 이 나중에 진짜 게임 씬(`ShipCoop.unity`)을 열어도 이 콜백은 다시 안 불립니다 —
    ///    개발자 모드가 영영 안 켜졌습니다. 그래서 `SceneManager.sceneLoaded` 도 같이 구독해
    ///    **씬이 바뀔 때마다** 다시 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        TryCreate();
        SceneManager.sceneLoaded += (_, _) => TryCreate();
    }

    private static void TryCreate()
    {
        if (_instance != null)
        {
            return;
        }

        // -devmode 없이 실행했으면 만들지 않는다.
        if (!UnderTheSea.Core.DevMode.Enabled)
        {
            return;
        }

        // 손으로 붙여둔 것이 있으면 그것을 쓴다.
        ShipCoopDevMode existing = FindAnyObjectByType<ShipCoopDevMode>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        // 배 협동 게임이 없는 씬이면 뜰 이유가 없다.
        if (FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include) == null)
        {
            return;
        }

        var go = new GameObject("ShipCoopDevMode (자동)");
        _instance = go.AddComponent<ShipCoopDevMode>();

        Debug.Log("[개발자 모드] 준비됐다. P 또는 F9 로 켠다.", go);
    }

    [Header("켜고 끄기")]
    // ⚠ 켜는 키 P 는 인스펙터 칸이 아니라 코드(DevMode.PanelKey)에 둔다. 세 게임이 같은 키를 쓰고,
    //    씬에 저장된 값이 코드 기본값보다 우선해서 칸으로 두면 P 가 안 먹을 수 있다. (DevMode 참고)
    [Tooltip("P 말고 추가로 받는 켜고 끄기 키. P 는 늘 먹는다(DevMode.PanelKey).\n" +
             "F2 는 쓰지 마세요 — 유니티의 '이름 바꾸기' 단축키라 에디터가 먼저 먹습니다.")]
    [SerializeField] private Key altToggleKey = Key.F9;

    [Tooltip("켜면 씬을 시작할 때부터 켜져 있다. 매번 F2 를 누르기 귀찮을 때 쓴다.")]
    [SerializeField] private bool onAtStart = false;

    [Header("표시")]
    [SerializeField] private int fontSize = 14;

    [Tooltip("기본 자리(HP 알약 바로 밑)에서 더 옮기고 싶을 때만 쓴다.\n\n" +
             "HUD 기준(1920×1080) 픽셀이고, 화면 크기에 맞춰 같이 줄고 늘어난다.\n" +
             "0, 0 이면 HP 알약 바로 밑에 붙는다.")]
    [SerializeField] private Vector2 panelNudge = Vector2.zero;

    [Header("한 번에 바꾸는 양")]
    [Tooltip("진행도를 이만큼씩 옮긴다. (0 ~ 1)")]
    [SerializeField, Range(0.01f, 0.5f)] private float progressStep = 0.1f;

    [Tooltip("물을 이만큼씩 붓는다. (0 ~ 1)")]
    [SerializeField, Range(0.05f, 1f)] private float floodStep = 0.2f;

    /// <summary>지금 켜져 있는지</summary>
    public bool IsOn { get; private set; }

    private ShipCoopGame _game;
    private ShipHealth _health;
    private ShipVoyage _voyage;
    private ShipFlooding _flooding;
    private EventScheduler _scheduler;

    private readonly List<VoyageEvent> _events = new List<VoyageEvent>();
    private readonly StringBuilder _sb = new StringBuilder(1024);
    private GUIStyle _style;

    private void Awake()
    {
        // 씬에 손으로 붙여 둔 것도 -devmode 없이는 돌지 않는다. 스스로 뜨는 쪽은 TryCreate 가 막는다.
        if (!UnderTheSea.Core.DevMode.Enabled)
        {
            enabled = false;
            return;
        }

        _game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        _health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        _voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        _flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        _scheduler = FindAnyObjectByType<EventScheduler>(FindObjectsInactive.Include);

        // 씬에 있는 사건을 담아 숫자키에 붙인다.
        // 종류를 코드에 적어두지 않는 이유는, 사건을 새로 만들어 씬에 놓으면
        // 이 파일을 고치지 않고도 바로 눌러볼 수 있게 하기 위해서다.
        _events.AddRange(FindObjectsByType<VoyageEvent>(FindObjectsInactive.Include));

        // ⚠ 정렬한다. FindObjectsByType 의 순서는 보장되지 않아서,
        //    정렬하지 않으면 **실행할 때마다 번호가 바뀝니다.**
        //    어제 3번이 파도였는데 오늘은 암초면 도구로 쓸 수가 없습니다.
        //
        // ⚠ **정렬 기준이 ShipCoopEventSync 와 같아야 합니다.** 네트워크에서는 클라이언트가 누른 번호를
        //    호스트가 자기 목록에서 찾아 터뜨린다. 두 목록의 순서가 다르면 **화면에 보이는 번호와 실제로
        //    터지는 사건이 어긋난다.** 그래서 저쪽과 같은 "계층 경로" 순으로 맞춘다.
        _events.Sort((a, b) => string.CompareOrdinal(PathOf(a), PathOf(b)));

        IsOn = onAtStart;
    }

    private void OnDisable()
    {
        // 꺼질 때 손대둔 것을 되돌린다. 켜둔 채로 씬을 떠나면
        // 다음 판이 무적으로 시작해서 한참을 헤맨다.
        Restore();
    }

    private void OnDestroy()
    {
        // 도메인 리로드를 끈 설정에서는 static 이 살아남는다.
        // 비워두지 않으면 다음 플레이에서 스스로 뜨지 않는다.
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool toggled = keyboard[UnderTheSea.Core.DevMode.PanelKey].wasPressedThisFrame
                    || (altToggleKey != Key.None && keyboard[altToggleKey].wasPressedThisFrame);

        if (toggled)
        {
            IsOn = !IsOn;

            if (!IsOn)
            {
                Restore();
            }

            Debug.Log($"[개발자 모드] {(IsOn ? "켜짐" : "꺼짐")}", this);
            return;
        }

        if (!IsOn)
        {
            return;
        }

        ReadKeys(keyboard);
    }

    /// <summary>켜둔 것을 모두 원래대로. 끌 때와 사라질 때 부른다.</summary>
    private void Restore()
    {
        if (_health != null)
        {
            _health.Invincible = false;
        }

        if (_scheduler != null)
        {
            _scheduler.Paused = false;
        }

        Time.timeScale = 1f;
    }

    private void ReadKeys(Keyboard keyboard)
    {
        // ⚠ **규칙을 바꾸는 키는 호스트가 실행한다. 클라이언트는 호스트에게 부탁한다.**
        //
        //    사건 강제 발생 · 무적 · 진행도 · 물 · HP · 스케줄러 정지 · 재시작은 전부 규칙 쪽 값이다
        //    (11장 — Events/ · ShipHealth · ShipVoyage · ShipFlooding · EventScheduler 는 호스트만 계산).
        //    예전에는 이 키들이 누른 자리에서 바로 값을 바꿨다. 클라이언트에서 누르면 그 프레임에만
        //    잠깐 바뀌었다가 호스트가 보내는 진짜 값이 도착해 **곧바로 덮어써 없던 일이 됐다** —
        //    "어떤 숫자는 되고 어떤 숫자는 안 된다" 로 보였던 것이 이것이다. 호스트가 우연히 같은 사건을
        //    스스로 띄운 번호만 된 것처럼 보였다.
        //
        //    "호스트에서만 눌러라" 로 막는 것도 답이 아니다. 진짜 Dedicated Server 는 창도 입력도 없어서
        //    사람이 키를 누를 데가 없다. 그래서 **어느 창에서 눌러도 호스트가 실행**하도록 부탁을 보낸다.
        //    (ShipCoopEventSync.Rpc_DevCommand)
        //
        //    시간 배속(`-` `=` `\`)만 예외다. `Time.timeScale` 은 이 프로세스 안에서만 도는 값이다.
        // 1 ~ 9 — 사건을 즉시 발생시킨다
        int events = ShipCoopEventSync.Current != null ? ShipCoopEventSync.Current.EventCount : _events.Count;

        for (int i = 0; i < events && i < 9; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
            {
                Ask(ShipCoopEventSync.DevCommand.FireEvent, i, 0f);
            }
        }

        // 0 — 무적
        if (keyboard[Key.Digit0].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.ToggleInvincible, 0, 0f);
        }

        // [ ] — 진행도
        if (keyboard[Key.LeftBracket].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.MoveProgress, 0, -progressStep);
        }

        if (keyboard[Key.RightBracket].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.MoveProgress, 0, progressStep);
        }

        // ; ' — 물
        if (keyboard[Key.Semicolon].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.AddFlood, 0, floodStep);
        }

        if (keyboard[Key.Quote].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.ResetFlood, 0, 0f);
        }

        // , . — HP
        if (keyboard[Key.Comma].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.Damage, 0, 20f);
        }

        if (keyboard[Key.Period].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.RepairFull, 0, 0f);
        }

        // O — 스케줄러 정지. P 는 켜고 끄기라 옆자리로 옮겼다.
        if (keyboard[Key.O].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.TogglePause, 0, 0f);
        }

        // R — 처음부터 다시
        if (keyboard[Key.R].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.Restart, 0, 0f);
        }

        // G — 바로 도착해 성공 화면을 본다
        if (keyboard[Key.G].wasPressedThisFrame)
        {
            Ask(ShipCoopEventSync.DevCommand.ForceArrive, 0, 0f);
        }

        // - = \ — 시간 배속. 규칙이 아니라 이 프로세스만의 값이라 호스트 여부와 무관하게 듣는다.
        if (keyboard[Key.Minus].wasPressedThisFrame)
        {
            SetTimeScale(Time.timeScale * 0.5f);
        }

        if (keyboard[Key.Equals].wasPressedThisFrame)
        {
            SetTimeScale(Time.timeScale * 2f);
        }

        if (keyboard[Key.Backslash].wasPressedThisFrame)
        {
            SetTimeScale(1f);
        }
    }

    /// <summary>루트부터의 이름 경로. <c>ShipCoopEventSync.PathOf</c> 와 **같은 규칙**이어야 한다.</summary>
    private static string PathOf(VoyageEvent step)
    {
        if (step == null)
        {
            return string.Empty;
        }

        string path = step.name;

        for (Transform parent = step.transform.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }

    /// <summary>
    /// 규칙을 바꾸는 일을 **호스트에게 맡긴다.**
    ///
    /// 내가 호스트면(혹은 네트워크가 아예 없는 혼자 테스트 씬이면) 그 자리에서 바로 하고,
    /// 클라이언트면 RPC 로 부탁한다. 어느 창에서 눌러도 결과가 모두에게 똑같이 보인다.
    /// </summary>
    private void Ask(ShipCoopEventSync.DevCommand command, int index, float value)
    {
        if (ShipCoopNet.IsAuthorityHere)
        {
            // 혼자 테스트 씬에는 ShipCoopEventSync 가 없다. 그때는 여기 목록으로 직접 터뜨린다.
            if (command == ShipCoopEventSync.DevCommand.FireEvent && ShipCoopEventSync.Current == null)
            {
                FireEvent(index);
                return;
            }

            ShipCoopEventSync.RunDevCommand(command, index, value);
            return;
        }

        if (ShipCoopEventSync.Current == null)
        {
            Debug.LogWarning("[개발자 모드] 호스트에 부탁할 곳(ShipCoopEventSync)을 못 찾았다.", this);
            return;
        }

        ShipCoopEventSync.Current.Rpc_DevCommand((int)command, index, value);
    }

    /// <summary>사건 하나를 지금 터뜨린다. 이미 떠 있으면 아무 일도 없다.</summary>
    private void FireEvent(int index)
    {
        VoyageEvent e = _events[index];
        if (e == null)
        {
            return;
        }

        if (e.IsActive)
        {
            Debug.Log($"[개발자 모드] {e.WarningText} 는 이미 떠 있다.", this);
            return;
        }

        e.Begin();
    }

    private void MoveProgress(float delta)
    {
        if (_voyage == null)
        {
            return;
        }

        _voyage.SetProgress01(_voyage.Progress01 + delta);
        Debug.Log($"[개발자 모드] 진행도 {_voyage.Progress01:P0}", this);
    }

    private static void SetTimeScale(float scale)
    {
        Time.timeScale = Mathf.Clamp(scale, 0.25f, 8f);
        Debug.Log($"[개발자 모드] 배속 {Time.timeScale:0.##}x");
    }

    // ------------------------------------------------------------------ 화면

    /// <summary>
    /// 패널 왼쪽 위 모서리. **HP 알약 바로 밑**이다.
    ///
    /// 오른쪽 위에 있었는데 사건 카드가 그리로 오면서 겹쳤습니다. 왼쪽 위는 HP 알약
    /// 한 줄뿐이라 그 밑이 통째로 비어 있고, 아래쪽은 항해 바가 화면 맨 밑에 붙어
    /// 있어서 그 사이가 이 패널이 쓸 수 있는 가장 넓은 빈 자리입니다.
    ///
    /// ⚠ **HUD 는 캔버스 스케일러로 줄었다 늘었다 한다.**(1920×1080 기준, Match 0.5)
    ///    이쪽은 IMGUI 라 화면 실제 픽셀을 쓰므로, 같은 배율을 직접 곱해야 화면
    ///    크기가 달라져도 HP 알약 밑에 그대로 붙어 있습니다.
    /// </summary>
    private Vector2 PanelAnchor()
    {
        // ShipCoopHudV2Art 의 값. 왼쪽 여백 28, 위 여백 24, 알약 높이 90.
        const float Margin = 28f;
        const float TopMargin = 24f;
        const float PillHeight = 90f;

        float scale = HudScale();

        return new Vector2(
            (Margin + panelNudge.x) * scale,
            (TopMargin + PillHeight + TopMargin + panelNudge.y) * scale);
    }

    /// <summary>
    /// HUD 캔버스가 지금 몇 배로 그려지고 있는지. (ScaleWithScreenSize · 1920×1080 · Match 0.5)
    ///
    /// 유니티 CanvasScaler 가 쓰는 식 그대로입니다 — 가로 · 세로 배율을 로그로 섞습니다.
    /// </summary>
    private static float HudScale()
    {
        float logWidth = Mathf.Log(Screen.width / 1920f, 2f);
        float logHeight = Mathf.Log(Screen.height / 1080f, 2f);

        return Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, 0.5f));
    }

    private void OnGUI()
    {
        EnsureStyle();

        // 꺼져 있을 때도 여는 방법 한 줄은 남긴다.
        // 이게 없으면 "켜는 키가 뭐였지" 로 매번 돌아오게 된다.
        if (!IsOn)
        {
            // 안내 줄도 패널과 **같은 자리**에 둔다. 오른쪽 위는 사건 카드 자리다.
            Vector2 hint = PanelAnchor();

            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.Label(new Rect(hint.x, hint.y, 260f, 20f), $"{KeyName(UnderTheSea.Core.DevMode.PanelKey)} 또는 {KeyName(altToggleKey)} — 개발자 모드", _style);
            GUI.color = Color.white;
            return;
        }

        _sb.Clear();
        _sb.AppendLine($"── 개발자 모드 ({KeyName(UnderTheSea.Core.DevMode.PanelKey)} 로 끈다) ──");

        AppendState();
        _sb.AppendLine();
        AppendKeys();

        string text = _sb.ToString();
        Vector2 size = _style.CalcSize(new GUIContent(text));

        Vector2 at = PanelAnchor();

        var box = new Rect(at.x, at.y, Mathf.Max(size.x + 16f, 340f), size.y + 16f);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(box.x + 8f, box.y + 8f, box.width - 16f, box.height - 16f),
                  text, _style);
    }

    private void AppendState()
    {
        if (_health != null && _health.Invincible)
        {
            _sb.AppendLine("● 무적");
        }

        if (_scheduler != null && _scheduler.Paused)
        {
            _sb.AppendLine("● 사건 뿌리기 정지");
        }

        if (!Mathf.Approximately(Time.timeScale, 1f))
        {
            _sb.AppendLine($"● 배속 {Time.timeScale:0.##}x");
        }

        if (_voyage != null)
        {
            _sb.AppendLine($"진행도 {_voyage.Progress01:P0}  ·  속도 {_voyage.Speed:0.##} m/s");
        }

        if (_flooding != null && _flooding.HasWater)
        {
            _sb.AppendLine($"침수 {_flooding.Level01:P0}  ·  새는 구멍 {_flooding.LeakingPoints}개");
        }
    }

    private void AppendKeys()
    {
        for (int i = 0; i < _events.Count && i < 9; i++)
        {
            VoyageEvent e = _events[i];
            string name = e != null ? e.WarningText : "(없음)";
            string mark = e != null && e.IsActive ? "  ← 떠 있음" : string.Empty;
            _sb.AppendLine($"  {i + 1}   {name}{mark}");
        }

        _sb.AppendLine("  0   무적 켜기/끄기");
        _sb.AppendLine($"  [ ] 진행도 ∓{progressStep:P0}");
        _sb.AppendLine($"  ;   물 +{floodStep:P0}      '   물 비우기");
        _sb.AppendLine("  ,   HP -20        .   HP 가득");
        _sb.AppendLine("  O   사건 뿌리기 정지/재개");
        _sb.AppendLine("  - = 배속 ÷2 / ×2      \\   배속 1x");
        _sb.AppendLine("  R   처음부터 다시");
        _sb.AppendLine("  G   바로 도착 (성공 화면)");
    }

    /// <summary>화면에 적을 키 이름. Backquote 는 글자로 보여줘야 알아본다.</summary>
    private static string KeyName(Key key)
    {
        return key == Key.Backquote ? "`" : key.ToString();
    }

    private void EnsureStyle()
    {
        if (_style != null)
        {
            return;
        }

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = false,
            wordWrap = false,
        };

        _style.normal.textColor = Color.white;
    }
}

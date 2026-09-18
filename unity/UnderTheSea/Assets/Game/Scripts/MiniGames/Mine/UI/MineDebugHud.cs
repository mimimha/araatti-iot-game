using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 만드는 동안 Game 화면에 판 상태를 글자로 띄우는 개발용 표시.
///
/// MINE.md 4장이 **"남은 복구 수는 항상 보인다"** 고 요구한다. 이건 꾸미기가 아니라
/// 규칙의 일부다. 몇 개 남았는지 안 보이면 "지금 쓸까 남길까" 를 판단할 수 없다.
///
/// ⚠ **임시다. IMGUI(OnGUI) 로 그리므로 출시용이 아니다.**
///
/// 진짜 HUD(<see cref="MineHud"/>)가 생긴 뒤에도 이 파일을 아직 지우지 않는다.
/// **7단계 밸런싱에 쓸 숫자가 여기밖에 없기 때문이다** — 랜턴 반경, 탑뷰 높이,
/// 채점 상세. 진짜 HUD 에는 이런 것을 넣지 않는다. 밸런싱이 끝나면 지운다.
///
/// 그래서 **기본은 꺼져 있다.** 진짜 HUD 와 겹쳐 보이면 안 된다. F1 로 켠다.
///
/// 다만 **읽는 값은 임시가 아니다.** 이 표시는 <see cref="MineGame"/> 의 공개 속성만
/// 보므로, 진짜 HUD 를 만들 때 MineGame 은 한 줄도 안 고치고 이 파일만 갈아끼운다.
///
/// 예외가 하나 있다. 랜턴 반경(<see cref="MineVision"/>)은 **밸런싱용 숫자**라
/// 여기서만 읽는다. 반경이 정해지면 이 줄은 진짜 HUD 로 넘어가지 않는다.
///
/// 사용법
///   테스트 씬에 빈 오브젝트를 하나 만들고 붙인다. 나머지는 자동으로 찾는다.
/// </summary>
public class MineDebugHud : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private bool showKeyGuide = true;
    [SerializeField] private int fontSize = 14;

    [Header("켜고 끄기")]
    [Tooltip("이 키를 누르면 표시가 켜지고 꺼진다.")]
    [SerializeField] private Key toggleKey = Key.F1;

    private MineGame _game;

    /// <summary>랜턴 반경만 읽는다. 밸런싱용이라 진짜 HUD 에는 안 들어간다.</summary>
    private MineVision _vision;

    /// <summary>시점만 읽는다. 랜턴과 같은 이유로 진짜 HUD 에는 안 들어간다.</summary>
    private MineCamera _camera;
    private GUIStyle _style;

    // 진짜 HUD 와 겹치지 않게 기본은 꺼둔다. 밸런싱할 때 F1 로 켠다.
    private bool _visible;
    private readonly StringBuilder _sb = new StringBuilder(512);

    private void Awake()
    {
        _game = FindAnyObjectByType<MineGame>(FindObjectsInactive.Include);
        _vision = FindAnyObjectByType<MineVision>(FindObjectsInactive.Include);
        _camera = FindAnyObjectByType<MineCamera>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (toggleKey != Key.None && keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
        {
            _visible = !_visible;
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;

        EnsureStyle();
        Build();

        var content = new GUIContent(_sb.ToString());

        // 높이를 고정하면 줄이 늘어날 때 잘린다. 글자에 맞춰 계산한다.
        const float pad = 10f;
        const float width = 380f;
        float height = _style.CalcHeight(content, width - pad * 2f) + pad * 2f;

        var area = new Rect(12f, 12f, width, height);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + pad, area.y + pad,
                           area.width - pad * 2f, area.height - pad * 2f),
                  content, _style);
    }

    private void Build()
    {
        _sb.Clear();

        if (_game == null)
        {
            _sb.AppendLine("MineGame 을 찾지 못했습니다.");
            return;
        }

        _sb.AppendLine($"목표      {(_game.TargetName == string.Empty ? "(없음)" : _game.TargetName)}");
        _sb.AppendLine($"단계      {StateLabel(_game.State)}");

        if (_game.State == MineState.Turn)
        {
            _sb.AppendLine($"턴        {_game.TurnNumber} / {_game.TotalTurns}");
        }

        if (_game.State != MineState.Ready && _game.State != MineState.Finished)
        {
            _sb.AppendLine($"남은 시간 {_game.TimeLeft:0.0}초");
        }

        // MINE.md 4장 — 이 줄이 규칙이 요구하는 표시다.
        _sb.AppendLine($"복구      {_game.RestoresLeft} / {_game.TotalRestores}개  (팀 공용)");
        _sb.AppendLine($"힌트      {(_game.HintAvailable ? "쓸 수 있음" : "사용함 / 대기 중")}" +
                       $"{(_game.HintShowing ? "   ← 보는 중" : string.Empty)}");

        if (_vision != null)
        {
            _sb.AppendLine($"랜턴      {_vision.LanternRange:0.#}m  ({(_vision.Lit ? "밝음" : "어두움")})");
        }

        if (_camera != null)
        {
            _sb.AppendLine($"시점      {(_camera.IsBoardView ? "탑뷰" : "낮은 3인칭")}" +
                           $"  (탑뷰 높이 {_camera.BoardHeight:0.#}m)");
        }

        if (_game.State == MineState.Finished)
        {
            _sb.AppendLine();
            _sb.AppendLine($"결과      {(_game.Success ? "성공" : "실패")}  {_game.Result.Percent:0.0}%");
            _sb.AppendLine($"          {_game.Result}");
        }

        if (!showKeyGuide) return;

        _sb.AppendLine();
        _sb.AppendLine("WASD      이동");
        _sb.AppendLine("Space     휘두르기 (발밑 한 칸)");
        _sb.AppendLine("C         되메우기 (발밑 · 블록 1개)");
        _sb.AppendLine("J         힌트 (목표 다시 보기 · 1회)");
        _sb.AppendLine($"{toggleKey}        이 개발용 표시 끄기");
    }

    private static string StateLabel(MineState state)
    {
        return state switch
        {
            MineState.Ready => "대기",
            MineState.Reveal => "목표 공개 중",
            MineState.Turn => "채굴",
            MineState.TurnGap => "턴 사이",
            MineState.Finished => "끝",
            _ => state.ToString(),
        };
    }

    private void EnsureStyle()
    {
        if (_style != null && _style.fontSize == fontSize) return;

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.UpperLeft,
            richText = false,
        };
        _style.normal.textColor = Color.white;
    }
}
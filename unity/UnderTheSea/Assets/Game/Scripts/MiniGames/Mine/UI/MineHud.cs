using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광산 플레이 중 화면. (MINE.md 10장)
///
/// 10장이 요구하는 다섯 가지를 모두 띄운다.
/// 남은 시간 · 지금 누구 차례 · 남은 복구 수 · 내 힌트 사용 여부 · 남은 턴 수.
///
/// **목표 그림은 여기 없다.** 시작에만 보여주고 숨기는 것이 이 게임의 규칙이다.
/// 힌트를 쓸 때만 판 위에 잠깐 나타난다. (MINE.md 2장)
///
/// **읽기만 한다.** <see cref="MineGame"/> 의 공개 속성과 이벤트만 보고,
/// 게임 쪽은 한 줄도 고치지 않는다. 개발용 <see cref="MineDebugHud"/> 와 같은 약속이라
/// 화면을 통째로 갈아끼워도 게임은 그대로다.
///
/// **조각을 런타임에 만들지 않는다.** 칸을 코드로 찍어내면 인스펙터에서 손볼 수가 없다.
/// 계층은 프리팹으로 두고, 이 스크립트는 거기 꽂힌 글자와 색만 바꾼다.
///
/// **결과 화면은 여기 없다.** 판이 끝나면 숨는다. 결과와 보상은 공통 매칭 흐름이
/// 맡는다. (MINE.md 13장)
/// </summary>
public class MineHud : MonoBehaviour
{
    /// <summary>왼쪽 참가자 목록의 한 줄.</summary>
    [Serializable]
    public class PlayerRow
    {
        public Image frame;
        public TMP_Text nameText;
        public TMP_Text stateText;
    }

    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private MineGame game;

    [Tooltip("판이 끝나면 이걸로 숨긴다. 끄는 게 아니라 투명하게 만든다.")]
    [SerializeField] private CanvasGroup group;

    [Header("위")]
    [Tooltip("남은 시간. mm:ss")]
    [SerializeField] private TMP_Text timeText;

    [Tooltip("지금 누구 차례인가. 공개 중에는 안내 문구가 들어간다.")]
    [SerializeField] private TMP_Text phaseText;

    [Tooltip("남은 턴 수. \"2 / 4\"")]
    [SerializeField] private TMP_Text turnText;

    [Header("왼쪽 — 참가자")]
    [Tooltip("사람 수만큼. 남는 줄은 자동으로 숨긴다.")]
    [SerializeField] private PlayerRow[] rows = Array.Empty<PlayerRow>();

    [Header("아래")]
    [Tooltip("남은 복구 블록. 팀 공용이라 항상 보여야 한다. (MINE.md 4장)")]
    [SerializeField] private TMP_Text restoreText;

    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image hintIcon;

    [Header("색")]
    [SerializeField] private Color activeFrame = new Color(1f, 0.78f, 0.33f);
    [SerializeField] private Color idleFrame = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color activeLabel = new Color(1f, 0.85f, 0.5f);
    [SerializeField] private Color doneLabel = new Color(0.55f, 0.75f, 0.55f);
    [SerializeField] private Color waitLabel = new Color(0.65f, 0.65f, 0.7f);
    [SerializeField] private Color hintReady = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color hintUsed = new Color(0.45f, 0.45f, 0.5f);

    /// <summary>
    /// 참가자 이름. 아직 로스터가 없어서 P1~P4 로 둔다.
    /// 10단계에서 네트워크가 붙으면 <see cref="SetPlayerNames"/> 로 갈아끼운다.
    /// </summary>
    private string[] _names;

    /// <summary>지난 프레임에 그린 초. 같으면 문자열을 다시 만들지 않는다.</summary>
    private int _shownSeconds = -1;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<MineGame>(FindObjectsInactive.Include);
        if (group == null) group = GetComponent<CanvasGroup>();

        EnsureNames();
    }

    private void OnEnable()
    {
        if (game == null) return;

        game.StateChanged += OnStateChanged;
        game.TurnStarted += OnTurnStarted;
        game.RestoresChanged += OnRestoresChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (game == null) return;

        game.StateChanged -= OnStateChanged;
        game.TurnStarted -= OnTurnStarted;
        game.RestoresChanged -= OnRestoresChanged;
    }

    /// <summary>10단계에서 진짜 참가자 이름을 넣는다.</summary>
    public void SetPlayerNames(string[] names)
    {
        _names = names;
        EnsureNames();
        RefreshPlayers();
    }

    private void Update()
    {
        if (game == null || timeText == null) return;

        // 시간만 매 프레임 바뀐다. 나머지는 이벤트로 갱신한다.
        // 초가 그대로면 문자열을 다시 만들지 않는다 — 매 프레임 쓰레기를 만들 이유가 없다.
        bool ticking = game.State == MineState.Reveal
                       || game.State == MineState.Turn
                       || game.State == MineState.TurnGap;

        int seconds = ticking ? Mathf.CeilToInt(game.TimeLeft) : 0;
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        timeText.text = ticking ? $"{seconds / 60:00}:{seconds % 60:00}" : "--:--";
    }

    private void OnStateChanged(MineState state)
    {
        RefreshAll();
    }

    private void OnTurnStarted(int turn)
    {
        RefreshAll();
    }

    private void OnRestoresChanged(int left)
    {
        RefreshRestore();
    }

    private void RefreshAll()
    {
        if (game == null) return;

        // 판이 끝나면 숨는다. 결과 화면은 공통 매칭 흐름이 맡는다. (MINE.md 13장)
        if (group != null)
        {
            bool show = game.State != MineState.Finished;
            group.alpha = show ? 1f : 0f;
            group.blocksRaycasts = show;
        }

        if (turnText != null)
        {
            turnText.text = game.State == MineState.Turn
                ? $"{game.TurnNumber} / {game.TotalTurns}"
                : $"- / {game.TotalTurns}";
        }

        if (phaseText != null) phaseText.text = PhaseLabel();

        _shownSeconds = -1;   // 다음 Update 에서 다시 그리게 한다
        RefreshPlayers();
        RefreshRestore();
        RefreshHint();
    }

    private string PhaseLabel()
    {
        switch (game.State)
        {
            case MineState.Ready:    return "곧 시작합니다";
            case MineState.Reveal:   return "목표를 외우세요";
            case MineState.Turn:     return NameOf(game.TurnNumber - 1) + "의 차례";
            case MineState.TurnGap:  return "다음 차례";
            case MineState.Finished: return "채굴 종료";
            default:                 return string.Empty;
        }
    }

    private void RefreshPlayers()
    {
        if (rows == null || game == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            PlayerRow row = rows[i];
            if (row == null) continue;

            // 사람 수보다 줄이 많으면 남는 줄은 숨긴다.
            bool used = i < game.TotalTurns;
            GameObject go = RowObject(row);
            if (go != null) go.SetActive(used);
            if (!used) continue;

            if (row.nameText != null) row.nameText.text = NameOf(i);
            if (row.stateText == null) continue;

            // 턴 번호는 1부터, 줄 번호는 0부터다. 여기서 한 칸 어긋나기 쉽다.
            int turn = i + 1;
            bool digging = game.State == MineState.Turn && turn == game.TurnNumber;
            bool done = turn < game.TurnNumber;

            row.stateText.text = digging ? "채굴 중" : done ? "완료" : "대기";
            row.stateText.color = digging ? activeLabel : done ? doneLabel : waitLabel;

            if (row.frame != null) row.frame.color = digging ? activeFrame : idleFrame;
        }
    }

    private void RefreshRestore()
    {
        if (restoreText == null || game == null) return;
        restoreText.text = game.RestoresLeft + " / " + game.TotalRestores;
    }

    private void RefreshHint()
    {
        if (game == null) return;

        bool ready = game.HintAvailable;
        bool lit = ready || game.HintShowing;

        if (hintText != null)
        {
            hintText.text = game.HintShowing ? "보는 중" : ready ? "V · 1회" : "사용함";
            hintText.color = lit ? hintReady : hintUsed;
        }

        if (hintIcon != null) hintIcon.color = lit ? hintReady : hintUsed;
    }

    private static GameObject RowObject(PlayerRow row)
    {
        if (row.frame != null) return row.frame.gameObject;
        if (row.nameText != null) return row.nameText.gameObject;
        return row.stateText != null ? row.stateText.gameObject : null;
    }

    private string NameOf(int index)
    {
        EnsureNames();
        if (index < 0 || index >= _names.Length) return "?";
        return _names[index];
    }

    private void EnsureNames()
    {
        int need = game != null ? Mathf.Max(game.TotalTurns, 1) : 4;
        if (_names != null && _names.Length >= need) return;

        var next = new string[need];
        for (int i = 0; i < need; i++)
        {
            next[i] = _names != null && i < _names.Length ? _names[i] : "P" + (i + 1);
        }

        _names = next;
    }
}

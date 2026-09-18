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
/// **결과 화면은 여기 없다.** 결과와 보상은 공통 매칭 흐름이 맡는다. (MINE.md 13장)
///
/// ⚠ 다만 **끝났다는 사실 한 줄은 남긴다.** 공통 흐름은 10단계에나 붙는데,
///   그때까지 판이 끝나면 화면에 아무것도 안 남아서 멈춘 것과 구분이 안 된다.
///   실제로 마지막 턴에 힌트를 누른 사람이 "힌트가 안 꺼진다"고 읽었다.
///   공통 결과 화면이 붙으면 <see cref="hideOnFinish"/> 를 켠다.
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

    [Tooltip("판이 끝나면 HUD 를 감출 것인가. 공통 결과 화면이 붙기 전에는 꺼둔다 — 켜면 끝났다는 표시가 아무 데도 안 남는다.")]
    [SerializeField] private bool hideOnFinish;

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

    [Header("카운트다운 — 시작 직전에만")]
    [Tooltip("3 2 1 을 띄운다. 화면 가운데 크게.")]
    [SerializeField] private TMP_Text countdownText;

    [Header("결과 — 판이 끝났을 때만")]
    [Tooltip("끝났을 때만 켠다. 공통 결과 화면이 붙으면 통째로 끈다.")]
    [SerializeField] private GameObject resultPanel;

    [Tooltip("\"실패 53.7%\" 처럼 크게 띄운다.")]
    [SerializeField] private TMP_Text resultText;

    [Tooltip("결과 아래 한 줄. 목표와 판 것을 적는다.")]
    [SerializeField] private TMP_Text resultDetailText;

    [Header("색")]
    [SerializeField] private Color activeFrame = new Color(1f, 0.78f, 0.33f);
    [SerializeField] private Color idleFrame = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color activeLabel = new Color(1f, 0.85f, 0.5f);
    [SerializeField] private Color doneLabel = new Color(0.55f, 0.75f, 0.55f);
    [SerializeField] private Color waitLabel = new Color(0.65f, 0.65f, 0.7f);
    [SerializeField] private Color hintReady = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color hintUsed = new Color(0.45f, 0.45f, 0.5f);

    [Tooltip("내 줄의 테두리. 채굴 중 테두리(금색)와 확실히 달라야 한다.")]
    [SerializeField] private Color selfFrame = new Color(0.45f, 0.8f, 1f, 0.55f);

    // 남의 힌트 동안 화면을 덮는 한마디에 쓰는 값. 셋 다 코드에만 둔다.
    //
    // [SerializeField] 로 두면 안 된다. 덮개는 프리팹이 아니라 코드가 만드는데,
    // 필드를 직렬화하는 순간 프리팹의 임포트 결과에 값이 한 번 박히고 그 값이 이긴다.
    // 여기 적은 초기값을 고쳐도 프리팹을 다시 저장하지 않는 한 무시된다 —
    // 실제로 알파를 세 번 바꿨는데 화면은 계속 첫 값(검정 불투명)이었다.
    private const float CenterNoticeFontSize = 90f;
    private static readonly Color CenterNoticeBackdrop = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color CenterNoticeLabelColor = new Color(1f, 0.92f, 0.78f, 1f);

    [SerializeField] private Color successColor = new Color(0.55f, 0.92f, 0.62f);
    [SerializeField] private Color failColor = new Color(0.95f, 0.55f, 0.5f);

    /// <summary>
    /// 참가자 이름. 아직 로스터가 없어서 P1~P4 로 둔다.
    /// 10단계에서 네트워크가 붙으면 <see cref="SetPlayerNames"/> 로 갈아끼운다.
    /// </summary>
    private string[] _names;

    /// <summary>지난 프레임에 그린 초. 같으면 문자열을 다시 만들지 않는다.</summary>
    private int _shownSeconds = -1;

    // 힌트는 이벤트가 없다. 지난 프레임 값과 견줘서 바뀔 때만 다시 그린다.
    private bool _shownHintReady;
    private bool _shownHintShowing;

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

        // ⚠ StateChanged 만으로는 점수를 못 읽는다.
        //   EnterFinished 는 **첫 줄에서** 상태를 Finished 로 바꾸고, 채점은 그 뒤에 한다.
        //   그래서 StateChanged 시점의 Result 는 아직 비어 있어 0.0% 로 보인다.
        //   채점이 끝난 뒤 오는 Finished 를 받아 한 번 더 그린다.
        game.Finished += OnGameFinished;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (game == null) return;

        game.StateChanged -= OnStateChanged;
        game.TurnStarted -= OnTurnStarted;
        game.RestoresChanged -= OnRestoresChanged;
        game.Finished -= OnGameFinished;
    }

    /// <summary>10단계에서 진짜 참가자 이름을 넣는다.</summary>
    public void SetPlayerNames(string[] names)
    {
        _names = names;
        EnsureNames();
        RefreshPlayers();
    }

    // ------------------------------------------------------------
    // 네트워크 전환용 덧붙임 (광산 서버화 1단계)
    // ------------------------------------------------------------

    /// <summary>
    /// 네트워크가 이 HUD 를 대신 몰고 있는가.
    ///
    /// 네트워크에서는 <c>MineGame</c> 이 꺼져 있다. 그대로 두면 시작도 안 한 판의
    /// 기본값(<c>- / 4 TURN</c> · "곧 시작합니다")이 계속 떠 있어 거짓말을 한다.
    /// 켜지면 아래 값들만 그린다. <c>MineGame</c> 은 한 줄도 보지 않는다.
    ///
    /// 혼자 하는 씬에서는 아무도 켜지 않으므로 예전 그대로다.
    /// </summary>
    public bool NetworkDriven { get; set; }

    /// <summary>"동료를 기다리는 중" · "P1 채굴 중" 같은 한 줄.</summary>
    public string NetworkPhaseText { get; set; }

    /// <summary>"1 / 2" 같은 턴 표시. 턴 전에는 빈 문자열.</summary>
    public string NetworkTurnText { get; set; }

    /// <summary>"00:27" 같은 남은 시간. 없으면 빈 문자열.</summary>
    public string NetworkTimeText { get; set; }

    /// <summary>카운트다운 숫자. 0 이면 안 띄운다.</summary>
    public int NetworkCountdown { get; set; }

    /// <summary>결과 화면을 띄울 것인가.</summary>
    public bool NetworkResultShow { get; set; }

    /// <summary>"성공!" 또는 "실패".</summary>
    public string NetworkResultText { get; set; }

    /// <summary>"82점 · 유사도 82.4%" 같은 두 줄.</summary>
    public string NetworkResultDetail { get; set; }

    /// <summary>
    /// 화면 한가운데에 띄울 한마디. 빈 문자열이면 안 띄운다.
    ///
    /// 남이 힌트를 보는 동안 관전자에게 "힌트타임" 을 알리는 데 쓴다.
    ///
    /// ⚠ 카운트다운 숫자와 <b>같은 오브젝트</b>를 쓴다. 둘은 겹치지 않는다 —
    ///   카운트다운은 시작 전(Countdown), 힌트는 턴 중(Turn)에만 나온다.
    ///   글자라서 숫자용 크기(220pt)로는 화면을 넘치므로 크기를 바꿔 쓴다.
    /// </summary>
    public string NetworkCenterNotice { get; set; }

    /// <summary>
    /// 이 화면의 주인이 몇 번 자리인가. 그 줄의 테두리를 <see cref="selfFrame"/> 으로 칠한다.
    /// -1 이면 아무 줄도 칠하지 않는다(관전).
    ///
    /// ⚠ <b>창이 뜬 순서와 슬롯 번호는 다르다.</b> 슬롯은 서버에 붙은 순서로 정해지고
    ///   (<c>MineMatchState.ReseatWaitingCrew</c> 가 <c>JoinTick</c> 으로 줄 세운다),
    ///   창 위치는 프로세스가 시작한 순서로 정한다. 둘은 접속 지연 때문에 어긋난다 —
    ///   먼저 띄운 창이 나중에 붙어 왼쪽 창이 P2 가 된 판이 실제로 나왔다.
    ///   그래서 화면에 표시하지 않으면 누가 누구인지 알 방법이 없다.
    ///
    /// ⚠ 전에는 왼쪽 위에 "나 = P1" 딱지를 따로 만들어 띄웠다. 명단을 감추던 때라
    ///   그 구석이 비어 있었는데, 명단을 살리자 <b>P1 줄과 겹쳤다.</b> 같은 것을
    ///   두 군데 적을 이유가 없어 명단 쪽에 합쳤다.
    /// </summary>
    public int NetworkSelfSlot { get; set; } = -1;

    /// <summary>"4 / 4" 같은 팀 복구 수. 빈 문자열이면 감춘다.</summary>
    public string NetworkRestoreText { get; set; }

    /// <summary>
    /// 내 힌트 상태. "J · 1회" · "사용함" · "보는 중" · "대기" 중 하나다.
    ///
    /// ⚠ <b>내 것만 적는다.</b> 남이 힌트를 쓰는 중이라도 이 칸은 내 것을 보여 준다 —
    ///   남의 힌트는 화면을 덮는 <see cref="NetworkCenterNotice"/> 가 알린다.
    ///   그래서 이 값은 판 전체를 보는 <c>MineMatchState</c> 가 아니라
    ///   내가 누구인지 아는 <c>MineLocalView</c> 가 넣는다.
    /// </summary>
    public string NetworkHintText { get; set; }

    /// <summary>힌트가 아직 살아 있는가. 글자와 아이콘 색을 가른다.</summary>
    public bool NetworkHintLit { get; set; }

    /// <summary>참가자 몇 명인가. 남는 줄은 감춘다. 0 이면 명단을 통째로 감춘다.</summary>
    public int NetworkRosterSize { get; set; }

    /// <summary>지금 파는 사람의 자리. -1 이면 아무도 아니다(대기 · 공개 · 종료).</summary>
    public int NetworkCurrentSlot { get; set; } = -1;

    /// <summary>실행 중에 만든 덮개. 한 번만 만들고 켜고 끄기만 한다.</summary>
    private GameObject _noticeRoot;
    private TMP_Text _noticeLabel;

    /// <summary>
    /// 참가자 명단. <see cref="RefreshPlayers"/> 와 **같은 규칙**으로 그린다.
    ///
    /// 솔로는 턴 번호(1부터)로 재고 네트워크는 자리 번호(0부터)로 재는 것만 다르다.
    /// 그래서 한 칸 어긋나기 쉽다 — 여기서는 줄 번호와 자리 번호가 그대로 맞는다.
    /// </summary>
    private void DrawNetworkPlayers()
    {
        if (rows == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            PlayerRow row = rows[i];
            if (row == null) continue;

            // 참가자보다 줄이 많으면 남는 줄은 감춘다.
            bool used = i < NetworkRosterSize;
            SetActive(RowObject(row), used);
            if (!used) continue;

            if (row.nameText != null) row.nameText.text = NameOf(i);
            if (row.stateText == null) continue;

            // ⚠ 판이 끝나면 서버가 CurrentSlot 을 -1 로 되돌린다(EnterFinished).
            //   그대로 두면 다 판 사람들이 "대기" 로 보인다. 끝났으면 전부 완료다.
            bool digging = NetworkCurrentSlot >= 0 && i == NetworkCurrentSlot;
            bool done = NetworkResultShow || (NetworkCurrentSlot >= 0 && i < NetworkCurrentSlot);

            row.stateText.text = digging ? "채굴 중" : done ? "완료" : "대기";
            row.stateText.color = digging ? activeLabel : done ? doneLabel : waitLabel;

            // ⚠ 채굴 중이 내 줄보다 우선이다. 둘을 한 테두리로 표시하므로 하나만 이긴다.
            //   내 턴에는 내 줄이 금색이 되는데, 그때는 내가 조작하고 있어 헷갈리지 않는다.
            if (row.frame != null)
            {
                row.frame.color = digging ? activeFrame
                    : i == NetworkSelfSlot ? selfFrame
                    : idleFrame;
            }
        }
    }

    /// <summary>
    /// 팀 복구 수.
    ///
    /// ⚠ 판이 시작하기 전에는 감춘다. 서버가 <c>BeginMatch</c> 에서야 총량을 정하므로
    ///   그 전에 그리면 "0 / 0" 이 뜬다. 솔로는 총량이 계산값이라 이 문제가 없다.
    /// </summary>
    private void DrawNetworkRestore()
    {
        bool show = !string.IsNullOrEmpty(NetworkRestoreText);
        SetActive(restoreText, show);

        if (show && restoreText != null) restoreText.text = NetworkRestoreText;
    }

    /// <summary>내 힌트 상태. 글자는 <c>MineLocalView</c> 가 정하고 색만 여기서 가른다.</summary>
    private void DrawNetworkHint()
    {
        bool show = !string.IsNullOrEmpty(NetworkHintText);
        SetActive(hintText, show);

        if (show && hintText != null)
        {
            hintText.text = NetworkHintText;
            hintText.color = NetworkHintLit ? hintReady : hintUsed;
        }

        if (hintIcon != null) hintIcon.color = NetworkHintLit ? hintReady : hintUsed;
    }

    private static void SetActive(Component target, bool on)
    {
        if (target != null && target.gameObject.activeSelf != on) target.gameObject.SetActive(on);
    }

    private static void SetActive(GameObject target, bool on)
    {
        if (target != null && target.activeSelf != on) target.SetActive(on);
    }

    /// <summary>서버가 준 값으로 그린다. <c>MineGame</c> 은 보지 않는다.</summary>
    private void DrawNetwork()
    {
        DrawNetworkPlayers();
        DrawNetworkRestore();
        DrawNetworkHint();

        if (countdownText != null)
        {
            bool counting = NetworkCountdown > 0;
            SetActive(countdownText, counting);
            if (counting) countdownText.text = NetworkCountdown.ToString();
        }

        DrawCenterNotice();

        // 결과는 끝났을 때만 띄운다. 그 전에는 빈 칸이 보이면 안 된다.
        //
        // hideOnFinish 를 켜면 이 칸을 통째로 감춘다. 공용 결과 화면이 붙은 씬에서는
        // 결과가 두 번 뜨기 때문이다. 배·검 게임과 마찬가지로 화면은 하나여야 한다.
        SetActive(resultPanel, NetworkResultShow && !hideOnFinish);

        if (NetworkResultShow)
        {
            if (resultText != null) resultText.text = NetworkResultText ?? string.Empty;
            if (resultDetailText != null) resultDetailText.text = NetworkResultDetail ?? string.Empty;
        }

        if (phaseText != null) phaseText.text = NetworkPhaseText ?? string.Empty;
        if (turnText != null) turnText.text = NetworkTurnText ?? string.Empty;
        if (timeText != null) timeText.text = string.IsNullOrEmpty(NetworkTimeText) ? "--:--" : NetworkTimeText;
    }

    /// <summary>
    /// 남이 힌트를 보는 동안 <b>화면 전체를 덮고</b> 한마디를 띄운다.
    ///
    /// 관전자가 남의 힌트를 공짜로 같이 보지 못하게 하는 것이 목적이라, 반투명이 아니라
    /// <b>불투명으로 덮는다.</b> 판도 캐릭터도 보이면 안 된다.
    ///
    /// ⚠ 이 덮개는 <b>실행 중에 만든다.</b> 프리팹에 넣지 않은 이유가 있다 —
    ///   이 캔버스 프리팹을 고치면 그것을 담은 씬마다 RectTransform 오버라이드가
    ///   붙어 씬이 매번 더러워진다. 실제로 겪은 문제다. 덮개는 전면을 채우는 사각형과
    ///   가운데 글자뿐이라 코드로 만드는 편이 값이 싸다.
    /// </summary>
    private void DrawCenterNotice()
    {
        bool show = !string.IsNullOrEmpty(NetworkCenterNotice);

        if (!show)
        {
            if (_noticeRoot != null) SetActive(_noticeRoot, false);
            return;
        }

        EnsureNoticeOverlay();
        if (_noticeRoot == null) return;

        SetActive(_noticeRoot, true);

        // 항상 맨 위에 둔다. 다른 칸이 나중에 켜져도 덮개가 가려지면 안 된다.
        _noticeRoot.transform.SetAsLastSibling();

        if (_noticeLabel != null && _noticeLabel.text != NetworkCenterNotice)
        {
            _noticeLabel.text = NetworkCenterNotice;
        }
    }

    /// <summary>덮개를 한 번만 만든다. 전면 사각형 + 가운데 글자 둘뿐이다.</summary>
    private void EnsureNoticeOverlay()
    {
        if (_noticeRoot != null) return;

        var back = new GameObject("CenterNotice", typeof(RectTransform));
        back.transform.SetParent(transform, false);

        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = Vector2.zero;
        backRect.offsetMax = Vector2.zero;

        var image = back.AddComponent<Image>();
        image.color = CenterNoticeBackdrop;
        image.raycastTarget = false;   // 누를 것이 없다. 아래 칸의 클릭을 막을 이유도 없다.

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(back.transform, false);

        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = CenterNoticeFontSize;
        label.color = CenterNoticeLabelColor;
        label.raycastTarget = false;

        // 글꼴은 이미 쓰고 있는 것을 빌린다. 한글이 나와야 하므로 기본 글꼴로는 안 된다.
        TMP_Text donor = countdownText != null ? countdownText : timeText;
        if (donor != null && donor.font != null) label.font = donor.font;

        _noticeRoot = back;
        _noticeLabel = label;
    }

    private void Update()
    {
        if (NetworkDriven) { DrawNetwork(); return; }

        if (game == null) return;

        // 카운트다운은 매 프레임 본다. 남은 시간이 곧 띄울 숫자다.
        //
        // 올림을 쓴다 — 3초가 남았으면 "3" 이고, 1초 아래로 내려가야 "1" 이 된다.
        // 내림을 쓰면 시작하자마자 "2" 가 뜨고 마지막 1초는 "0" 이 된다.
        if (countdownText != null)
        {
            bool counting = game.State == MineState.Countdown;
            if (countdownText.gameObject.activeSelf != counting)
                countdownText.gameObject.SetActive(counting);

            if (counting)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(game.TimeLeft));
                string text = n.ToString();
                if (countdownText.text != text) countdownText.text = text;
            }
        }

        if (timeText == null) return;

        // 시간만 매 프레임 바뀐다. 나머지는 이벤트로 갱신한다.
        // 초가 그대로면 문자열을 다시 만들지 않는다 — 매 프레임 쓰레기를 만들 이유가 없다.
        bool ticking = game.State == MineState.Reveal
                       || game.State == MineState.Turn
                       || game.State == MineState.TurnGap;

        // ⚠ 힌트는 쓴 순간에 알려주는 이벤트가 없다. MineGame 이 알리는 것은
        //   상태 전환 · 턴 시작 · 복구 변화 뿐이고, 힌트를 쓰는 것은 그 셋 중
        //   아무것도 아니다. 그래서 여기서 값을 보고 바뀌면 다시 그린다.
        //   (전에는 다음 턴 시작 때 얻어걸려 갱신됐는데, 턴마다 힌트를
        //    되돌려주게 바꾸면서 영영 안 바뀌게 됐다)
        if (game.HintAvailable != _shownHintReady || game.HintShowing != _shownHintShowing)
        {
            _shownHintReady = game.HintAvailable;
            _shownHintShowing = game.HintShowing;
            RefreshHint();
        }

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

    private void OnGameFinished(bool success, int score)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (game == null) return;

        // 공통 결과 화면이 붙으면 숨긴다. 그 전에는 남겨야 끝난 줄 안다.
        if (group != null)
        {
            bool show = !hideOnFinish || game.State != MineState.Finished;
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

        RefreshResult();

        _shownSeconds = -1;   // 다음 Update 에서 다시 그리게 한다
        _shownHintReady = game.HintAvailable;
        _shownHintShowing = game.HintShowing;
        RefreshPlayers();
        RefreshRestore();
        RefreshHint();
    }

    private string PhaseLabel()
    {
        switch (game.State)
        {
            case MineState.Ready:    return "곧 시작합니다";
            case MineState.Countdown: return "준비";
            case MineState.Reveal:   return "목표를 외우세요";
            case MineState.Turn:     return NameOf(game.TurnNumber - 1) + "의 차례";
            case MineState.TurnGap:  return "다음 차례";
            case MineState.Finished: return "채굴 종료";
            default:                 return string.Empty;
        }
    }

    // 판이 끝났을 때만 가운데에 크게 띄운다.
    //
    // ⚠ 이 칸은 임시다. 결과와 보상은 공통 매칭 흐름 몫이다 (MINE.md 13장).
    //   그것이 붙기 전까지 끝났다는 표시가 아무 데도 안 남는 것을 막으려고 둔다.
    private void RefreshResult()
    {
        bool done = game.State == MineState.Finished;

        if (resultPanel != null) resultPanel.SetActive(done);
        if (!done) return;

        if (resultText != null)
        {
            resultText.text = (game.Success ? "성공" : "실패")
                              + "  " + game.Result.Percent.ToString("0.0") + "%";
            resultText.color = game.Success ? successColor : failColor;
        }

        if (resultDetailText != null)
        {
            resultDetailText.text = "목표 " + game.Result.TargetCount + "칸"
                                    + "   ·   판 것 " + game.Result.DugCount + "칸";
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
            // ⚠ HintAvailable 은 "지금 쓸 수 있는가" 라서 내 턴이 아니면 false 다.
            //   그걸 그대로 "사용함" 으로 적으면, 공개 7초에 쓰지도 않은 힌트가
            //   이미 쓴 것처럼 보인다. 쓸 수 없는 때와 써버린 때를 갈라야 한다.
            if (game.HintShowing) hintText.text = "보는 중";
            else if (game.State != MineState.Turn) hintText.text = "대기";
            else hintText.text = ready ? "J · 1회" : "사용함";

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

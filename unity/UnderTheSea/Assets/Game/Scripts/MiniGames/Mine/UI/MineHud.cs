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

    [Tooltip("복구 판 그림. 블록을 다 쓰면 아래 \"다 쓴 그림\" 으로 갈아끼운다.\n" +
             "판 전체를 그리는 Image 를 넣는다.")]
    [SerializeField] private Image restoreIcon;

    [Tooltip("복구 블록이 남아 있을 때의 그림.")]
    [SerializeField] private Sprite restoreReadySprite;

    [Tooltip("다 써서 0 이 됐을 때의 그림. 흑백판이다.\n" +
             "⚠ 위 그림과 **크기가 같아야 한다.** 다르면 갈아끼울 때 자리가 튄다.")]
    [SerializeField] private Sprite restoreUsedSprite;

    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image hintIcon;

    [Tooltip("힌트를 아직 쓸 수 있을 때의 그림. 아래 \"쓴 그림\" 과 짝으로 넣는다.\n" +
             "둘 다 비어 있으면 예전처럼 색만 어둡게 바꾼다.")]
    [SerializeField] private Sprite hintReadySprite;

    [Tooltip("힌트를 쓴 뒤(또는 못 쓸 때)의 그림. 흑백판이다.\n" +
             "⚠ 두 그림은 **크기가 같아야 한다.** 다르면 갈아끼울 때 자리가 튄다.")]
    [SerializeField] private Sprite hintUsedSprite;

    [Header("카운트다운 — 시작 직전에만")]
    [Tooltip("3 2 1 을 띄운다. 화면 가운데 크게.\n" +
             "⚠ 아래 그림이 채워져 있으면 그쪽이 이긴다. 이 글자는 그림이 없을 때만 쓴다.")]
    [SerializeField] private TMP_Text countdownText;

    [Tooltip("숫자 그림. **1부터 차례로** 넣는다 — [0]=1, [1]=2, [2]=3.\n" +
             "비워 두면 위의 글자로 띄운다. 솔로 씬처럼 그림을 안 붙인 곳이 그대로 동작한다.")]
    [SerializeField] private Image[] countdownImages;

    [Header("도안 공개 — 공개 7초 동안만")]
    [Tooltip("\"도안을 기억하세요\" 안내. 목표를 보여 주는 7초 동안 띄운다.\n" +
             "타이머 바로 아래에 둔다. 남은 초는 위 타이머가 세므로 여기는 글자뿐이다.")]
    [SerializeField] private GameObject memorizeTitlePanel;

    [Header("채굴 종료 — 판이 끝난 직후 잠깐")]
    [Tooltip("\"채굴 종료\" 제목. 판이 끝나고 이것부터 뜬다.\n" +
             "이 동안에는 네 캐릭터가 판 위에 서 있고 성적표는 아직 안 나온다.\n" +
             "(MineMatchState.finishTitleSeconds)")]
    [SerializeField] private GameObject finishTitlePanel;

    [Header("결과 — 채굴 종료 다음")]
    [Tooltip("성공·실패와 점수. \"채굴 종료\" 가 끝나면 켠다.\n" +
             "⚠ 공용 결과 화면이 그 뒤에 또 열린다. 이것은 광산 전용 성적표다.")]
    [SerializeField] private GameObject resultPanel;

    [Tooltip("성적표 판 그림. 성공·실패에 따라 아래 두 그림으로 갈아끼운다.")]
    [SerializeField] private Image resultPanelImage;

    [Tooltip("성공했을 때의 판. 제목 \"성공!\" 과 칸 이름이 그림에 박혀 있다.")]
    [SerializeField] private Sprite resultSuccessSprite;

    [Tooltip("실패했을 때의 판.\n" +
             "⚠ 위 그림과 **크기가 같아야 한다.** 칸 자리가 겹쳐야 글자가 안 튄다.")]
    [SerializeField] private Sprite resultFailSprite;

    [Tooltip("① 큰 칸 — 최종 점수. 그림에 박힌 \"도안 유사도\" 라벨 위다.")]
    [SerializeField] private TMP_Text resultScoreText;

    [Tooltip("② 가로 칸 — 점수 구간별 한 줄 평.")]
    [SerializeField] private TMP_Text resultCommentText;

    [Tooltip("③ 왼쪽 — 도안 전체 칸 수. 그림에 박힌 \"목표\" 라벨 아래다.")]
    [SerializeField] private TMP_Text resultTargetText;

    [Tooltip("③ 오른쪽 — 실제로 판 전체 칸 수. 그림에 박힌 \"채굴\" 라벨 아래다.")]
    [SerializeField] private TMP_Text resultDugText;

    [Header("색")]
    [SerializeField] private Color activeFrame = new Color(1f, 0.78f, 0.33f);
    [SerializeField] private Color idleFrame = new Color(1f, 1f, 1f, 0.15f);
    [Tooltip("\"채굴 중\" — 지금 파는 사람. 이 줄만 눈에 띄어야 한다.")]
    [SerializeField] private Color activeLabel = new Color(1f, 0.85f, 0.5f);

    // ⚠ "채굴 완료" 와 "대기 중" 은 **같은 회색**이다. 파는 사람 하나만 도드라지게
    //   두려는 것이라, 끝난 사람과 기다리는 사람을 색으로 가르지 않는다.
    //   둘을 다시 가르고 싶으면 여기만 다르게 두면 된다.
    [SerializeField] private Color doneLabel = new Color(0.65f, 0.65f, 0.7f);
    [SerializeField] private Color waitLabel = new Color(0.65f, 0.65f, 0.7f);
    [SerializeField] private Color hintReady = new Color(1f, 0.82f, 0.35f);
    [SerializeField] private Color hintUsed = new Color(0.45f, 0.45f, 0.5f);

    [Tooltip("복구 블록이 남아 있을 때의 숫자 색.")]
    [SerializeField] private Color restoreReady = new Color(0.94f, 0.94f, 0.96f);

    [Tooltip("다 썼을 때의 숫자 색. 판이 흑백이 되므로 글자도 같이 죽여야 어울린다.")]
    [SerializeField] private Color restoreUsed = new Color(0.45f, 0.45f, 0.5f);

    [Tooltip("내 줄의 테두리. 채굴 중 테두리(금색)와 확실히 달라야 한다.")]
    [SerializeField] private Color selfFrame = new Color(0.45f, 0.8f, 1f, 0.55f);

    [Header("참가자 줄 그림 — 비워 두면 위의 색으로 칠한다")]
    [Tooltip("지금 채굴 중인 줄. 금색 테두리 그림이다.")]
    [SerializeField] private Sprite rowDiggingSprite;

    [Tooltip("대기·완료한 줄. 회색 그림이다.\n" +
             "⚠ 위 그림과 **크기가 같아야 한다.** 다르면 턴이 넘어갈 때 줄이 튄다.")]
    [SerializeField] private Sprite rowIdleSprite;

    [Tooltip("그림을 쓸 때 **내 줄**에만 얹는 색. 흰색으로 두면 남의 줄과 구분이 안 된다.\n" +
             "채굴 중인 줄은 그림 자체가 금색이라 여기서 건드리지 않는다.")]
    [SerializeField] private Color selfTintOnArt = new Color(1f, 0.93f, 0.78f);

    // 남의 힌트 동안 화면을 덮는 한마디에 쓰는 값. 셋 다 코드에만 둔다.
    //
    // [SerializeField] 로 두면 안 된다. 덮개는 프리팹이 아니라 코드가 만드는데,
    // 필드를 직렬화하는 순간 프리팹의 임포트 결과에 값이 한 번 박히고 그 값이 이긴다.
    // 여기 적은 초기값을 고쳐도 프리팹을 다시 저장하지 않는 한 무시된다 —
    // 실제로 알파를 세 번 바꿨는데 화면은 계속 첫 값(검정 불투명)이었다.
    private const float CenterNoticeFontSize = 90f;
    private static readonly Color CenterNoticeBackdrop = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color CenterNoticeLabelColor = new Color(1f, 0.92f, 0.78f, 1f);

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

    /// <summary>
    /// 판이 끝났는가. <b>참가자 줄을 전부 "채굴 완료" 로 만든다.</b>
    ///
    /// ⚠ 성적표가 떴는지(<see cref="NetworkResultShow"/>)로 가리면 안 된다. 그 값은
    ///   "채굴 종료" 2초가 지나야 참이라, 그 2초 동안 다 판 사람들이 "대기 중" 으로
    ///   되돌아가 보인다.
    /// </summary>
    public bool NetworkMatchOver { get; set; }

    /// <summary>"도안을 기억하세요" 를 띄울 것인가. 공개 7초 동안만 참이다.</summary>
    public bool NetworkMemorizeShow { get; set; }

    /// <summary>"채굴 종료" 제목을 띄울 것인가. 판이 끝난 직후 잠깐이다.</summary>
    public bool NetworkFinishTitleShow { get; set; }

    /// <summary>성적표를 띄울 것인가. "채굴 종료" 가 끝난 뒤다.</summary>
    public bool NetworkResultShow { get; set; }

    /// <summary>성공인가. 성적표 판 그림과 점수 색을 가른다.</summary>
    public bool NetworkResultSuccess { get; set; }

    /// <summary>최종 점수. 유사도와 **같은 값**이다 (MINE.md 2장).</summary>
    public int NetworkResultScore { get; set; }

    /// <summary>도안 전체 칸 수. 맞힌 칸 수가 아니다.</summary>
    public int NetworkResultTargetCount { get; set; }

    /// <summary>실제로 판 전체 칸 수. 맞힌 칸 수가 아니다.</summary>
    public int NetworkResultDugCount { get; set; }

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

    /// <summary>복구 블록이 아직 남았는가. 0 이 되면 판이 흑백으로 바뀐다.</summary>
    public bool NetworkRestoreLit { get; set; } = true;

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

            // 상태는 셋뿐이다.
            //
            //   대기 중    아직 자기 차례가 안 왔다. 공개 7초에는 넷 다 여기다
            //   채굴 중    지금 그 사람 차례다. 한 번에 하나뿐
            //   채굴 완료  자기 차례가 지났다
            //
            // ⚠ 판이 끝나면 서버가 CurrentSlot 을 -1 로 되돌린다(EnterFinished).
            //   그것만 보면 다 판 사람들이 "대기 중" 으로 보인다. 끝났으면 전부 완료다.
            //   그래서 끝났는지를 따로 받는다 — 성적표가 떴는지(NetworkResultShow)로
            //   가리면 안 된다. 그 값은 "채굴 종료" 2초가 지나야 참이 되기 때문이다.
            bool digging = NetworkCurrentSlot >= 0 && i == NetworkCurrentSlot;
            bool done = NetworkMatchOver || (NetworkCurrentSlot >= 0 && i < NetworkCurrentSlot);

            row.stateText.text = digging ? "채굴 중" : done ? "채굴 완료" : "대기 중";
            row.stateText.color = digging ? activeLabel : done ? doneLabel : waitLabel;

            // ⚠ 채굴 중이 내 줄보다 우선이다. 둘을 한 테두리로 표시하므로 하나만 이긴다.
            //   내 턴에는 내 줄이 금색이 되는데, 그때는 내가 조작하고 있어 헷갈리지 않는다.
            DrawRowFrame(row.frame, digging, i == NetworkSelfSlot);
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

        DrawRestoreArt(NetworkRestoreLit);
    }

    /// <summary>
    /// 복구 판을 남은 블록에 맞춰 그린다. <b>다 쓰면 흑백판으로 갈아끼운다.</b>
    /// 힌트 판과 같은 방식이다. (<see cref="DrawHintArt"/>)
    ///
    /// 숫자는 그림이 아니라 TMP 라 색을 따로 죽여 준다. 판만 흑백이 되고 숫자가
    /// 그대로면 거기만 살아 있는 것처럼 보인다.
    /// </summary>
    private void DrawRestoreArt(bool lit)
    {
        if (restoreText != null) restoreText.color = lit ? restoreReady : restoreUsed;

        if (restoreIcon == null) return;
        if (restoreReadySprite == null || restoreUsedSprite == null) return;

        Sprite want = lit ? restoreReadySprite : restoreUsedSprite;
        if (restoreIcon.sprite != want) restoreIcon.sprite = want;

        // ⚠ 그림을 쓸 때는 색을 흰색으로 고정한다. 어두운 색이 남아 있으면
        //   흑백판이 한 번 더 어두워져 거의 안 보인다.
        if (restoreIcon.color != Color.white) restoreIcon.color = Color.white;
    }

    /// <summary>
    /// 참가자 한 줄의 테두리. <b>그림이 붙어 있으면 그림, 없으면 색.</b>
    ///
    /// 그림은 두 장뿐이라(채굴 중 · 쉬는 중) <b>내 줄은 색을 살짝 얹어</b> 가른다.
    /// 채굴 중인 줄이 내 줄이기도 하면 채굴 중이 이긴다 — 그때는 내가 조작하고
    /// 있어서 어느 줄이 내 것인지 헷갈리지 않는다.
    /// </summary>
    private void DrawRowFrame(Image frame, bool digging, bool mine)
    {
        if (frame == null) return;

        bool hasArt = rowDiggingSprite != null && rowIdleSprite != null;

        if (!hasArt)
        {
            frame.color = digging ? activeFrame : mine ? selfFrame : idleFrame;
            return;
        }

        Sprite want = digging ? rowDiggingSprite : rowIdleSprite;
        if (frame.sprite != want) frame.sprite = want;

        // ⚠ 그림을 쓸 때는 색이 곱해진다. 채굴 중인 줄에 색을 얹으면 금색이 물든다.
        Color tint = !digging && mine ? selfTintOnArt : Color.white;
        if (frame.color != tint) frame.color = tint;
    }

    /// <summary>
    /// 카운트다운 숫자. <paramref name="number"/> 가 0 이면 아무것도 안 띄운다.
    ///
    /// <b>그림이 붙어 있으면 그림, 없으면 글자.</b> 솔로 씬처럼 그림을 안 붙인 곳도
    /// 예전 그대로 동작한다. 네트워크와 솔로가 같은 함수를 쓴다 — 둘이 갈라져 있으면
    /// 한쪽만 고치는 일이 생긴다.
    ///
    /// ⚠ 숫자가 그림 수를 넘으면(카운트다운을 5초로 늘린 경우 등) 그림을 못 고른다.
    ///   그때는 글자로 떨어뜨린다. 조용히 아무것도 안 뜨는 것보다 낫다.
    /// </summary>
    private void DrawCountdown(int number)
    {
        bool counting = number > 0;
        bool hasArt = countdownImages != null && countdownImages.Length > 0;
        int index = number - 1;
        bool useArt = hasArt && counting && index >= 0 && index < countdownImages.Length
                      && countdownImages[index] != null;

        if (hasArt)
        {
            for (int i = 0; i < countdownImages.Length; i++)
            {
                if (countdownImages[i] == null) continue;
                SetActive(countdownImages[i], useArt && i == index);
            }
        }

        if (countdownText == null) return;

        // 그림으로 띄웠으면 글자는 끈다. 둘 다 켜지면 숫자가 겹쳐 보인다.
        bool useText = counting && !useArt;
        SetActive(countdownText, useText);

        if (useText) countdownText.text = number.ToString();
    }

    /// <summary>
    /// 내 힌트 상태. 글자는 <c>MineLocalView</c> 가 정하고 보이는 것만 여기서 가른다.
    ///
    /// <b>그림 두 장을 갈아끼운다.</b> 쓸 수 있으면 원래 그림, 쓰고 나면 흑백판이다.
    /// 색만 어둡게 하면 주황빛이 그대로 남아 "못 쓴다" 로 안 읽힌다.
    ///
    /// 그림을 안 넣은 곳(솔로 씬)에서는 예전처럼 색으로만 가른다.
    /// </summary>
    private void DrawNetworkHint()
    {
        bool show = !string.IsNullOrEmpty(NetworkHintText);
        SetActive(hintText, show);

        if (show && hintText != null)
        {
            hintText.text = NetworkHintText;
            hintText.color = NetworkHintLit ? hintReady : hintUsed;
        }

        DrawHintArt(NetworkHintLit);
    }

    /// <summary>
    /// 힌트 그림을 상태에 맞춰 바꾼다. 두 그림이 다 있으면 갈아끼우고,
    /// 없으면 색만 바꾼다.
    /// </summary>
    private void DrawHintArt(bool lit)
    {
        if (hintIcon == null) return;

        bool hasArt = hintReadySprite != null && hintUsedSprite != null;

        if (!hasArt)
        {
            hintIcon.color = lit ? hintReady : hintUsed;
            return;
        }

        Sprite want = lit ? hintReadySprite : hintUsedSprite;
        if (hintIcon.sprite != want) hintIcon.sprite = want;

        // ⚠ 그림을 쓸 때는 색을 흰색으로 고정한다. 어두운 색이 남아 있으면
        //   흑백판이 한 번 더 어두워져 거의 안 보인다.
        if (hintIcon.color != Color.white) hintIcon.color = Color.white;
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

        DrawCountdown(NetworkCountdown);

        DrawCenterNotice();

        // 목표를 보여 주는 7초 동안만 뜨는 안내. 그 시간은 위 타이머가 센다.
        SetActive(memorizeTitlePanel, NetworkMemorizeShow);

        // 판이 끝나면 화면이 **둘**이다. 제목 먼저(2초), 그 다음 성적표(6초),
        // 그러고 나서야 공용 결과 화면이 열린다. (MineMatchState.ShowResultAfterHold)
        //
        // ⚠ 둘을 같이 켜면 "채굴 종료" 위에 점수가 겹친다. 서버가 한 번에 하나만
        //   참으로 보내지만, 여기서도 서로 모르는 칸으로 나눠 둔다.
        SetActive(finishTitlePanel, NetworkFinishTitleShow && !hideOnFinish);

        // hideOnFinish 를 켜면 두 칸을 다 감춘다. 광산 전용 결과를 아예 건너뛰고
        // 공용 결과 화면만 쓰는 씬을 위한 스위치다.
        SetActive(resultPanel, NetworkResultShow && !hideOnFinish);

        if (NetworkResultShow)
        {
            DrawResultCard(NetworkResultSuccess, NetworkResultScore,
                           NetworkResultTargetCount, NetworkResultDugCount);
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
        label.color = CenterNoticeLabelColor;
        label.raycastTarget = false;

        // ⚠ **자동 크기로 둔다.** 한마디가 늘 짧지는 않다. "힌트타임" 은 90pt 로 크게
        //   나오지만 "동료를 기다리는 중 (1 / 2)" 는 그 크기로 화면을 넘친다.
        //   좌우 여백은 글자가 화면 끝에 닿지 않게 하려는 것이다.
        label.enableAutoSizing = true;
        label.fontSizeMin = 32f;
        label.fontSizeMax = CenterNoticeFontSize;
        label.fontSize = CenterNoticeFontSize;
        label.margin = new Vector4(80f, 0f, 80f, 0f);

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
        DrawCountdown(game.State == MineState.Countdown
            ? Mathf.Max(1, Mathf.CeilToInt(game.TimeLeft))
            : 0);

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

    // 판이 끝났을 때만 가운데에 크게 띄운다. 솔로 쪽 입구다.
    //
    // 점수는 MineGame.EnterFinished 와 **같은 식**으로 다시 낸다. MineGame 은 그 값을
    // Finished 이벤트로만 흘려보내고 속성으로 들고 있지 않아서다. 식이 하나라도
    // 어긋나면 같은 판이 솔로와 네트워크에서 다른 점수로 보인다.
    private void RefreshResult()
    {
        bool done = game.State == MineState.Finished;

        if (resultPanel != null) resultPanel.SetActive(done);
        if (!done) return;

        DrawResultCard(
            game.Success,
            Mathf.Clamp(Mathf.RoundToInt(game.Result.Percent), 0, 100),
            game.Result.TargetCount,
            game.Result.DugCount);
    }

    /// <summary>
    /// 성적표 한 장을 채운다. <b>솔로와 네트워크가 같이 쓴다.</b>
    ///
    /// 안 바뀌는 글자는 전부 <b>그림에 박혀 있다</b> — 제목("성공!"/"실패!") ·
    /// "도안 유사도" · "목표" · "채굴". 여기서 넣는 것은 바뀌는 값 넷뿐이다.
    ///
    /// ⚠ <b>점수와 유사도는 같은 값이다.</b> 반올림만 다르다 (MINE.md 2장).
    ///   그래서 "87점 · 유사도 87.5%" 처럼 두 번 적지 않는다. 점수 하나만 크게 띄우고,
    ///   그것이 무엇인지는 그림에 박힌 "도안 유사도" 가 말한다.
    ///
    /// ⚠ <b>목표·채굴은 맞힌 칸 수가 아니다.</b> 목표는 도안 전체 칸 수, 채굴은 실제로
    ///   판 전체 칸 수다. 둘이 같아도 그림이 맞다는 뜻이 아니다.
    /// </summary>
    private void DrawResultCard(bool success, int score, int targetCount, int dugCount)
    {
        if (resultPanelImage != null)
        {
            Sprite want = success ? resultSuccessSprite : resultFailSprite;

            // 그림을 안 붙인 씬에서는 건드리지 않는다. 지우면 판이 통째로 사라진다.
            if (want != null && resultPanelImage.sprite != want) resultPanelImage.sprite = want;
        }

        // ⚠ 점수는 **언제나 흰색**이다. 성공·실패로 물들이지 않는다.
        //   제목("성공!"/"실패!")이 이미 판 그림에 크게 박혀 있어서, 점수까지 색을 바꾸면
        //   같은 말을 두 번 하는 셈이고 금속·주황 판 위에서 색만 겉돈다.
        if (resultScoreText != null) resultScoreText.text = score + "점";

        if (resultCommentText != null) resultCommentText.text = CommentFor(score);
        if (resultTargetText != null) resultTargetText.text = targetCount + "칸";
        if (resultDugText != null) resultDugText.text = dugCount + "칸";
    }

    /// <summary>
    /// 점수 구간별 한 줄 평. <b>AI 가 아니라 표다.</b>
    ///
    /// MINE.md 7장은 한 줄 평을 AI 몫으로 뒀지만, 표로 둔 이유는 그 장이 판정과 AI 를
    /// 가른 이유와 같다 — 같은 점수면 늘 같은 말이 나오고, 왕복을 기다리지 않고,
    /// 오프라인에서도 된다. 승패도 점수도 AI 가 건드리지 않는 자리다.
    ///
    /// ⚠ <b>성공선이 70 이라는 것이 이 표에 박혀 있다.</b> 70 부터가 성공 말투이고
    ///   60~69 는 아쉬워하는 말투다. 씬의 <c>successThreshold</c> 를 바꾸면
    ///   <b>"실패!" 제목 밑에 성공 문구가 뜬다.</b> 둘은 같이 움직여야 한다.
    ///   (MINE.md 12장)
    /// </summary>
    private static string CommentFor(int score)
    {
        if (score >= 90) return "광부들, 혹시 도안 몰래 보고 온 거 아니죠?";
        if (score >= 80) return "손발이 척척! 이 정도면 거의 곡괭이 합주단!";
        if (score >= 70) return "곡괭이가 살짝 자유로웠지만 팀워크로 성공!";
        if (score >= 60) return "호흡은 나쁘지 않았지만 마무리가 아쉬웠어요!";
        if (score >= 40) return "팀워크는 있었는데 정답이 눈치가 없었어요!";

        return "팀워크보다 창의력이 너무 앞서갔습니다!";
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

            // 네트워크와 같은 규칙이다 — 끝났으면 전부 완료. (DrawNetworkPlayers 주석)
            bool done = game.State == MineState.Finished || turn < game.TurnNumber;

            row.stateText.text = digging ? "채굴 중" : done ? "채굴 완료" : "대기 중";
            row.stateText.color = digging ? activeLabel : done ? doneLabel : waitLabel;

            // 솔로에는 "내 줄" 이 없다. 혼자 다 도는 씬이라 전부 내 줄이다.
            DrawRowFrame(row.frame, digging, false);
        }
    }

    private void RefreshRestore()
    {
        if (game == null) return;

        if (restoreText != null)
            restoreText.text = game.RestoresLeft + " / " + game.TotalRestores;

        DrawRestoreArt(game.RestoresLeft > 0);
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

        DrawHintArt(lit);
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

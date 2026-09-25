using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors
{
    /// <summary>
    /// Canvas/TMP view for WarriorsTest. Gameplay state remains owned by the
    /// existing score, flow, health, combo and rhythm components.
    /// </summary>
    public sealed class WarriorsHudPresenter : MonoBehaviour
    {
        [Header("Gameplay sources")]
        [SerializeField] private WarriorsBattleScore score;
        [SerializeField] private WarriorsGameFlow flow;
        [SerializeField] private WarriorsHealth playerHealth;
        [SerializeField] private WarriorsComboSystem combo;
        [SerializeField] private WarriorsRhythmBattle rhythm;
        [SerializeField] private WarriorsCombatHud combatHud;
        [SerializeField] private MonoBehaviour attackInputSource;   // WarriorsInputRouter
        [SerializeField] private GameObject rhythmJudgementChip;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private UnityEngine.UI.Image resultRule;
        [SerializeField] private TMP_Text rewardNameText;
        [SerializeField] private TMP_Text rewardHeadingText;
        [SerializeField] private UnityEngine.UI.Image rewardGem;

        [Header("Top HUD")]
        /// <summary>
        /// 네트워크 매치 안내. 비어 있으면 예전처럼 라운드 이름이 뜬다.
        ///
        /// "두 명을 기다리는 중" . "3초 뒤 시작" 처럼 전투 밖의 상태를 보여주는 데 쓴다.
        /// 혼자 하는 씬에서는 아무도 넣지 않으므로 하나도 안 바뀐다.
        /// </summary>
        public string MatchNotice { get; set; }

        /// <summary>매치 안내의 둘째 줄. 시간 자리에 뜬다. (남은 목숨 등)</summary>
        public string MatchDetail { get; set; }

        /// <summary>
        /// 네트워크 3페이즈가 돌고 있는가. 서버가 준 노트로 리듬 화면을 그린다.
        ///
        /// 혼자 하는 씬에서는 아무도 켜지 않으므로 예전 경로(<c>WarriorsRhythmBattle</c>)
        /// 그대로다. 둘 중 하나만 켜진다.
        /// </summary>
        public bool NetworkRhythmActive { get; set; }

        /// <summary>서버가 준 노트. 레인 번호가 그대로 사람 번호다.</summary>
        public readonly List<WarriorsRhythmNoteView> NetworkRhythmNotes = new();

        /// <summary>남은 목표를 0~1 로. 크라켄 막대 자리에 그린다.</summary>
        public float NetworkRhythmProgress { get; set; }

        /// <summary>"최후의 일격 3 / 15" 같은 한 줄.</summary>
        public string NetworkRhythmDetail { get; set; }

        /// <summary>"1P 목숨 3   2P DOWN". 3분 시계 자리에 대신 들어간다.</summary>
        public string NetworkRhythmLives { get; set; }

        /// <summary>네트워크 3페이즈의 판정 문구("COMBO FINISH!" 등). 비어 있으면 아무것도 뜨지 않는다.</summary>
        public string NetworkRhythmJudgement { get; set; }

        /// <summary>
        /// 내 레인 번호. -1 이면 모르는 것(혼자 하는 씬 · 서버). 알면 남의 레인 노트를 흐리게 그려
        /// 두 사람 노트가 나란히 떨어져도 내 것이 한눈에 들어온다.
        /// </summary>
        public int NetworkLocalLane { get; set; } = -1;

        /// <summary>
        /// 지금 판정선을 번쩍여야 하는 레인. -1 이면 아무 줄도 반응하지 않는다.
        /// <c>WarriorsPhase3Director</c> 가 정타를 본 동안(0.08~0.12초)만 채워 준다.
        /// </summary>
        public int NetworkRhythmPulseLane { get; set; } = -1;

        /// <summary>협동 게이지(0~1). 두 사람이 번갈아 맞힐 때만 찬다.</summary>

        /// <summary>지금 팀 강화 중인가. 게이지 막대가 금색으로 바뀐다.</summary>

        /// <summary>구조 안내 문구. 비어 있으면 띄우지 않는다.</summary>
        public string NetworkDownNotice { get; set; }

        /// <summary>협동 신호 문구("지금! 이어서 베세요" 등). 비어 있으면 띄우지 않는다.</summary>
        public string NetworkFinishWindowNotice { get; set; }

        /// <summary>
        /// 라운드 전환이 시작되고 흐른 시간(초). -1 이면 전환 중이 아니다.
        ///
        /// 서버가 재는 값 하나다. 종료 문구와 소개가 <b>이 값으로 갈리므로</b>
        /// 둘이 같은 순간에 켜질 수 없다.
        /// </summary>
        public float NetworkTransitionElapsed { get; set; } = -1f;

        /// <summary>쓰러진 레인 비트 묶음. 0번 자리가 1P. 그 줄은 트랙을 그리지 않는다.</summary>
        public int NetworkDownLanes { get; set; }

        /// <summary>종료 문구를 보여 주는 구간의 끝(초).</summary>
        public float NetworkClearSeconds { get; set; } = 1.1f;

        /// <summary>소개가 시작되는 시각(초). 이 사이는 빈 화면이다.</summary>
        public float NetworkIntroStartSeconds { get; set; } = 1.5f;

        /// <summary>
        /// 화면이 어두워지기 시작하는 시각(초). 음수면 종료 문구가 끝난 때(<see cref="NetworkClearSeconds"/>)부터.
        /// 판 마지막에는 크라켄이 다 가라앉은 뒤부터 어두워져야 가라앉는 장면이 가려지지 않는다.
        /// </summary>
        public float NetworkFadeStartSeconds { get; set; } = -1f;

        /// <summary>
        /// 방금 깬 라운드 번호(1~3). 0 이면 지금 깬 라운드가 없다.
        ///
        /// 종료 문구는 이 값으로 고른다. 라운드 번호가 오르는 것으로 판단하면 무대가 이미
        /// 바뀐 뒤라 문구가 다음 라운드 배경 위에서 뜬다.
        /// </summary>
        public int NetworkClearedRound { get; set; }

        /// <summary>지금 종료 문구를 띄울 구간인가.</summary>
        private bool InClearWindow =>
            NetworkTransitionElapsed >= 0f && NetworkTransitionElapsed < NetworkClearSeconds;

        /// <summary>지금 소개를 띄울 구간인가.</summary>
        private bool InIntroWindow =>
            NetworkTransitionElapsed >= NetworkIntroStartSeconds;

        /// <summary>결과 화면용 — 동시 공격 성공 횟수.</summary>

        /// <summary>결과 화면용 — 팀 강화 발동 횟수.</summary>

        // 구조 성공 횟수(NetworkRescueCount)는 삭제했다. 구조·부활이 규칙에서 빠졌다.

        /// <summary>
        /// 네트워크 매치가 이 HUD 를 몰고 있는가. (Warriors 네트워크 전환)
        ///
        /// 켜지면 라운드 · 목표 · 점수 · 결과를 아래 Network* 값에서 읽는다. 원래 읽던
        /// <c>WarriorsBattleScore</c> · <c>WarriorsGameFlow</c> 는 네트워크에서 꺼져 있어
        /// 처치 수가 0 / 30 에 멈춰 있고 결과 화면이 절대 뜨지 않았다.
        /// 혼자 하는 씬에서는 아무도 켜지 않으므로 예전 그대로다.
        /// </summary>
        public bool NetworkMatchActive { get; set; }

        /// <summary>지금 라운드(1~3). 0 은 대기 · 카운트다운.</summary>
        public int NetworkRound { get; set; }

        /// <summary>도달한 가장 높은 라운드. 결과 화면의 "도달 라운드".</summary>
        public int NetworkReachedRound { get; set; }

        /// <summary>상단 가운데 막대의 글. "처치 수 3 / 10".</summary>
        public string NetworkObjective { get; set; }

        /// <summary>상단 가운데 막대의 채움(0~1).</summary>
        public float NetworkObjectiveProgress { get; set; }

        public int NetworkScore { get; set; }

        /// <summary>
        /// 내 연속 처치 수. <c>WarriorsLocalView</c> 가 내 캐릭터의 복제 값을 넣어 준다.
        ///
        /// 싱글 씬의 <c>WarriorsComboSystem</c> 은 네트워크에서 서버에만 붙어 있어
        /// 클라이언트에서는 늘 0 이었다. 그래서 점수·처치처럼 복제 값을 따로 받는다.
        /// </summary>
        public int NetworkCombo { get; set; }

        /// <summary>1페이즈 처치 수. 결과 화면의 "몬스터 처치".</summary>
        public int NetworkKills { get; set; }

        /// <summary>1페이즈 시작부터 흐른 시간. 결과 화면의 "플레이 시간".</summary>
        public float NetworkElapsedSeconds { get; set; }

        /// <summary>0 진행 중 · 1 클리어 · 2 실패. 1 · 2 면 결과 화면을 띄운다.</summary>
        public int NetworkFinal { get; set; }

        /// <summary>실패 문구. "GAME OVER" 또는 "TIME OVER".</summary>
        public string NetworkFailureLabel { get; set; }

        /// <summary>
        /// **내 캐릭터의 HP 를 이 HUD 에 붙인다.** 네트워크에서 <c>WarriorsPlayerLife</c> 가 부른다.
        ///
        /// 원래는 처음 찾은 <c>WarriorsLocalPlayerController</c> 의 HP 를 읽었는데,
        /// 두 사람이 있으면 그것이 상대 캐릭터일 수 있다.
        /// </summary>
        public void BindLocalHealth(WarriorsHealth health)
        {
            if (health != null) playerHealth = health;
        }

        [SerializeField] private GameObject standardHud;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text phaseEyebrowText;
        [SerializeField] private TMP_Text phaseValueText;
        [SerializeField] private Image phaseFill;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private GameObject playerStatusRoot;
        [SerializeField] private TMP_Text[] playerStateTexts;
        [SerializeField] private Image[] playerStateFills;
        [SerializeField] private GameObject objectiveRoot;      // standing goal under the TIME card
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private CanvasGroup objectiveGroup;

        [Header("Attack feedback")]
        [SerializeField] private RectTransform[] monsterGuideCards;   // fish / crab / jellyfish
        [SerializeField] private RectTransform[] attackGuideCards;    // 1 / 2 / 3
        [SerializeField] private GameObject monsterGuideRoot;   // ROUND 1: which monster needs which attack
        [SerializeField] private GameObject attackGuideRoot;    // ROUND 2/3: the three attacks only

        [Header("Round intro")]
        [SerializeField] private GameObject roundIntroRoot;
        [SerializeField] private TMP_Text roundIntroBadge;
        [SerializeField] private RectTransform roundIntroCard;
        [SerializeField] private TMP_Text roundIntroTitle;
        [SerializeField] private TMP_Text roundIntroBody;
        [SerializeField] private CanvasGroup roundIntroGroup;
        [SerializeField, Min(.5f)] private float roundIntroSeconds = 3f;
        [SerializeField] private bool holdGameplayDuringIntro = true;

        [Header("Feedback")]
        [SerializeField] private GameObject announcementRoot;
        [SerializeField] private TMP_Text announcementText;
        [SerializeField] private TMP_Text specialText;
        [SerializeField] private TMP_Text actionText;

        [Header("Rhythm HUD")]
        [SerializeField] private GameObject rhythmRoot;
        [SerializeField] private TMP_Text rhythmRoundText;
        [SerializeField] private TMP_Text rhythmTimeText;
        [SerializeField] private TMP_Text rhythmHpText;
        [SerializeField] private Image rhythmHpFill;
        [SerializeField] private TMP_Text rhythmBossText;
        [SerializeField] private Image rhythmBossFill;
        [SerializeField] private TMP_Text rhythmScoreText;
        [SerializeField] private TMP_Text rhythmComboText;
        [SerializeField] private RectTransform rhythmHitLine;
        [SerializeField] private TMP_Text rhythmJudgementText;
        [SerializeField] private RectTransform[] rhythmNotes;
        [SerializeField] private TMP_Text[] rhythmNoteGlyphs;
        [SerializeField] private Color rhythmNoteColor = new(.043f, .09f, .18f, .92f);
        [SerializeField] private Color rhythmSuccessfulColor = new(.2f, .9f, .48f, 1f);
        [SerializeField] private Color rhythmMissedColor = new(.32f, .12f, .12f, .85f);

        [Header("Rhythm note colours by attack — same hues as the monster cards")]
        // **바다 배경 위에서 순간적으로 갈려야 한다.** 이전 값(.2/.55/.95 등, 알파 .95)은
        // 링이 얇아서 색이 화면에서 거의 사라졌다. 셋 다 채도를 최대로 올리고 알파를 1 로 둔다.
        // 색을 갖는 것은 **링과 기호뿐이고 가운데는 여전히 알파 0** 이다(RingSprite).
        [SerializeField] private Color rhythmHorizontalColor = new(.11f, .62f, 1f, 1f);      // 물고기 · 가로베기 · 파랑
        [SerializeField] private Color rhythmVerticalColor = new(1f, .22f, .22f, 1f);        // 게 · 세로베기 · 빨강
        // 찌르기는 **노랑**이다. 보라로 두었더니 판정선(청록)·가로베기(파랑)와 한 계열로 뭉쳐
        // 세 종류가 한눈에 갈리지 않았다. 파랑 · 빨강 · 노랑이 서로 가장 멀다.
        [SerializeField] private Color rhythmThrustColor = new(1f, .86f, .08f, 1f);          // 해파리 · 찌르기 · 노랑

        [Header("Final overlay")]
        [SerializeField] private GameObject finalRoot;
        [SerializeField] private TMP_Text finalEyebrowText;
        [SerializeField] private TMP_Text finalTitleText;
        [SerializeField] private TMP_Text finalDetailText;
        [SerializeField] private GameObject retryButtonRoot;

        private readonly List<WarriorsRhythmNoteView> visibleNotes = new();

        /// <summary>
        /// 한 사람 몫의 리듬 레인 화면 조각. 런타임에 만든다.
        ///
        /// 바닥(<see cref="fill"/>)은 거의 투명하고, 실제로 "길" 로 읽히게 하는 것은
        /// 양쪽 가장자리 선과 레인마다 따로 있는 판정선이다.
        /// </summary>
        private sealed class LaneVisual
        {
            public RectTransform fill;
            public RectTransform leftEdge;
            public RectTransform rightEdge;
            public RectTransform judge;

            /// <summary>판정선 뒤에 까는 글로우. 선보다 넓고 위아래로 흐려진다.</summary>
            public RectTransform judgeGlow;

            /// <summary>판정선 한가운데의 밝은 심지(4px). 납작한 색 막대가 "선" 으로 읽히게 한다.</summary>
            public RectTransform core;

            /// <summary>가로 · 세로 · 찌르기 열을 나누는 세로 안내선. 공격 색을 옅게 깐다.</summary>
            public RectTransform[] columns;

            /// <summary>판정선 위의 "1P" · "2P" 표. 어느 줄이 누구 것인지 바로 읽히게 한다.</summary>
            public TMP_Text label;
        }

        // ------------------------------------------------------------
        // 3라운드 리듬 화면의 자리 — **전부 1920x1080 기준으로 계산한 값이다.**
        //
        // 좌표계: 노트의 부모 NoteTrack 은 화면을 채우는 RhythmHUD 의 한가운데에서
        // (0, 40) 만큼 올라가 있다. 그래서 여기 적는 로컬 y 에 40 을 더하면 화면 중앙 기준 y 가 된다.
        //
        //   화면 위쪽 HUD(라운드 · 보스 HP · 점수)  화면중앙 y > +380
        //   크라켄 얼굴                             +150 ~ +450
        //   캐릭터 머리                             약 -230
        //   아래쪽 공격 가이드                      y < -400
        //
        // 노트가 지나갈 수 있는 세로 구간은 그 사이다.
        // ------------------------------------------------------------

        // 프리팹에서 잰 HUD 가 차지하는 칸 (화면 중앙 원점, y 위쪽 +)
        //
        //   RoundCard(좌상)   x -920..-600   y 384..512
        //   BossCard(중상)    x -230..+230   y 428..512   ← 아래에서 460x84 로 줄인다
        //   ScoreCard(우상)   x +600..+920   y 384..512
        //   AttackGuide(하단) x -415..+415   y -506..-430
        //
        // 노트가 지나갈 수 있는 칸은 y -430 ~ +384 사이다.

        /// <summary>
        /// 노트가 생기는 높이(NoteTrack 로컬). 화면 중앙 기준 +290.
        ///
        /// 예전에는 +360 으로 상단 카드(384) 바로 밑에 붙여 뒀다. 트랙이 화면 위에서 아래까지
        /// 꽉 뻗었는데 그 안에 노트가 없는 시간이 길어, <b>UI 가 자리만 차지한다</b>는 인상이었다.
        /// 70px 내려 트랙을 짧게 하면 같은 낙하 시간에 빈 구간이 줄고 카드와도 떨어진다.
        /// 낙하 거리 540px / 2초 = 270px/s 로 예전(277px/s)과 체감 속도는 거의 같다.
        /// </summary>
        private const float NoteSpawnLocalY = 250f;

        /// <summary>판정선 높이(NoteTrack 로컬). 화면 중앙 기준 -250 — 하단 공격 가이드(-430) 위.</summary>
        private const float JudgeLocalY = -290f;

        /// <summary>
        /// 판정선에서의 입력 열 간격.
        ///
        /// 노트 지름이 90 이고 "지름 = 열 폭의 65%" 로 잡으면 90 / .65 = 138 이다.
        /// 이보다 좁으면 두 노트가 나란히 떨어질 때 어느 열인지 읽히지 않는다.
        /// </summary>
        // 138 은 레인 폭 414 — 화면에서 "큰 사다리꼴 도형" 으로 읽혔다(실측). 120 이면 폭 360,
        // 노트(90)가 열의 75% 라 옆 노트와 30px 이 뜬다. 좁은 세로 레인으로 읽히는 쪽을 택한다.
        private const float ColumnPitch = 120f;

        /// <summary>판정선에서의 트랙 반폭. 열 3개 = 414, 그 절반.</summary>
        private const float TrackHalfWidthBottom = ColumnPitch * 1.5f;

        /// <summary>
        /// 트랙 맨 위의 폭은 아래의 몇 배인가. 리듬게임 하이웨이처럼 위가 좁고 아래가 넓다.
        ///
        /// 직사각형이면 "투명한 상자" 로 보이고, 이 기울기가 있어야 노트가 <b>다가온다</b>고 읽힌다.
        /// </summary>
        // .60 은 위가 너무 좁아 삼각형에 가까웠다(실측 "베기 이펙트 같다"). .82 면 원근 힌트는
        // 남기면서 세로 레인으로 읽힌다.
        // .82 — 위가 살짝 좁은 사다리꼴. 바닥은 <see cref="TrapezoidSprite"/> 로 같은 비율의
        // 사다리꼴을 그려 가장자리 선과 정확히 겹친다 (사각형 Image 를 깔았을 때 레인 안에
        // 네모가 따로 보였던 문제의 해법).
        private const float TrackTopScale = .82f;

        /// <summary>
        /// 두 트랙의 중심. **화면 기준 고정값이다.**
        ///
        /// ⚠ 캐릭터의 월드 자리를 화면으로 투영해 따라가게 했더니, 두 클라이언트의 카메라 구도가
        ///    조금 달라 같은 판인데도 화면마다 레인 위치가 달랐다. 리듬 UI 는 화면에 고정한다.
        ///    누구 줄인지는 아래 1P · 2P 표로 알린다.
        ///
        /// ±300 이면 아래쪽이 ±93..±507 이라 가운데 186 이 남고, 위쪽은 ±176..±424 라 352 가 남는다.
        /// 크라켄 얼굴은 위쪽에 있으므로 가려지지 않는다.
        /// </summary>
        private const float TrackCentreX = 300f;

        /// <summary>사람마다 하나. <see cref="UpdateLaneTracks"/> 가 만들고 관리한다.</summary>
        private readonly List<LaneVisual> laneVisuals = new();

        /// <summary>마지막으로 본 라운드 번호. 이 값이 오르면 앞 라운드를 깬 것이다.</summary>
        private int shownRound;

        /// <summary>라운드를 깬 문구와, 그것을 언제까지 보여 줄지.</summary>
        private string clearNotice = string.Empty;

        private float clearNoticeUntil;

        /// <summary>그 라운드를 깼을 때의 한 문장.</summary>
        private static string ClearedLine(int round) => round switch
        {
            1 => "해변 방어 성공!",
            2 => "크라켄의 공격을 막아냈습니다!",
            3 => "크라켄 공격 성공!",
            _ => string.Empty,
        };

        /// <summary>
        /// 각 레인의 가로 중심(노트가 놓이는 좌표계 기준).
        ///
        /// 레인 배경과 노트가 **같은 값**을 써야 노트가 트랙 밖으로 새지 않는다.
        /// <see cref="UpdateLaneTracks"/> 가 채우고 노트 배치가 읽는다.
        /// </summary>
        private readonly List<float> laneCentres = new();
        private int connectedPlayers = 1;
        private float nextPlayerScanTime;
        private WarriorsBattlePhase introPhase = (WarriorsBattlePhase)int.MinValue;
        private float introUntil;
        private bool introHoldsTime;
        private string objectiveLabel = string.Empty;
        private IWarriorsInputSource attackInput;
        private WarriorsAttackDirection lastAttack = WarriorsAttackDirection.None;
        private float lastAttackUntil;
        private UnityEngine.UI.Image[] monsterCardFills, attackCardFills;
        private Color[] monsterCardBase, attackCardBase;
        private Image[] rhythmNoteFills;

        /// <summary>베인 노트가 터질 때 날아가는 파편. 노트 한 칸에 <see cref="ShardsPerNote"/> 개.</summary>
        private RectTransform[][] noteShards;

        /// <summary>파편 개수. 6 이면 찌르기(사방)가 60도 간격으로 고르게 퍼진다.</summary>
        private const int ShardsPerNote = 6;

        /// <summary>파편 하나의 크기(px). 노트가 72~117px 이므로 그 1/6 쯤 되는 부스러기다.</summary>
        private const float ShardPixels = 16f;

        /// <summary>파편이 날아가는 거리(px). 노트 지름(약 90px)보다 조금 크게 흩어진다.</summary>
        private const float ShardFlyPixels = 62f;

        /// <summary>노트 링 바깥의 글로우.</summary>
        private RectTransform[] noteGlows;

        /// <summary>글로우 판의 크기(px). 노트 프리팹이 90px 이므로 그보다 크게 잡아 바깥으로 번진다.</summary>
        private const float NoteGlowPixels = 90f;

        /// <summary>글로우를 노트보다 얼마나 크게 그릴지. 1.0 이면 링과 같은 크기다.</summary>
        private const float NoteGlowScale = 1.35f;

        /// <summary>
        /// 평소 글로우 세기.
        ///
        /// 너무 올리면 링이 두꺼워 보이고 기호가 묻힌다 — 링을 7px 로 얇게 유지하는 이유가
        /// 사라진다. "밝은 배경에서 사라지지 않을 만큼" 만 준다.
        /// </summary>
        // .55 는 실측 스크린샷(3라운드)에서 밝은 하늘·바다 위에 노트가 거의 묻혔다. 올린다.
        // 잔상을 뺀 대신 글로우는 "또 하나의 링" 으로 안 읽히게 은은하게 둔다.
        private const float NoteGlowAlpha = .45f;

        /// <summary>노트가 남기는 자국. 뒤(위쪽)로 두 토막.</summary>
        private RectTransform[][] noteTrails;

        /// <summary>자국 토막 수. 2 면 "짧게 흐른다" 가 되고, 더 늘리면 꼬리가 된다.</summary>
        private const int TrailPieces = 2;

        /// <summary>토막 사이 간격(px). 낙하 거리 540px 의 약 5% · 10% 지점에 남는다.</summary>
        private const float TrailStepPixels = 27f;

        /// <summary>첫 토막의 세기. 노트보다 확실히 옅어야 노트가 먼저 읽힌다.</summary>
        private const float TrailAlpha = .3f;

        private void Awake()
        {
            ResolveSources();
            DisableLegacyHud();
        }

        private void OnEnable()
        {
            ResolveSources();
            DisableLegacyHud();
            introPhase = (WarriorsBattlePhase)int.MinValue;
            introUntil = 0f;

            // ⚠ 네트워크 판에서는 attackInputSource(IWarriorsInputSource)가 비어 있어 카드 펄스가
            //    한 번도 안 났다(프리팹 참조 {fileID: 0}). 네트워크 입력은 WarriorsInputProvider 가
            //    모으므로 그쪽의 "방금 휘둘렀다" 신호를 같은 펄스 처리로 잇는다.
            Warriors.Net.WarriorsInputProvider.LocalSwing += HandleLocalSwing;
        }

        private void HandleLocalSwing(WarriorsAttackDirection direction) => HandleAttackForPulse(direction, 1f);

        private void OnDisable()
        {
            ReleaseIntroHold();
            Warriors.Net.WarriorsInputProvider.LocalSwing -= HandleLocalSwing;
            if (attackInput != null) { attackInput.AttackRequested -= HandleAttackForPulse; attackInput = null; }
            score?.SetLegacyHudVisible(true);
            rhythm?.SetLegacyHudVisible(true);
            combatHud?.SetLegacyHudVisible(true);
        }

        private void ResolveSources()
        {
            if (score == null) score = FindFirstObjectByType<WarriorsBattleScore>(FindObjectsInactive.Include);
            if (flow == null) flow = FindFirstObjectByType<WarriorsGameFlow>(FindObjectsInactive.Include);
            if (playerHealth == null)
            {
                WarriorsLocalPlayerController player = FindFirstObjectByType<WarriorsLocalPlayerController>(FindObjectsInactive.Include);
                if (player != null) playerHealth = player.GetComponent<WarriorsHealth>();
            }
            if (combo == null) combo = FindFirstObjectByType<WarriorsComboSystem>(FindObjectsInactive.Include);
            if (rhythm == null) rhythm = FindFirstObjectByType<WarriorsRhythmBattle>(FindObjectsInactive.Include);
            if (combatHud == null) combatHud = FindFirstObjectByType<WarriorsCombatHud>(FindObjectsInactive.Include);
            if (attackInputSource == null) attackInputSource = FindFirstObjectByType<WarriorsInputRouter>(FindObjectsInactive.Include);
            BindAttackInput();
        }

        /// <summary>
        /// The card pulse listens to the shared input router, which lives in the game root
        /// prefab and is therefore present in every Warriors scene. Reading the swing off a
        /// HUD component instead would have tied the feedback to one dev scene.
        /// </summary>
        private void BindAttackInput()
        {
            var source = attackInputSource as IWarriorsInputSource;
            if (ReferenceEquals(source, attackInput)) return;
            if (attackInput != null) attackInput.AttackRequested -= HandleAttackForPulse;
            attackInput = source;
            if (attackInput != null) attackInput.AttackRequested += HandleAttackForPulse;
        }

        private void HandleAttackForPulse(WarriorsAttackDirection direction, float strength)
        {
            lastAttack = direction;
            lastAttackUntil = Time.unscaledTime + .3f;
        }

        private void DisableLegacyHud()
        {
            score?.SetLegacyHudVisible(false);
            rhythm?.SetLegacyHudVisible(false);
            combatHud?.SetLegacyHudVisible(false);
        }

        private void LateUpdate()
        {
            if (score == null || flow == null)
            {
                ResolveSources();
                DisableLegacyHud();
                if (score == null || flow == null) return;
            }

            // 네트워크에서는 WarriorsGameFlow 가 꺼져 있어 Phase 가 움직이지 않는다.
            // 서버가 켜 준 값으로 같은 화면을 띄운다.
            bool isFinalOverlay = NetworkMatchActive
                ? NetworkFinal != 0
                : flow.Phase == WarriorsBattlePhase.FinalSwingPhase ||
                  flow.Phase == WarriorsBattlePhase.Clear ||
                  flow.Phase == WarriorsBattlePhase.Failed;
            // 결과가 나면 리듬 화면보다 결과 화면이 먼저다.
            bool isRhythm = !isFinalOverlay &&
                            (NetworkRhythmActive ||
                             (flow.Phase == WarriorsBattlePhase.FinalKrakenPhase && rhythm != null && rhythm.IsActive));

            SetActive(standardHud, !isRhythm && !isFinalOverlay);
            // The player strip lives outside StandardHUD so it survives the rhythm round,
            // but the result screen should be clean.
            // **2인 프로필 칸을 다시 켠다.** 좌하단, 두 칸.
            //
            // 한때 꺼 두었다. 그때는 "1P 적중 12   2P 적중 8" 이라는 글자 줄이었고, 적중 수는
            // 전투 중 아무 판단에도 쓰이지 않아 화면만 한 층 더 채웠기 때문이다.
            //
            // 지금은 얼굴 · 이름 · HP 가 들어간 프로필 칸이라 성격이 다르다. 2인 협동에서
            // <b>상대가 살아 있는지</b>는 계속 봐야 하는 정보다.
            //
            // ⚠ **혼자일 때는 통째로 숨긴다.** 빈 2P 칸이나 "대기 중" 은 보여 주지 않는다.
            //    혼자 하는 사람에게 자기 얼굴 하나만 띄우는 칸도 필요 없다 — 자기 HP 는
            //    이미 오른쪽 위 칸에 크게 있다.
            //
            // ⚠ 결과 화면에서도 끈다. 그때 볼 것은 결과 판 하나뿐이다.
            SetActive(playerStatusRoot, !isFinalOverlay && ResolveConnectedPlayers() >= 2);
            SetActive(rhythmRoot, isRhythm);

            // ⚠ **공용 결과 판이 씬에 있으면 옛 결과 카드를 띄우지 않는다.**
            //
            //    세 게임이 같은 결과 판(MiniGameResultOverlay)을 쓰기로 했다. 이 카드는 그보다
            //    앞서 만든 것이라 둘 다 띄우면 같은 내용이 두 번 겹친다.
            //
            //    ⚠ 예전에는 <c>!MiniGameTransition.InMiniGame</c> 으로 갈랐다. "포탈로 들어온
            //      판에만 공용 판이 있다" 는 <b>가정</b>이었는데 그것이 틀렸다. 공용 판은
            //      <c>MiniGameResultOverlaySetup</c> 이 미니게임 씬에 미리 깔아 두므로,
            //      포탈을 거치지 않는 단독 실행에도 그대로 있다. 그래서 GAME OVER 가
            //      두 겹으로 겹쳐 보였다.
            //
            //      가정 대신 <b>실제로 있는지</b>를 본다. 이러면 어느 경로로 들어와도 맞는다.
            SetActive(finalRoot, isFinalOverlay && !SharedResultPanelExists());

            // ROUND 1 teaches the monster -> attack mapping; Fish/Crab/Jellyfish do not
            // appear from ROUND 2 on, so only the three attacks stay on screen there.
            bool monsterRound = NetworkMatchActive
                ? NetworkRound <= 1
                : flow.Phase == WarriorsBattlePhase.NormalBattle;
            SetActive(monsterGuideRoot, monsterRound && !isFinalOverlay);
            SetActive(attackGuideRoot, !monsterRound && !isFinalOverlay);

            // 3라운드에서 하단 카드는 보조 정보다. 레인·노트·판정선에 시선을 양보하게 15% 줄인다.
            // (1·2라운드 카드와 같은 비중이면 시선이 아래로 흩어졌다 — 실측 지적)
            if (attackGuideRoot != null)
            {
                attackGuideRoot.transform.localScale = isRhythm ? Vector3.one * .85f : Vector3.one;
            }

            CloseSharedResultOnRestart(isFinalOverlay);

            TrackReachedRound();
            UpdateRoundIntro(isFinalOverlay);
            UpdateGuidePulse();
            UpdateNoticeHud(isFinalOverlay);
            UpdateSharedFeedback(isFinalOverlay);
            UpdateTransitionFade();
            if (isRhythm) UpdateRhythmHud();
            else if (isFinalOverlay) UpdateFinalOverlay();
            else UpdateStandardHud();
        }

        /// <summary>
        /// 이 씬에 <b>공용 결과 판이 깔려 있는가.</b>
        ///
        /// 한 번 찾고 기억한다. 결과 판은 씬이 살아 있는 동안 생기거나 사라지지 않는다.
        /// 꺼져 있을 수 있으므로 <c>FindObjectsInactive.Include</c> 로 찾는다 —
        /// 평소에는 숨어 있다가 결과가 올 때만 켜지기 때문이다.
        /// </summary>
        private bool SharedResultPanelExists()
        {
            if (sharedResultPanel.HasValue) return sharedResultPanel.Value;

            bool found = FindAnyObjectByType<MiniGames.Common.UI.MiniGameResultOverlay>(
                FindObjectsInactive.Include) != null;

            sharedResultPanel = found;
            return found;
        }

        private bool? sharedResultPanel;

        /// <summary>
        /// **새 판이 시작되면 공용 결과 판을 닫는다.**
        ///
        /// ⚠ <b>[다시 하기] 를 누른 사람의 화면만 닫히던 문제를 막는다.</b> 결과 판은 누른
        ///    쪽에서 스스로 닫지만, 상대 화면은 아무도 닫아 주지 않아 <b>새 판이 카운트다운을
        ///    하는 동안에도 GAME OVER 가 그대로 떠 있었다.</b>
        ///
        /// 판이 끝났는지는 서버가 정해 모두에게 같은 값으로 온다(<see cref="NetworkFinal"/>).
        /// 그것이 꺼지는 순간 두 화면이 함께 닫힌다.
        /// </summary>
        private void CloseSharedResultOnRestart(bool isFinalOverlay)
        {
            if (isFinalOverlay)
            {
                sawFinal = true;
                return;
            }

            if (!sawFinal) return;

            sawFinal = false;

            MiniGames.Common.UI.MiniGameResultOverlay shared =
                FindAnyObjectByType<MiniGames.Common.UI.MiniGameResultOverlay>(FindObjectsInactive.Include);

            if (shared != null) shared.CloseForNewMatch();
        }

        /// <summary>지난 프레임에 결과 화면이었는가. 닫을 때를 한 번만 잡기 위한 것이다.</summary>
        private bool sawFinal;

        private void UpdateStandardHud()
        {
            bool net = NetworkMatchActive;
            bool tentacle = net ? NetworkRound == 2 : flow.Phase == WarriorsBattlePhase.KrakenTentaclePhase;
            int secondsLeft = Mathf.CeilToInt(score.RemainingSeconds);
            int hp = playerHealth != null ? playerHealth.CurrentHealth : 100;
            int maxHp = playerHealth != null ? playerHealth.MaxHealth : 100;
            float progress = net ? Mathf.Clamp01(NetworkObjectiveProgress)
                : tentacle ? flow.TentacleSuccesses / (float)Mathf.Max(1, flow.TentacleSuccessesRequired)
                : score.Progress;

            // 매치 안내가 있으면 그쪽이 먼저다. 대기 · 카운트다운 · 결과에만 쓰인다.
            Set(roundText, string.IsNullOrEmpty(MatchNotice)
                ? (tentacle ? "ROUND 2  ·  크라켄의 등장" : "ROUND 1  ·  몬스터 습격")
                : MatchNotice);
            Set(timeText, string.IsNullOrEmpty(MatchDetail)
                ? $"{secondsLeft / 60:00}:{secondsLeft % 60:00}"
                : MatchDetail);
            FitLongText(timeText, !string.IsNullOrEmpty(MatchDetail));
            Set(hpText, $"HP  {hp}");
            SetFill(hpFill, hp / (float)Mathf.Max(1, maxHp));
            // 처치 수는 상단 가운데 막대의 몫이다. TIME 칸에는 시간만 들어간다.
            Set(phaseValueText, net ? NetworkObjective
                : tentacle ? $"잘라낸 촉수   {flow.TentacleSuccesses} / {flow.TentacleSuccessesRequired}"
                : $"처치 수   {score.Kills} / {score.TargetKills}");
            SetFill(phaseFill, progress);
            Set(scoreText, (net ? NetworkScore : score.Score).ToString("N0"));
            Set(comboText, $"COMBO  {(net ? NetworkCombo : combo != null ? combo.Combo : 0)}");
            int activePlayers = ResolveConnectedPlayers();

            for (int i = 0; i < playerStateTexts.Length; i++)
            {
                // 빈 칸은 "대기" 로 두지 않고 숨긴다. 전투는 없는 사람을 기다리지 않으므로
                // 판이 끝날 때까지 WAIT 이라고 적혀 있는 칸은 거짓말이다.
                bool active = i < activePlayers;
                SetActive(RowOf(playerStateTexts[i], playerStatusRoot), active);
                if (!active) continue;

                // ⚠ **"적중 0" 을 뺐다.** 전투 중 아무 판단에도 쓰이지 않는 숫자인데
                //    프로필 칸의 절반을 차지했다. 프로필에서 알아야 하는 것은
                //    <b>누구인지</b> 와 <b>살아 있는지</b> 둘뿐이다.
                Set(playerStateTexts[i], NameOf(i));

                // 막대는 그 사람의 남은 체력이다. 적중 비율이 아니다.
                if (i < playerStateFills.Length) SetFill(playerStateFills[i], HealthOf(i));
            }
        }

        /// <summary>
        /// 이 칸에 적을 <b>이름.</b> 실제 계정 닉네임이 있으면 그것을 쓴다.
        ///
        /// 닉네임은 <c>NetworkPlayerIdentity</c> 가 <c>[Networked]</c> 로 들고 있어
        /// <b>남의 것도 읽힌다</b> — 주인 클라이언트가 서버에 올리고 서버가 모두에게 복제한다.
        ///
        /// 없을 때는 "1P" 로 떨어진다. 계정 없이 도는 단독 빌드가 그 경우다.
        /// </summary>
        private string NameOf(int playerIndex)
        {
            WarriorsPlayerCombat who = WarriorsPlayers.ForId(playerIndex);

            if (who != null)
            {
                UnderTheSea.Network.NetworkPlayerIdentity id =
                    who.GetComponentInParent<UnderTheSea.Network.NetworkPlayerIdentity>();

                // ⚠ 사라지는 중인 오브젝트의 [Networked] 값을 읽으면 예외가 난다.
                if (id != null && id.Object != null && id.Object.IsValid)
                {
                    string shown = id.DisplayName;

                    if (!string.IsNullOrWhiteSpace(shown) && shown != "이름 없음")
                    {
                        return $"{playerIndex + 1}P  {shown}";
                    }
                }
            }

            return $"{playerIndex + 1}P";
        }

        /// <summary>이 사람의 남은 체력 비율. 0~1.</summary>
        private float HealthOf(int playerIndex)
        {
            WarriorsPlayerCombat who = WarriorsPlayers.ForId(playerIndex);
            if (who == null) return 0f;

            Warriors.Net.WarriorsPlayerLife life =
                who.GetComponentInParent<Warriors.Net.WarriorsPlayerLife>();

            if (life == null || life.Object == null || !life.Object.IsValid || life.MaxHp <= 0)
            {
                WarriorsHealth plain = who.GetComponentInParent<WarriorsHealth>();
                return plain != null && plain.MaxHealth > 0
                    ? plain.CurrentHealth / (float)plain.MaxHealth
                    : 0f;
            }

            return Mathf.Clamp01(life.Hp / (float)life.MaxHp);
        }

        /// <summary>
        /// The slot count must reflect the players that actually exist in the scene rather
        /// than a configured maximum, otherwise a solo run shows a second player who is not
        /// there.
        ///
        /// The same roster the flow counts from, so the strip and the round can never
        /// disagree about how many people are playing. It replaced a half second polling
        /// scan, which also meant the strip lagged a join by up to half a second.
        /// </summary>
        private int ResolveConnectedPlayers() =>
            Mathf.Clamp(Mathf.Max(1, WarriorsPlayers.Count), 1, WarriorsPlayers.Max);

        /// <summary>
        /// Matched on the player id the combat itself reports rather than on scan order, which
        /// is not stable and would let the two rows swap places mid run.
        /// </summary>
        private int LandedHitsOf(int playerIndex)
        {
            WarriorsPlayerCombat combat = WarriorsPlayers.ForId(playerIndex);
            return combat != null ? combat.LandedHits : 0;
        }

        /// <summary>
        /// The row a HUD element belongs to, found by walking up to the direct child of
        /// <paramref name="root"/>, so the prefab can nest these however it likes.
        /// </summary>
        private static GameObject RowOf(Component child, GameObject root)
        {
            if (child == null) return null;
            if (root == null) return child.gameObject;
            Transform current = child.transform;
            while (current != null && current.parent != root.transform) current = current.parent;
            return current != null ? current.gameObject : child.gameObject;
        }

        /// <summary>
        /// Each round asks the player to do something different, so every round opens with a
        /// two line brief: what is happening, then what to press. Driven purely off the phase
        /// the battle flow reports - no gameplay state is touched here.
        ///
        /// 네트워크 라운드(1~3)를 소개 문구를 고르는 페이즈 값으로 옮긴다. 0(대기)은 소개가 없는 값으로.
        /// 서버가 라운드를 바꾸는 순간 두 화면이 같은 소개를 본다 — 서버도 그동안 게임을 붙잡아 둔다.
        /// </summary>
        private static WarriorsBattlePhase PhaseForNetworkRound(int round) => round switch
        {
            1 => WarriorsBattlePhase.NormalBattle,
            2 => WarriorsBattlePhase.KrakenTentaclePhase,
            3 => WarriorsBattlePhase.FinalKrakenPhase,
            _ => WarriorsBattlePhase.Clear,
        };

        private void UpdateRoundIntro(bool finalOverlayVisible)
        {
            WarriorsBattlePhase currentPhase = NetworkMatchActive ? PhaseForNetworkRound(NetworkRound) : flow.Phase;

            if (currentPhase != introPhase)
            {
                introPhase = currentPhase;
                string title = null, body = null, objective = null, badge = null;

                // ⚠ 인원에 따라 문장을 바꾸지 않는다. 예전에는 혼자/둘이 각각 다른 협동 안내를 냈는데,
                //    화면에 설명이 계속 쌓여 읽느라 바쁜 게임이 됐다. 라운드마다 한 줄이면 충분하다.
                switch (currentPhase)
                {
                    // 문구는 짧게, 규칙이 바로 읽히게 쓴다. 예전 문장("~하기 시작했습니다",
                    // "~하면 보너스가 들어옵니다")은 게임 안의 말이 아니라 기능 설명문으로 읽혔다.
                    // ⚠ 뱃지 칸(RoundBadge)은 210x46 이다. 라운드 이름까지 넣었더니 글자가 칸을 넘쳤다.
                    //    라운드 이름은 좌상단 카드가 이미 보여 주므로 여기는 번호만 짧게 둔다.
                    case WarriorsBattlePhase.NormalBattle:
                        badge = "ROUND 1";
                        title = "몬스터들이 해변으로 몰려옵니다!";
                        body = "몬스터 종류에 맞는 공격으로 막아내세요.";
                        objective = "몬스터 종류에 맞는 공격으로 막아내세요.";
                        break;
                    case WarriorsBattlePhase.KrakenTentaclePhase:
                        badge = "ROUND 2";
                        title = "크라켄이 모습을 드러냈습니다!";
                        body = "촉수의 표시와 같은 방향으로 공격하세요.";
                        objective = "촉수의 표시와 같은 방향으로 공격하세요.";
                        break;
                    case WarriorsBattlePhase.FinalKrakenPhase:
                        badge = "ROUND 3";
                        title = "크라켄이 마지막 공격을 준비합니다!";
                        // 한 줄만 둔다. 콤보 안내("연속으로 성공하면…")까지 두 줄을 쓰니 읽을 게 많았다.
                        body = "내려오는 화살표가 판정선에 닿을 때 공격하세요.";
                        objective = "내려오는 화살표가 판정선에 닿을 때 공격하세요.";
                        break;
                }

                // **앞 라운드를 깬 문구.**
                //
                // ⚠ 예전에는 <b>라운드 번호가 오른 것</b>을 보고 판단했다. 그런데 번호가 오르는
                //   순간이 곧 무대가 바뀌는 순간이라, 문구가 항상 <b>다음 라운드 무대 위에서</b>
                //   떴다. 이제 서버가 "방금 깬 라운드" 를 따로 알려 주고, 그 동안 무대는
                //   앞 라운드에 붙잡혀 있다.
                if (NetworkMatchActive && NetworkClearedRound > 0)
                {
                    clearNotice = ClearedLine(NetworkClearedRound);
                    clearNoticeUntil = Time.unscaledTime + 1.8f;
                }

                if (NetworkMatchActive) shownRound = NetworkRound;

                if (title == null) { introUntil = 0f; objectiveLabel = string.Empty; }
                else
                {
                    Set(roundIntroBadge, badge);
                    Set(roundIntroTitle, title);
                    Set(roundIntroBody, body);
                    objectiveLabel = objective;
                    Set(objectiveText, objective);
                    FitObjectivePanel(objective);
                    introUntil = Time.unscaledTime + roundIntroSeconds;
                    // Keep the briefing above every regular HUD panel regardless of the
                    // prefab's authored sibling order.
                    roundIntroRoot?.transform.SetAsLastSibling();
                }
            }

            float remaining = introUntil - Time.unscaledTime;

            // 네트워크 판에서는 **서버가 정한 소개 구간**에만 띄운다. 종료 문구가 아직 떠 있거나
            // 그 뒤 여백인 동안에는 켜지지 않는다 — 둘이 동시에 화면에 있는 일이 없다.
            bool show = !finalOverlayVisible
                        && (NetworkMatchActive ? InIntroWindow : remaining > 0f);

            SetActive(roundIntroRoot, show);

            // The brief does not simply vanish: over its last moments it shrinks and fades,
            // and the one line that still matters settles under the TIME card as a goal.
            if (show && roundIntroGroup != null)
            {
                const float outro = .4f;
                float a = Mathf.Clamp01(remaining / outro);
                roundIntroGroup.alpha = a;
                Transform scaled = roundIntroCard != null ? roundIntroCard : roundIntroRoot.transform;
                scaled.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, a);
            }

            bool showObjective = !finalOverlayVisible && !show && !string.IsNullOrEmpty(objectiveLabel);
            SetActive(objectiveRoot, showObjective);

            // ⚠ 폭은 **보이는 동안 매 프레임** 실제 글자 상자로 다시 잰다. 소개 문구가 뜨는 순간 한 번만
            //    재면 그때 TMP 가 아직 이전 라운드 문장을 들고 있어, 3라운드의 긴 문장이 1라운드
            //    폭의 칸을 넘었다(실측). TMP 하나 ForceMeshUpdate 는 프레임 비용이 무시할 만하다.
            if (showObjective) FitObjectivePanel(objectiveLabel);
            if (objectiveGroup != null)
                objectiveGroup.alpha = showObjective
                    ? Mathf.MoveTowards(objectiveGroup.alpha, 1f, Time.unscaledDeltaTime * 3.5f)
                    : 0f;

            // The brief is a briefing, not a blindfold: while it is up the round must not
            // already be running behind it. Holding timeScale stops the spawner, the round
            // clock, enemy movement and the rhythm notes at once, without any of those
            // systems needing to know the HUD exists.
            // 네트워크에서는 이 PC 의 timeScale 을 멈추지 않는다. 서버가 소개 시간 동안 게임을 붙잡아 두고
            // (WarriorsMatchState.InIntro), 여기서 멈추면 다른 PC 와 어긋난다.
            // ⚠ "잡을지" 만 조건에 넣고 "풀기" 는 늘 한다. 한번 조건을 통째로 막았더니, 네트워크 플래그가
            //    켜지기 전 첫 프레임에 0 으로 잡은 timeScale 이 영영 풀리지 않았다 — 화면이 멈추고 피격
            //    플래시가 빨갛게 고정되고 카메라가 캐릭터 머리 위에서 움직이지 않았다. 실측으로 확인했다.
            bool hold = holdGameplayDuringIntro && !NetworkMatchActive && show;

            if (introHoldsTime != hold)
            {
                introHoldsTime = hold;
                Time.timeScale = hold ? 0f : 1f;
            }
        }

        /// <summary>
        /// Attack feedback without adding more words to the screen: the guide card for the
        /// swing the player just made lifts and brightens briefly, then settles back.
        /// </summary>
        private void UpdateGuidePulse()
        {
            WarriorsAttackDirection type = Time.unscaledTime <= lastAttackUntil
                ? lastAttack
                : WarriorsAttackDirection.None;
            int lit = type switch
            {
                WarriorsAttackDirection.HorizontalSlash => 0,
                WarriorsAttackDirection.VerticalSlash => 1,
                WarriorsAttackDirection.Thrust => 2,
                _ => -1,
            };

            Cache(monsterGuideCards, ref monsterCardFills, ref monsterCardBase);
            Cache(attackGuideCards, ref attackCardFills, ref attackCardBase);
            Pulse(monsterGuideCards, monsterCardFills, monsterCardBase, lit);
            Pulse(attackGuideCards, attackCardFills, attackCardBase, lit);
        }

        /// <summary>
        /// 카드의 **키캡 윗면(KeyBadge/CapFill)** 을 잡는다. 카드 판 자체는 금테 그림에 흰 tint 라
        /// 색을 더해도 보이지 않는다(1 을 넘어 잘림). 키캡은 남색 평면이라 색이 그대로 난다.
        /// </summary>
        private static void Cache(RectTransform[] cards, ref UnityEngine.UI.Image[] fills, ref Color[] baseColors)
        {
            if (cards == null || (fills != null && fills.Length == cards.Length)) return;
            fills = new UnityEngine.UI.Image[cards.Length];
            baseColors = new Color[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                Transform cap = cards[i].Find("KeyBadge/CapFill");
                fills[i] = cap != null ? cap.GetComponent<UnityEngine.UI.Image>() : cards[i].GetComponent<UnityEngine.UI.Image>();
                if (fills[i] != null) baseColors[i] = fills[i].color;
            }
        }

        /// <summary>펄스 색. 공격 종류 색(카드 아이콘 · 노트 링과 같은 계열)으로 키캡이 번쩍인다.</summary>
        private static readonly Color[] PulseTone =
        {
            new Color(.25f, .65f, 1f, 1f),    // 가로베기 — 파랑
            new Color(1f, .32f, .38f, 1f),    // 세로베기 — 빨강
            new Color(1f, .85f, .15f, 1f),    // 찌르기 — 노랑
        };

        private static void Pulse(RectTransform[] cards, UnityEngine.UI.Image[] fills, Color[] baseColors, int lit)
        {
            if (cards == null || fills == null) return;
            float step = Time.unscaledDeltaTime * 10f;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                bool on = i == lit;

                // Snap up on the swing and ease back down - a hit cue has to land on the
                // same frame as the input to read as a response rather than a delay.
                if (on)
                {
                    cards[i].localScale = Vector3.one * 1.12f;
                    if (fills[i] != null) fills[i].color = PulseTone[Mathf.Clamp(i, 0, PulseTone.Length - 1)];
                    continue;
                }

                cards[i].localScale = Vector3.Lerp(cards[i].localScale, Vector3.one, step);
                if (fills[i] != null) fills[i].color = Color.Lerp(fills[i].color, baseColors[i], step);
            }
        }

        private void ReleaseIntroHold()
        {
            if (!introHoldsTime) return;
            introHoldsTime = false;
            Time.timeScale = 1f;
        }

        /// <summary>
        /// The battle scripts still label their states in English shorthand. Translating here
        /// keeps those scripts untouched while the player only ever reads Korean.
        /// </summary>
        private static string Korean(string label) => label switch
        {
            "BREAK!" => "촉수를 모두 잘라냈습니다!",
            "SWING!" => "지금 공격하세요!",
            "FINAL SWING" => "마지막 일격!",
            "SUCCESS!" => "성공!",
            "FINISH CHANCE" => "마무리 기회!",
            "ULTIMATE READY" => "필살기 준비 완료!",
            "GAME OVER" => "패배",
            "TIME OVER" => "시간 종료",
            "PERFECT!" => "완벽!",
            "GOOD" => "좋아요!",
            "MISS" => "놓쳤어요",
            "FINAL COMBO" => "마지막 연타!",
            // 3라운드 리듬 보스전 문구
            "COMBO FINISH!" => "콤보 어택!",
            "TEAM FINISH!" => "둘이 함께 마무리!",
            "KRAKEN COUNTER" => "크라켄 반격!",
            _ => label,
        };

        /// <summary>
        /// **무대가 바뀌는 순간을 덮는 화면 페이드.**
        ///
        /// 서버는 종료 문구가 끝난 시각(<c>NetworkIntroStartSeconds</c>)에 페이즈를 넘긴다.
        /// 그 프레임에 카메라 구도와 캐릭터 자리가 한꺼번에 바뀌므로, 아무것도 덮지 않으면
        /// "어느 순간 갑자기 위치가 바뀜" 으로 보인다. 그 앞뒤를 짧게 검게 덮는다.
        ///
        /// <code>
        ///   종료 문구 끝  →  0.5초 동안 어두워짐
        ///   전환 시점     →  여기서 무대가 바뀐다 (가장 어두울 때)
        ///   소개 시작     →  0.35초 동안 다시 밝아짐
        /// </code>
        ///
        /// ⚠ 완전한 검정까지 가지 않는다(<see cref="FadePeak"/>). 화면이 통째로 꺼지면
        ///   짧은 전환이 오히려 뚝 끊긴 것처럼 보인다.
        /// </summary>
        private void UpdateTransitionFade()
        {
            float elapsed = NetworkTransitionElapsed;
            float gate = NetworkIntroStartSeconds;

            // 어두워지는 데 쓰는 시간은 <b>문구가 끝난 뒤 남은 여백</b> 전체다.
            // 라운드 사이는 0.5초, 판 마지막(크라켄 처치)은 1.6초라 저절로 더 천천히 어두워진다.
            // 판 마지막은 크라켄이 가라앉는 동안 밝게 두고, 다 가라앉은 뒤에만 어두워진다.
            float fadeStart = NetworkFadeStartSeconds >= 0f ? NetworkFadeStartSeconds : NetworkClearSeconds;
            float fadeOut = Mathf.Max(.3f, gate - fadeStart);

            float target = 0f;

            // ⚠ **판이 끝나면 어두운 채로 둔다.**
            //
            //    예전에는 전환이 끝나면서 다시 밝아졌다. 그 순간 무대가 정리되고 카메라가
            //    제자리로 돌아가는 것이 그대로 보여 **화면이 한 번 튀었다.** 그 뒤에 결과 판이
            //    떴으니 사람 눈에는 "끝났는데 화면이 흔들리고 나서 결과가 나온다" 가 된다.
            //
            //    결과 판은 어두워진 화면 위에 뜨는 것으로 충분하다. 뒤에서 무엇이 정리되든
            //    보이지 않는다.
            if (NetworkMatchActive && NetworkFinal != 0)
            {
                // 라운드 사이(82%)보다는 짙게, 그렇다고 완전히 검게는 하지 않는다.
                //
                // 완전히 덮었더니 결과 판만 뜬 검은 화면이 되어 "게임이 꺼진" 느낌이었다.
                // 무대가 희미하게 비치는 편이 방금 무엇을 하다 끝났는지 남는다.
                // 뒤에서 카메라가 움직이는 것은 WarriorsLocalView 가 멈춰 두므로
                // 비쳐도 흔들리지 않는다.
                target = FinalFade;
            }
            else if (NetworkMatchActive && elapsed >= 0f)
            {
                if (elapsed < gate)
                {
                    // 전환 시점으로 다가가며 어두워진다.
                    float into = gate - elapsed;
                    target = FadePeak * Mathf.Clamp01(1f - into / fadeOut);
                }
                else
                {
                    // 전환이 끝났다. 다시 밝아진다.
                    target = FadePeak * Mathf.Clamp01(1f - (elapsed - gate) / FadeInSeconds);
                }
            }

            // ⚠ **걷힐 때는 목표값을 그대로 따르지 않는다.**
            //    판 마지막에는 전환이 끝나는 순간 경과가 -1 이 되어 목표가 0 으로 뚝 떨어진다.
            //    그대로 두면 결과 화면이 <b>깜빡하며</b> 나타난다. 올라갈 때만 서버 시계를 따르고,
            //    내려갈 때는 일정한 속도로 천천히 걷는다.
            if (target >= fadeAlpha) fadeAlpha = target;
            else fadeAlpha = Mathf.MoveTowards(fadeAlpha, target, Time.unscaledDeltaTime / FadeInSeconds);

            float alpha = fadeAlpha;

            if (alpha <= .001f)
            {
                if (transitionFade != null && transitionFade.gameObject.activeSelf)
                    transitionFade.gameObject.SetActive(false);
                return;
            }

            RectTransform fade = EnsureTransitionFade();
            if (fade == null) return;

            fade.gameObject.SetActive(true);

            Image image = fade.GetComponent<Image>();
            if (image != null) image.color = new Color(.02f, .04f, .07f, alpha);
        }

        /// <summary>전환 페이드가 가장 진할 때의 알파.</summary>
        private const float FadePeak = .82f;

        /// <summary>판이 끝난 뒤 결과 판 뒤를 덮는 정도. 무대가 희미하게 비칠 만큼만 남긴다.</summary>
        private const float FinalFade = .92f;

        /// <summary>다시 밝아지는 데 걸리는 시간(초).</summary>
        private const float FadeInSeconds = .45f;

        private RectTransform transitionFade;

        /// <summary>지금 화면에 깔려 있는 어둠. 걷힐 때 목표값을 천천히 따라간다.</summary>
        private float fadeAlpha;

        /// <summary>화면 전체를 덮는 판을 만들어 둔다. 라운드 소개보다 뒤에 그려야 글이 읽힌다.</summary>
        private RectTransform EnsureTransitionFade()
        {
            if (transitionFade != null) return transitionFade;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            RectTransform parent = canvas.transform as RectTransform;
            if (parent == null) return null;

            GameObject piece = new("RoundTransitionFade", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)piece.transform;

            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // ⚠ 라운드 소개 문구보다 **뒤**에 둔다. 맨 앞에 두면 읽어야 할 글을 덮는다.
            rect.SetAsFirstSibling();

            Image image = piece.GetComponent<Image>();
            image.raycastTarget = false;

            transitionFade = rect;
            return rect;
        }

        private void UpdateSharedFeedback(bool finalOverlayVisible)
        {
            // 네트워크 판에서는 라운드를 깬 문구를 여기로 낸다. 혼자 하는 씬은 예전 경로 그대로.
            //
            // ⚠ **자기 타이머(clearNoticeUntil)로 판단하지 않는다.** 그렇게 두었더니 소개 화면과
            //    시계가 따로 돌아 "해변 방어 성공!" 과 "크라켄이 모습을 드러냈습니다!" 가
            //    같은 순간에 겹쳐 떴다. 서버가 주는 전환 경과 하나로 구간을 나눈다.
            bool showCleared = NetworkMatchActive
                               && !finalOverlayVisible
                               && InClearWindow
                               && !string.IsNullOrEmpty(clearNotice);

            bool showAnnouncement = showCleared ||
                                    (!NetworkMatchActive && flow.IsTransitioning && !finalOverlayVisible &&
                                     Time.unscaledTime >= introUntil);

            SetActive(announcementRoot, showAnnouncement);
            Set(announcementText, showCleared ? clearNotice
                : showAnnouncement ? Korean(flow.TransitionLabel) : string.Empty);
            Set(specialText, combo != null ? combo.ActiveSpecial : string.Empty);
            // The attack name is no longer written across the screen; the card reacts instead.
            Set(actionText, string.Empty);
        }

        // ------------------------------------------------------------
        // 크라켄 리액션 — 정타마다 움찔, 3연속부터 크게
        // ------------------------------------------------------------

        private int krakenPulseSeen = -1;
        private Transform krakenVisual;
        private Vector3 krakenRest;
        private Coroutine krakenShake;

        /// <summary>
        /// 정타 신호(<see cref="NetworkRhythmPulseLane"/>)가 새로 켜지는 순간 크라켄을 흔든다.
        /// 서버가 복제한 값의 **변화**를 보므로 두 클라이언트에서 같은 순간에 난다.
        /// 루트는 NetworkTransform 이 쥐고 있으므로 겉모습 자식만 흔든다 (몬스터 squash 와 같은 이유).
        /// </summary>
        private void ShakeKrakenOnHit()
        {
            int lane = NetworkRhythmPulseLane;
            bool rising = lane >= 0 && krakenPulseSeen < 0;
            krakenPulseSeen = lane;
            if (!rising) return;

            if (krakenVisual == null)
            {
                WarriorsKrakenBoss boss = FindFirstObjectByType<WarriorsKrakenBoss>();
                Renderer body = boss != null ? boss.GetComponentInChildren<Renderer>() : null;
                if (body == null) return;

                Transform step = body.transform;
                while (step.parent != null && step.parent != boss.transform) step = step.parent;
                krakenVisual = step;
                krakenRest = krakenVisual.localPosition;
            }

            int combo = NetworkMatchActive ? NetworkCombo : (rhythm != null ? rhythm.Combo : 0);
            float amplitude = combo >= 3 ? .35f : .14f;

            if (krakenShake != null) StopCoroutine(krakenShake);
            krakenShake = StartCoroutine(KrakenShakeRoutine(amplitude, combo >= 3 ? .34f : .2f));
        }

        private System.Collections.IEnumerator KrakenShakeRoutine(float amplitude, float seconds)
        {
            for (float t = 0f; t < seconds && krakenVisual != null; t += Time.deltaTime)
            {
                float k = 1f - t / seconds;
                float wobble = Mathf.Sin(t * 60f) * amplitude * k;
                krakenVisual.localPosition = krakenRest + new Vector3(wobble, -Mathf.Abs(wobble) * .4f, 0f);
                yield return null;
            }

            if (krakenVisual != null) krakenVisual.localPosition = krakenRest;
            krakenShake = null;
        }

        private void UpdateRhythmHud()
        {
            ShakeKrakenOnHit();
            int secondsLeft = Mathf.CeilToInt(score.RemainingSeconds);
            Set(rhythmRoundText, string.IsNullOrEmpty(MatchNotice) ? "ROUND 3  ·  크라켄의 공격" : MatchNotice);

            // **TIME 칸에는 시간만 넣는다.**
            //
            // 예전에는 여기에 "1P DOWN   2P HP 64" 를 적었다. TIME 이라고 쓰인 칸에 체력이 들어가
            // 무엇을 보는 칸인지 알 수 없었고, 1·2라운드와 좌상단 구조도 달라졌다.
            // 두 사람의 상태는 이미 우상단 플레이어 줄이 맡고 있다.
            Set(rhythmTimeText, NetworkRhythmActive
                ? (string.IsNullOrEmpty(MatchDetail) ? "--:--" : MatchDetail)
                : $"{secondsLeft / 60:00}:{secondsLeft % 60:00}");
            FitLongText(rhythmTimeText, false);
            // ROUND 3 is the round that actually hits back - the counter takes ten off
            // the player - and it was the one round with no health on screen at all.
            int hp = playerHealth != null ? playerHealth.CurrentHealth : 100;
            int maxHp = playerHealth != null ? playerHealth.MaxHealth : 100;
            Set(rhythmHpText, $"HP  {hp}");
            SetFill(rhythmHpFill, hp / (float)Mathf.Max(1, maxHp));
            // ROUND 1 and 2 put a number on the centre panel; the boss bar was the one
            // round that gave a colour and nothing to read. The percentage matches the
            // fill, so the line and the bar say the same thing.
            if (NetworkRhythmActive)
            {
                // ⚠ <c>NetworkRhythmProgress</c> 는 **남은 체력**이다(1 = 가득). 그대로 채운다.
                //    예전에는 여기서 1 에서 빼 한 번 더 뒤집는 바람에, 크라켄을 때릴수록
                //    막대가 차올랐다. 보내는 쪽(WarriorsPhase3Director)이 이미 뒤집어 준다.
                Set(rhythmBossText, NetworkRhythmDetail);
                PaintBossBar(rhythmBossFill, NetworkRhythmProgress);
            }
            else
            {
                Set(rhythmBossText, $"크라켄   {flow.FinalKrakenHealthPercent}%");
                PaintBossBar(rhythmBossFill, flow.FinalKrakenHealthPercent / 100f);
            }
            Set(rhythmScoreText, (NetworkMatchActive ? NetworkScore : score.Score).ToString("N0"));
            Set(rhythmComboText, $"COMBO  {rhythm.Combo}");
            // 네트워크 3페이즈는 서버가 정한 판정 문구(콤보 피니시)를 쓴다. 혼자 하는 씬은 예전 그대로.
            string judgement = NetworkRhythmActive
                ? Korean(NetworkRhythmJudgement ?? string.Empty)
                : Korean(rhythm.ActiveJudgement);
            Set(rhythmJudgementText, judgement);
            // ⚠ 판정 글자의 배경 칩은 쓰지 않는다. 실측에서 화면 한가운데(크라켄 머리 위)에
            //    글자 없는 금테 막대로만 보였다 — 글자는 다른 자리(아래 안내 줄)에 그려지기 때문.
            SetActive(rhythmJudgementChip, false);

            if (NetworkRhythmActive)
            {
                visibleNotes.Clear();
                visibleNotes.AddRange(NetworkRhythmNotes);
            }
            else
            {
                rhythm.CopyVisibleNotes(visibleNotes);
            }
            CacheRhythmNoteFills();

            // One lane per player who is actually here: solo reads as a single central
            // column, a pair as one column each.
            int playerCount = Mathf.Clamp(ResolveConnectedPlayers(), 1, WarriorsPlayers.Max);

            // 레인 간격과 낙하 거리. **이 두 값이 3라운드가 리듬게임으로 읽히는지를 정한다.**
            //
            // 예전에는 간격 96 · 낙하 430 이었다. 1920 화면에서 두 레인이 96px 떨어져 있으면
            // 좌우로 갈린 두 줄이 아니라 캐릭터 앞에 아이콘이 몇 개 떠 있는 것으로 보인다.
            // 실제 플레이 영상에서 "리듬 레인 · 내려오는 노트 · 판정선" 구조가 전혀 읽히지 않았다.
            //
            // 간격을 벌려 1P 는 왼쪽, 2P 는 오른쪽 줄이 되게 하고, 낙하 거리를 늘려
            // 노트가 "위에서 내려와 선에 닿는" 것이 눈에 보이게 한다.
            // ⚠ 판정선 자리를 프리팹의 HitLine 에서 읽지 않는다. 그 오브젝트는 NoteTrack 로컬 +280,
            //    즉 **트랙 맨 위**에 있어 스폰 높이와 150 밖에 차이나지 않았다. 노트가 내려오는 것이
            //    보이지 않던 진짜 이유다. 위에서 계산한 값을 쓴다 — 낙하 500.
            const float noteSpawnY = NoteSpawnLocalY;
            const float hitLineY = JudgeLocalY;

            UpdateLaneTracks(playerCount, noteSpawnY, hitLineY);

            for (int i = 0; i < rhythmNotes.Length; i++)
            {
                bool visible = i < visibleNotes.Count;
                rhythmNotes[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    // 조각과 글로우는 노트와 별개의 오브젝트다. 노트를 끄기만 하면 화면에 남는다.
                    HideNoteShards(i);
                    HideNoteGlow(i);
                    HideNoteTrail(i);
                    continue;
                }
                WarriorsRhythmNoteView note = visibleNotes[i];

                // ⚠ 프리팹의 노트는 앵커도 피벗도 (0, 0.5) — **트랙 왼쪽 가장자리 기준**이다.
                //    그대로 두고 중앙 기준 좌표를 넣으면 노트가 통째로 어긋난다. 가운데로 맞춘다.
                RectTransform slot = rhythmNotes[i];
                if (slot.pivot.x != .5f || slot.anchorMin.x != .5f)
                {
                    slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(.5f, .5f);
                }

                // 레인 배경과 **같은 중심**을 쓰고, 공격 종류만큼 열을 옮긴다.
                int laneIndex = Mathf.Clamp(note.PlayerIndex, 0, playerCount - 1);
                float centre = laneIndex < laneCentres.Count
                    ? laneCentres[laneIndex]
                    : TrackCentre(laneIndex, playerCount);

                // **노트는 트랙 한가운데로 내려온다.**
                //
                // 공격 종류마다 열을 달리해 봤더니, 세 갈래로 흩어져 "어느 칸으로 몸을 옮길까" 처럼
                // 읽혔다. 이 게임은 자리를 옮기는 게임이 아니라 **선 자리에서 검을 휘두르는** 게임이다.
                // 종류는 노트의 색과 기호가 말하면 충분하다.
                //
                // widen 은 거리감에만 쓴다. Travel 0 = 막 생김, 1 = 판정선.
                float widen = Mathf.LerpUnclamped(TrackTopScale, 1f, note.Travel);
                float x = centre;
                // Travel 1 is the moment the note is due, so it has to be exactly on the line
                // then - the two used to disagree, and the player had to swing when the note
                // was already well past it. Unclamped so a missed note keeps falling through
                // instead of stopping dead on the line.
                float y = Mathf.LerpUnclamped(noteSpawnY, hitLineY, note.Travel);
                rhythmNotes[i].anchoredPosition = new Vector2(x, y);

                // 노트 색은 공격 종류를 따른다(물고기 파랑 · 게 빨강 · 해파리 보라, 아래 카드와 같은 색).
                // 판정이 나면 그 자리에서 바뀐다: 성공은 초록, 실패는 어둡게 — "쳤는데 인식이 안 된다" 는
                // 느낌은 결과가 눈에 안 보였기 때문이다. 내려오는 동안에는 색이 변하지 않는다.
                // 남의 레인(네트워크)은 흐리고 작게 그려 내 노트가 먼저 읽히게 한다.
                bool mine = !NetworkMatchActive || NetworkLocalLane < 0 || note.PlayerIndex == NetworkLocalLane;
                // .38 은 남의 노트가 배경에 묻혀 "화살표가 안 보인다" 로 읽혔다(실측). 내 것과
                // 구분만 되면 되니 .7 로 올린다. 크기 차(.92)가 여전히 내 것을 앞에 세운다.
                float alpha = mine ? 1f : .7f;

                // 멀리 있을수록 작다. 다만 스폰 지점에서도 무엇인지는 읽혀야 하므로
                // 트랙 비율(0.60)을 그대로 쓰지 않고 0.80~1.30 으로 눌러 쓴다.
                // 프리팹 노트가 90px 이라 판정선에서 117px, 스폰에서 72px 이 된다.
                float noteScale = Mathf.LerpUnclamped(.80f, 1.30f, Mathf.Clamp01(note.Travel));

                // **색은 링과 기호만 갖는다. 가운데는 비어 있다.**
                Color tone = note.IsMissed ? rhythmMissedColor : NoteColor(note.Type);

                // **맞으면 터진다.** 그냥 사라지면 "쳤다" 는 결과가 화면에 남지 않는다.
                // 판정된 뒤 잠깐(clearSeconds) 더 살아 있는 동안 링을 키우면서 지운다.
                // 노트가 1.45초에 540px 내려오므로 Travel 0.18 이 약 0.26초다.
                // 베인 뒤 그만큼만 살아 있다가 사라진다 — 짧아야 다음 노트를 가리지 않는다.
                float burst = note.IsSuccessfulHit ? Mathf.Clamp01((note.Travel - 1f) / .18f) : 0f;

                // 갈라지는 조각이 <b>칼 반대편으로 날아가야</b> 잘린 것으로 읽힌다.
                // 크기는 BurstSpread 가 방향까지 맡으므로 여기서는 전체 배율만 살짝 준다.
                float pop = 1f + burst * .35f;
                float fade = note.IsSuccessfulHit ? 1f - burst * burst : 1f;   // 끝에서 빠르게 사라진다

                tone.a *= alpha * fade;

                // **터지는 방향이 공격 방향을 따른다.** 셋 다 똑같이 부풀기만 하면 무엇으로
                // 깼는지가 결과에 남지 않는다. 가로베기는 좌우로 늘어나며 찢어지고, 세로베기는
                // 위아래로, 찌르기는 사방으로 고르게 퍼진다. 기호(↔ ↕ ⊙)와 같은 방향이다.
                Vector3 spread = BurstSpread(note.Type, burst);

                rhythmNotes[i].localScale = new Vector3(
                    noteScale * pop * spread.x * (mine ? 1f : .92f),
                    noteScale * pop * spread.y * (mine ? 1f : .92f),
                    1f);

                if (i < rhythmNoteFills.Length && rhythmNoteFills[i] != null)
                {
                    Image disc = rhythmNoteFills[i];

                    // 프리팹의 꽉 찬 원판 대신 코드로 그린 링을 쓴다. 가운데 알파 = 0.
                    if (disc.sprite != RingSprite())
                    {
                        disc.sprite = RingSprite();
                        disc.type = Image.Type.Simple;
                    }

                    disc.color = tone;
                }

                // **링 바깥에 글로우를 깐다.** 밝은 해변·바다 위에서 얇은 단색 링은 묻힌다.
                // 링을 두껍게 하면 기호보다 링이 먼저 읽히므로, 링은 7px 그대로 두고
                // 바깥에만 빛을 번지게 한다. 맞는 순간에는 더 세게 번진다.
                DrawNoteGlow(i, note, burst, tone, noteScale, alpha, new Vector2(x, y));

                // ⚠ 잔상(위쪽 링 두 개)은 뺐다. 링 + 글로우 + 잔상 2 가 겹쳐 "동그라미가 너무
                //    많아 헷갈린다" 는 실측 지적. 노트 하나 = 링 하나 + 기호 하나로 돌린다.
                HideNoteTrail(i);

                if (i < rhythmNoteGlyphs.Length && rhythmNoteGlyphs[i] != null)
                {
                    // 화살표 위, 키 글자 아래. 키는 화살표의 55% 로 작게 둬 주인공을 뺏지 않는다.
                    // 둘 다 한 TMP 안에 넣어 노트 프리팹을 건드리지 않는다.
                    // 키 글자(J/K/L)는 노트에서 뺐다 — 하단 카드가 이미 말해 주고, 두 줄이 되면
                    // 링 안이 붐빈다. 노트는 기호 하나만 크게.
                    Set(rhythmNoteGlyphs[i], Glyph(note.Type));

                    // **기호가 이 노트의 주인공이다.** 링을 얇게 줄인 만큼 기호를 키우고 굵게 해
                    // 읽는 순서를 기호 → 링 → 트랙으로 돌린다.
                    rhythmNoteGlyphs[i].fontStyle = FontStyles.Bold;

                    // ⚠ 두 줄이 되었으므로 글자 상자를 넘어간다. 줄바꿈을 막고 넘쳐도 그리게
                    //    두지 않으면 아래 줄(키 글자)이 통째로 잘린다.
                    rhythmNoteGlyphs[i].textWrappingMode = TextWrappingModes.NoWrap;
                    rhythmNoteGlyphs[i].overflowMode = TextOverflowModes.Overflow;
                    rhythmNoteGlyphs[i].alignment = TextAlignmentOptions.Center;
                    rhythmNoteGlyphs[i].lineSpacing = -22f;   // 두 줄을 링 안으로 당겨 붙인다

                    if (rhythmNoteGlyphs[i].fontSize < GlyphFontSize)
                    {
                        rhythmNoteGlyphs[i].fontSize = GlyphFontSize;
                        rhythmNoteGlyphs[i].fontSizeMax = GlyphFontSize;
                    }

                    // **맞는 순간 기호도 같이 반응한다.** 링만 터지면 어떤 노트를 벤 것인지
                    // 결과에 남지 않는다. 짧게 커졌다가 링과 함께 옅어진다.
                    RectTransform glyphRect = rhythmNoteGlyphs[i].rectTransform;
                    glyphRect.localScale = Vector3.one * (1f + burst * .55f);

                    // **기호도 링과 같은 색이다.** 흰색으로 두었더니 정작 제일 먼저 보는 것이
                    // 무채색이라 색으로 종류를 읽을 단서가 얇은 링 하나뿐이었다.
                    // 다만 바다 위에서 파랑이 묻히지 않도록 흰색을 조금 섞어 한 단계 밝게 쓴다.
                    // 0.25 로는 파랑 노트가 바다색에 묻혔다. 색은 링이 이미 쥐고 있으므로
                    // 기호는 <b>읽히는 것</b>이 먼저다 — 흰색 쪽으로 더 끌어올린다.
                    // .55 는 밝은 하늘 위에서 흰색에 가까워 색이 죽었다. 외곽선(남색)이 있으니
                    // 채도를 살려 링과 같은 색으로 읽히게 한다.
                    Color glyphTone = Color.Lerp(tone, Color.white, .25f);
                    glyphTone.a = alpha * fade;
                    rhythmNoteGlyphs[i].color = glyphTone;

                    // **기호에 어두운 외곽선을 준다.** 밝은 색 기호가 밝은 하늘 위에 놓이면 색을
                    // 아무리 조절해도 한쪽 배경에서는 묻힌다. 어두운 테가 있어야 하늘·모래·바다
                    // 어디에서나 읽힌다. TMP SDF 외곽선이라 폰트 에셋을 건드리지 않는다.
                    if (rhythmNoteGlyphs[i].outlineWidth < .2f)
                    {
                        rhythmNoteGlyphs[i].outlineWidth = .28f;
                        rhythmNoteGlyphs[i].outlineColor = new Color32(6, 18, 40, 255);
                    }
                }

                // **베인 노트는 조각이 되어 날아간다.** 늘어나며 옅어지기만 해서는
                // "사라졌다" 로 읽히지 터졌다로 읽히지 않는다.
                DrawNoteShards(i, note, burst, tone, noteScale, alpha, new Vector2(x, y));
            }
        }

        /// <summary>
        /// 베인 노트가 <b>조각으로 터지는</b> 연출. 링·기호를 늘리는 것(BurstSpread)에 더해
        /// 실제로 파편이 바깥으로 날아간다.
        ///
        /// 파편은 노트와 같은 <see cref="RingSprite"/> 를 아주 작게 쓴다. 가운데가 알파 0 인
        /// 링이라 <b>원판이 생기지 않는다</b> — 노트 규칙(링과 기호만, 중앙 알파 0)을 그대로 지킨다.
        ///
        /// 날아가는 방향이 공격 종류를 따른다. <see cref="BurstSpread"/> 와 같은 축이다.
        ///   ↔ 가로베기  좌우로
        ///   ↕ 세로베기  위아래로
        ///   ⊙ 찌르기    사방으로
        /// </summary>
        /// <summary>
        /// 노트 링 **바깥으로 번지는 빛.** 링 자체는 건드리지 않는다.
        ///
        /// 가운데는 글로우 스프라이트도 알파 0 이라 원판이 생기지 않는다.
        /// 맞는 순간(<paramref name="burst"/> &gt; 0)에는 한 번 크게 번졌다가 사라진다.
        /// </summary>
        private void DrawNoteGlow(
            int index,
            WarriorsRhythmNoteView note,
            float burst,
            Color tone,
            float noteScale,
            float alpha,
            Vector2 centre)
        {
            RectTransform glow = EnsureNoteGlow(index);
            if (glow == null) return;

            glow.gameObject.SetActive(true);

            // 글로우는 노트보다 조금 크다. 맞으면 한 번 더 부푼다.
            float swell = 1f + burst * .9f;
            float fade = burst > 0f ? 1f - burst * burst : 1f;

            glow.anchoredPosition = centre;
            glow.localScale = Vector3.one * (noteScale * NoteGlowScale * swell);

            Image image = glow.GetComponent<Image>();
            if (image == null) return;

            Color glowTone = tone;

            // 평소에는 은은하게, 맞는 순간에는 확 밝아진다.
            glowTone.a = alpha * fade * Mathf.Lerp(NoteGlowAlpha, 1f, burst);
            image.color = glowTone;
        }

        /// <summary>노트 하나가 쓸 글로우를 만들어 둔다. 링보다 뒤에 그려야 한다.</summary>
        private RectTransform EnsureNoteGlow(int index)
        {
            if (rhythmNotes == null || index < 0 || index >= rhythmNotes.Length) return null;
            if (rhythmNotes[index] == null) return null;

            noteGlows ??= new RectTransform[rhythmNotes.Length];
            if (noteGlows.Length != rhythmNotes.Length) System.Array.Resize(ref noteGlows, rhythmNotes.Length);
            if (noteGlows[index] != null) return noteGlows[index];

            // ⚠ 노트의 자식으로 달지 않는다. 노트는 BurstSpread 로 납작하게 눌리므로
            //    자식인 글로우까지 같이 찌그러진다. 파편(DrawNoteShards)과 같은 이유다.
            RectTransform parent = rhythmNotes[index].parent as RectTransform;
            if (parent == null) return null;

            GameObject piece = new($"NoteGlow_{index}", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)piece.transform;

            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(NoteGlowPixels, NoteGlowPixels);

            // 링보다 뒤에 그린다. 노트 앞에 오면 기호가 뿌옇게 덮인다.
            rect.SetSiblingIndex(rhythmNotes[index].GetSiblingIndex());

            Image image = piece.GetComponent<Image>();
            image.sprite = GlowRingSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            noteGlows[index] = rect;
            return rect;
        }

        /// <summary>
        /// 노트가 지나온 자리에 남는 **아주 짧은 자국.**
        ///
        /// 노트가 등속으로 내려오면 화면에서는 "위치가 바뀌는 아이콘" 으로만 보이고
        /// <b>위에서 아래로 흐른다</b>는 방향이 약하다. 뒤(위쪽)에 두 토막을 옅게 남기면
        /// 같은 속도라도 흐름이 읽힌다.
        ///
        /// ⚠ 꼬리를 길게 끌지 않는다. 길면 트랙이 지저분해지고 다음 노트를 가린다.
        ///   낙하 거리(540px)의 약 5%, 10% 두 곳까지만 남긴다.
        /// </summary>
        private void DrawNoteTrail(
            int index,
            WarriorsRhythmNoteView note,
            float burst,
            Color tone,
            float noteScale,
            float alpha,
            Vector2 centre)
        {
            // 베인 뒤에는 자국을 남기지 않는다 — 터지는 연출과 겹쳐 지저분해진다.
            // 막 생긴 노트도 제외한다. 아직 움직인 거리가 없다.
            if (burst > 0f || note.IsSuccessfulHit || note.IsMissed || note.Travel <= .06f)
            {
                HideNoteTrail(index);
                return;
            }

            RectTransform[] trail = EnsureNoteTrail(index);
            if (trail == null) return;

            for (int t = 0; t < trail.Length; t++)
            {
                if (trail[t] == null) continue;

                float back = TrailStepPixels * (t + 1);

                trail[t].gameObject.SetActive(true);
                trail[t].anchoredPosition = centre + Vector2.up * back;
                trail[t].localScale = Vector3.one * (noteScale * (.72f - t * .18f));

                Image piece = trail[t].GetComponent<Image>();
                if (piece == null) continue;

                Color trailTone = tone;
                trailTone.a = alpha * TrailAlpha * (1f - t * .45f);
                piece.color = trailTone;
            }
        }

        private RectTransform[] EnsureNoteTrail(int index)
        {
            if (rhythmNotes == null || index < 0 || index >= rhythmNotes.Length) return null;
            if (rhythmNotes[index] == null) return null;

            noteTrails ??= new RectTransform[rhythmNotes.Length][];
            if (noteTrails.Length != rhythmNotes.Length) System.Array.Resize(ref noteTrails, rhythmNotes.Length);
            if (noteTrails[index] != null) return noteTrails[index];

            RectTransform parent = rhythmNotes[index].parent as RectTransform;
            if (parent == null) return null;

            RectTransform[] pieces = new RectTransform[TrailPieces];

            for (int t = 0; t < pieces.Length; t++)
            {
                GameObject piece = new($"NoteTrail_{index}_{t}", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)piece.transform;

                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(NoteGlowPixels, NoteGlowPixels);

                // 노트보다 뒤에 그린다.
                rect.SetSiblingIndex(rhythmNotes[index].GetSiblingIndex());

                Image image = piece.GetComponent<Image>();
                image.sprite = RingSprite();
                image.type = Image.Type.Simple;
                image.raycastTarget = false;

                piece.SetActive(false);
                pieces[t] = rect;
            }

            noteTrails[index] = pieces;
            return pieces;
        }

        private void HideNoteTrail(int index)
        {
            if (noteTrails == null || index < 0 || index >= noteTrails.Length) return;

            RectTransform[] pieces = noteTrails[index];
            if (pieces == null) return;

            for (int t = 0; t < pieces.Length; t++)
                if (pieces[t] != null && pieces[t].gameObject.activeSelf)
                    pieces[t].gameObject.SetActive(false);
        }

        private void HideNoteGlow(int index)
        {
            if (noteGlows == null || index < 0 || index >= noteGlows.Length) return;
            if (noteGlows[index] != null && noteGlows[index].gameObject.activeSelf)
                noteGlows[index].gameObject.SetActive(false);
        }

        private void DrawNoteShards(
            int index,
            WarriorsRhythmNoteView note,
            float burst,
            Color tone,
            float noteScale,
            float alpha,
            Vector2 centre)
        {
            // 아직 베이지 않았거나 다 흩어졌으면 조각이 없다.
            if (burst <= 0f || burst >= 1f || !note.IsSuccessfulHit)
            {
                HideNoteShards(index);
                return;
            }

            RectTransform[] shards = EnsureNoteShards(index);
            if (shards == null) return;

            // 앞에서 빠르게 튀어나가고 뒤에서 느려진다 — 터진 뒤 흩어지는 움직임이다.
            float travel = 1f - (1f - burst) * (1f - burst);
            float shrink = Mathf.Lerp(1f, .35f, burst);
            float shardFade = 1f - burst * burst;

            for (int s = 0; s < shards.Length; s++)
            {
                if (shards[s] == null) continue;

                Vector2 direction = ShardDirection(note.Type, s, shards.Length);

                shards[s].gameObject.SetActive(true);
                shards[s].anchoredPosition = centre + direction * (ShardFlyPixels * travel * noteScale);
                shards[s].localScale = Vector3.one * (noteScale * shrink);

                Image piece = shards[s].GetComponent<Image>();
                if (piece == null) continue;

                Color pieceTone = Color.Lerp(tone, Color.white, .35f);
                pieceTone.a = alpha * shardFade;
                piece.color = pieceTone;
            }
        }

        /// <summary>파편 하나가 날아가는 방향. 공격 종류가 축을 정한다.</summary>
        private static Vector2 ShardDirection(WarriorsAttackDirection type, int shard, int count)
        {
            switch (type)
            {
                // 가로베기 — 좌우로 갈라진다. 위아래로는 조금만 흩어진다.
                case WarriorsAttackDirection.HorizontalSlash:
                {
                    float side = (shard % 2 == 0) ? 1f : -1f;
                    float spread = ((shard / 2) - 1) * .28f;
                    return new Vector2(side, spread).normalized;
                }

                // 세로베기 — 위아래로 갈라진다.
                case WarriorsAttackDirection.VerticalSlash:
                {
                    float side = (shard % 2 == 0) ? 1f : -1f;
                    float spread = ((shard / 2) - 1) * .28f;
                    return new Vector2(spread, side).normalized;
                }

                // 찌르기 — 방향이 없으므로 사방으로 고르게 터진다.
                default:
                {
                    float angle = Mathf.PI * 2f * shard / Mathf.Max(1, count);
                    return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
        }

        /// <summary>
        /// 노트 하나가 쓸 파편을 만들어 둔다. 판마다 한 번만 만들고 그 뒤로는 껐다 켜기만 한다 —
        /// 리듬 구간에서 매 프레임 <c>new</c> 를 하면 GC 가 튄다.
        /// </summary>
        private RectTransform[] EnsureNoteShards(int index)
        {
            if (rhythmNotes == null || index < 0 || index >= rhythmNotes.Length) return null;
            if (rhythmNotes[index] == null) return null;

            noteShards ??= new RectTransform[rhythmNotes.Length][];
            if (noteShards.Length != rhythmNotes.Length) System.Array.Resize(ref noteShards, rhythmNotes.Length);
            if (noteShards[index] != null) return noteShards[index];

            // ⚠ 노트의 자식으로 달면 안 된다. 노트는 BurstSpread 로 <b>납작하게 눌리므로</b>
            //    (가로 2.6배 · 세로 0.18배) 자식인 파편까지 같이 찌그러진다. 트랙에 직접 단다.
            RectTransform parent = rhythmNotes[index].parent as RectTransform;
            if (parent == null) return null;

            RectTransform[] shards = new RectTransform[ShardsPerNote];

            for (int s = 0; s < shards.Length; s++)
            {
                GameObject piece = new($"NoteShard_{index}_{s}", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)piece.transform;

                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(ShardPixels, ShardPixels);

                Image image = piece.GetComponent<Image>();
                image.sprite = RingSprite();
                image.type = Image.Type.Simple;
                image.raycastTarget = false;

                piece.SetActive(false);
                shards[s] = rect;
            }

            noteShards[index] = shards;
            return shards;
        }

        private void HideNoteShards(int index)
        {
            if (noteShards == null || index < 0 || index >= noteShards.Length) return;

            RectTransform[] shards = noteShards[index];
            if (shards == null) return;

            for (int s = 0; s < shards.Length; s++)
                if (shards[s] != null && shards[s].gameObject.activeSelf)
                    shards[s].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------
        // 상황 안내 한 줄 — 런타임에 만든다
        // ------------------------------------------------------------
        //
        // ⚠ 예전에는 여기에 **협동 게이지 막대**가 있었다. 게이지가 규칙에서 빠져
        //   막대는 지웠고, 안내 문구 한 줄만 남았다.

        private RectTransform noticeRoot;
        private TMP_Text noticeLabel;

        /// <summary>
        /// 지금 무슨 일이 일어났는지 알리는 한 줄. 쓰러짐과 2라운드 마무리 창이 여기로 나온다.
        ///
        /// ⚠ <b>프리팹을 고치지 않고 코드로 만든다.</b> 이 HUD 프리팹은 혼자 하는 씬도 함께 쓰는데,
        ///    거기에는 이 안내가 필요 없다. 네트워크 판에서만 켜져야 한다.
        /// </summary>
        private void UpdateNoticeHud(bool isFinalOverlay)
        {
            bool show = NetworkMatchActive && !isFinalOverlay;

            EnsureNoticeHud();

            if (noticeRoot == null) return;

            SetActive(noticeRoot.gameObject, show);
            if (!show) return;

            // 쓰러짐 안내가 먼저다. 그다음이 2라운드 마무리 창 신호다.
            string notice = !string.IsNullOrEmpty(NetworkDownNotice) ? NetworkDownNotice : NetworkFinishWindowNotice;
            bool hasNotice = !string.IsNullOrEmpty(notice);

            if (noticeLabel != null)
            {
                SetActive(noticeLabel.gameObject, hasNotice);

                if (hasNotice)
                {
                    Set(noticeLabel, notice);

                    // 쓰러짐은 주황, 마무리 창 신호는 청록으로 구분한다.
                    noticeLabel.color = !string.IsNullOrEmpty(NetworkDownNotice)
                        ? new Color(1f, .72f, .35f, 1f)
                        : new Color(.45f, .95f, .85f, 1f);
                }
            }
        }

        /// <summary>
        /// 노트 기호(↔ ↕ ⊙)의 글자 크기.
        ///
        /// 링을 바깥 반지름의 11% 로 얇게 줄였으므로, 노트에서 눈에 먼저 들어오는 것은
        /// 기호여야 한다. 노트 지름이 화면에서 72~117px 이라 44 면 안쪽을 넉넉히 채운다.
        /// </summary>
        private const float GlyphFontSize = 44f;

        /// <summary>안내 줄의 기준 너비(px). 실제 글자 상자는 여기에 여유를 더해 쓴다.</summary>
        private const float NoticeWidth = 360f;

        private void EnsureNoticeHud()
        {
            if (noticeRoot != null || rhythmNotes == null || rhythmNotes.Length == 0 || rhythmNotes[0] == null) return;

            RectTransform parent = rhythmNotes[0].parent as RectTransform;
            if (parent == null) return;

            // 자리: **보스 HP 카드 아래, 노트가 생기는 높이 위.**
            //
            // 처음에는 화면 아래(-368)에 뒀는데 거기는 두 캐릭터와 크라켄 몸이 서 있는 자리라
            // 막대와 글자가 캐릭터에 묻혔다. 보스 카드는 화면 중앙 기준 y +406~+512 를 쓰고
            // 노트는 +290 에서 생기므로, 그 사이 +290~+406 이 비어 있다.
            // 중심을 +348 에 두면(로컬 308 = 화면 348 - NoteTrack 오프셋 40) 76 높이가
            // +310~+386 에 들어가 어느 쪽과도 겹치지 않는다. 보스 체력 바로 아래라
            // "팀 상태" 를 읽는 자리로도 맞다.
            noticeRoot = MakeRect(parent, "NoticeHud", new Vector2(NoticeWidth, 40f), new Vector2(0f, 308f));

            noticeLabel = MakeNoticeText(noticeRoot, "Notice", 26f, Vector2.zero);

            if (noticeLabel != null) noticeLabel.color = new Color(1f, .72f, .35f, 1f);
        }

        private static RectTransform MakeRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject made = new GameObject(name, typeof(RectTransform));
            made.transform.SetParent(parent, false);

            RectTransform rect = made.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return rect;
        }

        private TMP_Text MakeNoticeText(Transform parent, string name, float size, Vector2 position)
        {
            RectTransform rect = MakeRect(parent, name, new Vector2(NoticeWidth + 240f, 32f), position);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (roundText != null) text.font = roundText.font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.85f, .95f, 1f, .95f);
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// **노트가 내려오는 세로 트랙.** 사람마다 하나씩, 런타임에 만든다.
        ///
        /// 노트와 판정선만 있으면 "아이콘이 떠 있다" 로 보이고 리듬게임으로 읽히지 않는다.
        /// 실제 2인 플레이 영상에서 레인 구조가 전혀 전달되지 않았다. 옅은 세로 띠 하나가
        /// 들어가면 "이 줄을 따라 내려와 선에 닿는다" 가 한눈에 보인다.
        ///
        /// ⚠ 프리팹을 고치지 않고 코드로 만든다. 이 HUD 프리팹은 혼자 하는 씬도 함께 쓰고,
        ///    거기에 레인을 박아 두면 리듬 라운드가 아닐 때도 따라다닌다.
        ///
        /// 노트보다 뒤에 두어야 노트를 가리지 않는다. 순서는 만들 때 한 번만 정한다 —
        /// 매 프레임 <c>SetAsFirstSibling</c> 을 부르면 둘의 앞뒤가 프레임마다 뒤집힌다.
        /// </summary>
        private void UpdateLaneTracks(int playerCount, float spawnY, float hitLineY)
        {
            if (rhythmNotes == null || rhythmNotes.Length == 0 || rhythmNotes[0] == null) return;

            RectTransform parent = rhythmNotes[0].parent as RectTransform;
            if (parent == null) return;

            while (laneVisuals.Count < playerCount)
            {
                int index = laneVisuals.Count;

                laneVisuals.Add(new LaneVisual
                {
                    fill = MakeLanePiece(parent, $"RhythmLane{index}Fill"),
                    columns = new[]
                    {
                        MakeLanePiece(parent, $"RhythmLane{index}Col0"),
                        MakeLanePiece(parent, $"RhythmLane{index}Col1"),
                        MakeLanePiece(parent, $"RhythmLane{index}Col2"),
                    },
                    leftEdge = MakeLanePiece(parent, $"RhythmLane{index}Left"),
                    rightEdge = MakeLanePiece(parent, $"RhythmLane{index}Right"),

                    // ⚠ 글로우를 판정선보다 **먼저** 만든다. 형제 순서가 곧 그리는 순서라,
                    //    나중에 만들면 글로우가 선 위를 덮어 선이 흐려 보인다.
                    // ⚠ MakeLanePiece 는 SetAsFirstSibling 이라 **먼저 만든 것이 위에** 그려진다.
                    //    심지(core)가 판정선 위에 보여야 하므로 판정선보다 먼저 만든다.
                    core = MakeLanePiece(parent, $"RhythmLane{index}Core"),
                    judgeGlow = MakeLanePiece(parent, $"RhythmLane{index}JudgeGlow"),
                    judge = MakeLanePiece(parent, $"RhythmLane{index}Judge"),
                    label = MakeLaneLabel(parent, $"RhythmLane{index}Label"),
                });
            }

            // 공용 판정선은 그리지 않는다. 두 레인을 가로지르는 긴 선 하나는 "각자의 레인" 이 아니라
            // 공용 타이밍 바처럼 보였다. 자리(Y)를 재는 기준으로만 남기고 그림은 끈다.
            if (rhythmHitLine != null)
            {
                Image shared = rhythmHitLine.GetComponent<Image>();
                if (shared != null && shared.enabled) shared.enabled = false;
            }

            // ⚠ **프리팹 NoteTrack 의 자기 그림(150×560 · 회색 24%)과 옛 HitLine(180×5 · 금색)을 끈다.**
            //    실측 스크린샷에서 크라켄 머리 위에 떠 있던 "회색 세로 사각형과 가로 선" 이 이것이다.
            //    레인은 여기서 만드는 조각들이 그리므로 프리팹 그림은 남을 이유가 없다.
            Image trackBack = parent.GetComponent<Image>();
            if (trackBack != null && trackBack.enabled) trackBack.enabled = false;

            Transform oldHitLine = parent.Find("HitLine");
            if (oldHitLine != null && oldHitLine.gameObject.activeSelf) oldHitLine.gameObject.SetActive(false);

            laneCentres.Clear();

            for (int i = 0; i < laneVisuals.Count; i++)
            {
                LaneVisual lane = laneVisuals[i];
                // 쓰러진 사람의 줄은 지운다. 그 레인에는 노트가 나오지 않으므로(서버가 막는다)
                // 빈 트랙만 남으면 고장난 것처럼 보인다.
                bool down = (NetworkDownLanes & (1 << i)) != 0;
                // ⚠ 쓰러진 사람의 레인도 **남긴다(흐리게).** 지우면 남은 레인 하나가 한쪽에 떠
                //    "누구 줄인지 모르는 큰 도형" 이 됐다(실측). 구조는 두 줄로 고정하고 상태만 흐리게.
                bool used = i < playerCount;

                lane.fill.gameObject.SetActive(used);
                lane.leftEdge.gameObject.SetActive(used);
                lane.rightEdge.gameObject.SetActive(used);
                lane.judgeGlow.gameObject.SetActive(used);
                lane.judge.gameObject.SetActive(used);
                lane.core.gameObject.SetActive(used);
                lane.label.gameObject.SetActive(used);
                foreach (RectTransform column in lane.columns) column.gameObject.SetActive(used);

                if (!used) continue;

                float x = TrackCentre(i, playerCount);
                laneCentres.Add(x);

                bool mine = !NetworkMatchActive || NetworkLocalLane < 0 || i == NetworkLocalLane;
                float dim = mine ? 1f : .55f;
                if (down) dim *= .35f;

                float bottom = TrackHalfWidthBottom;
                float top = bottom * TrackTopScale;

                // **트랙 색은 사람마다 다르다.** 1P 청록 · 2P 금색.
                // 두 트랙이 같은 색이면 화면 가운데를 기준으로 좌우가 거울처럼 보여, 자기 줄을
                // 구별하는 단서가 위치 하나뿐이다. 색이 다르면 눈이 먼저 자기 줄을 찾는다.
                Color tint = TrackTint(i);

                // 바닥은 거의 투명하다. 사다리꼴이라 사각형 하나로는 못 그리므로, 가운데를 채우는 대신
                // 가장자리 두 줄로 "길" 을 만든다. 얇게 깔린 바닥은 방향만 거든다.
                //
                // **트랙보다 노트가 먼저 보여야 한다.** 예전 값(바닥 .10 · 가장자리 .85)은
                // 청록/금색 외곽선이 또렷해 화면에서 제일 먼저 눈에 들어왔고, 개발용 와이어프레임처럼
                // 보였다. 둘 다 크게 낮춰 트랙은 "길이 있다" 정도만 거들게 한다.
                // ⚠ **밝은 해변·하늘 배경에서 다시 눌러졌다.** 트랙 색 자체(TrackTint)를
                //    청회색 파스텔에서 진하게 바꾼 뒤에도, 여기 알파들이 예전 값(.05/.52/.09)
                //    그대로라 화살표·선이 여전히 흐린 자국으로만 보였다. 실측 스크린샷으로
                //    확인하고 한 번 더 올린다.
                // 바닥은 사람 색이 아니라 **HUD 남색 유리**다. 주황/청록 면이 넓게 깔리면 보스·배경과
                // 섞여 "색 덩어리" 로 보였다(실측). 어두운 반투명 기둥이면 HUD 판과 같은 계열로 읽힌다.
                Color floorColor = new Color(.02f, .06f, .14f, .30f * dim);

                // 바닥은 **사다리꼴 스프라이트**를 아래 폭 × 높이로 깐다. 스프라이트의 위 폭 비율이
                // TrackTopScale 이라 어떤 크기로 늘려도 가장자리 선(같은 비율)과 정확히 겹친다.
                PaintSprite(lane.fill, TrapezoidSprite(), new Vector2(bottom * 2f, spawnY - hitLineY),
                    new Vector2(x, (spawnY + hitLineY) * .5f), floorColor);

                // **양쪽 가장자리 — 위가 좁고 아래가 넓게 기울인다.** 이 기울기가 원근을 만든다.
                // 가장자리는 사람 색을 **흰색 쪽으로 반쯤 섞어** 얇게. 진한 주황 두 줄이 "주황 프레임
                // 덩어리" 로 읽혔다(실측). 색은 판정선이 쥐고, 가장자리는 경계만 알린다.
                Color edgeColor = Color.Lerp(tint, Color.white, .45f);
                edgeColor.a = .75f * dim;

                PaintSegment(lane.leftEdge, x - top, spawnY, x - bottom, hitLineY, 5f, edgeColor);
                PaintSegment(lane.rightEdge, x + top, spawnY, x + bottom, hitLineY, 5f, edgeColor);

                // 트랙을 세로로 나누는 **옅은 구분선 2줄.** 길이 흐르는 방향만 거들 뿐,
                // 공격 종류와는 상관이 없다 — 종류는 노트의 색과 기호가 말한다.
                Color divider = Color.white;
                divider.a = .14f * dim;

                for (int c = 0; c < lane.columns.Length; c++)
                {
                    // 3등분하는 두 줄. 남는 하나는 쓰지 않는다.
                    if (c >= 2) { lane.columns[c].gameObject.SetActive(false); continue; }

                    float side = c == 0 ? -1f : 1f;
                    float third = 1f / 3f;

                    PaintSegment(lane.columns[c],
                        x + side * top * third, spawnY,
                        x + side * bottom * third, hitLineY, 2f, divider);
                }

                // **판정선은 두 줄 모두 같은 색(청록빛 흰색)이다.**
                //
                // 예전에는 트랙 색을 그대로 써서 1P 청록 · 2P 금색이었다. 그런데 노트의
                // 파랑·빨강·노랑이 곧 공격 종류라, 2P 의 금색 판정선이 "찌르기(노랑)" 와
                // 같은 계열로 읽혔다. 화면에서 <b>색의 의미는 공격 종류 하나</b>여야 한다.
                // 누가 누구 줄인지는 좌우 위치가 이미 말해 준다.
                // 판정선은 **그 레인의 색**이다. 실측 지적 — 레인이 주황인데 판정선이 청록이면
                // 어느 줄의 선인지 한 번 더 읽어야 한다. 공격 색(파랑·빨강·노랑)과 겹치지 않게
                // 레인 색 자체를 청록·주황으로 두었으므로 판정선이 따라가도 뜻이 안 섞인다.
                Color judgeColor = tint;
                judgeColor.a = 1f * dim;

                // **정타가 나면 그 줄의 판정선만 번쩍인다.** 노트가 터지는 것은 노트가 있던
                // 자리에서 일어나므로, 정작 "선에 맞췄다" 는 것은 선 자체가 말해 줘야 한다.
                // 맞은 줄 하나만 반응해야 누가 맞췄는지 읽힌다.
                // 굵기를 14 → 24 로 올린다(정타 24 → 38). 실측으로 판정선 폭은 434px 이고
                // 트랙 하단 폭 414px 의 105% 라 폭은 이미 충분했다 — 부족했던 것은 <b>두께</b>다.
                // 1080p 에서 14px 은 밝은 바다 위에서 선이라기보다 흐린 자국으로 보였다.
                // 24px 막대 + 46 글로우는 넓은 레인 바닥과 합쳐져 "큰 베기 잔상" 으로 읽혔다(실측).
                // 판정선은 **짧고 얇고 또렷**하게 — 14px 막대, 30 글로우. 정타 때만 잠깐 굵어진다.
                float lineHeight = 14f;
                float glowHeight = 30f;
                float glowAlpha = .30f;

                if (NetworkRhythmPulseLane == i)
                {
                    judgeColor = Color.Lerp(judgeColor, Color.white, .75f);
                    judgeColor.a = 1f;
                    lineHeight = 22f;
                    glowHeight = 52f;
                    glowAlpha = .6f;
                }

                // 폭은 트랙 하단의 95%. 예전에는 +20px 이라 트랙보다 넓은 105% 였고,
                // 선이 레인 밖으로 삐져나와 "이 레인의 기준선" 이 아니라 화면을 가로지르는
                // 막대처럼 보였다. 살짝 안쪽으로 들이면 레인에 속한 선으로 읽힌다.
                // 글로우는 트랙 폭까지 번지므로 좁아 보이지 않는다.
                float lineWidth = bottom * 2f * .95f;

                // **글로우를 선 뒤에 먼저 깐다.** 어두운 패널을 까는 대신 선만 빛나게 하는 방법이다.
                // 위아래로 흐려지는 띠라 배경을 가리지 않으면서 선이 떠 보인다.
                Color glowColor = tint;
                glowColor.a = glowAlpha * dim;

                PaintSprite(lane.judgeGlow, GlowBarSprite(),
                    new Vector2(lineWidth + 40f, glowHeight), new Vector2(x, hitLineY), glowColor);

                // **판정선은 둥근 막대 + 밝은 심지.** 납작한 단색 사각형은 "그림판으로 그은 선" 으로
                // 보였다(실측 지적). 양 끝을 둥글게 하고 가운데에 흰 심지를 넣으면 레인의 바닥을
                // 이루는 발광 막대로 읽힌다. 트랙 가장자리 선이 이 막대의 중심선에서 끝나 한 몸이다.
                PaintSprite(lane.judge, PillSprite(), new Vector2(lineWidth, lineHeight), new Vector2(x, hitLineY), judgeColor);

                Color coreColor = Color.Lerp(judgeColor, Color.white, .85f);
                coreColor.a = (NetworkRhythmPulseLane == i ? 1f : .9f) * dim;
                Paint(lane.core, new Vector2(lineWidth * .94f, 4f), new Vector2(x, hitLineY), coreColor);

                // "P1" · "P2" 글자는 두지 않는다. 캐릭터가 좌우에 서 있고 트랙도 좌우로 갈려 있어
                // 글자로 한 번 더 알려 줄 이유가 없다. 트랙 색이 이미 두 사람을 구분한다.
                lane.label.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 이 레인의 화면상 중심. **캐릭터 자리를 따라가지 않는다.**
        ///
        /// 혼자면 가운데, 둘이면 0번이 왼쪽 · 1번이 오른쪽으로 고정이다.
        /// </summary>
        private static float TrackCentre(int lane, int playerCount)
        {
            if (playerCount <= 1) return 0f;
            return lane == 0 ? -TrackCentreX : TrackCentreX;
        }

        /// <summary>
        /// 두 점을 잇는 **기울어진 선**을 그린다. 가운데에 놓고 길이만큼 늘린 뒤 각도만큼 돌린다.
        ///
        /// 사다리꼴 트랙의 가장자리와 열 안내선은 세로가 아니라 비스듬하므로 사각형으로는 못 그린다.
        /// </summary>
        private static void PaintSegment(
            RectTransform rect, float fromX, float fromY, float toX, float toY, float thickness, Color color)
        {
            Vector2 from = new Vector2(fromX, fromY);
            Vector2 to = new Vector2(toX, toY);
            Vector2 delta = to - from;
            float length = delta.magnitude;

            rect.sizeDelta = new Vector2(thickness, length);
            rect.anchoredPosition = (from + to) * .5f;

            // 기본이 세로 막대이므로, 세로축(0,1)에서 실제 방향까지 돌린 각을 쓴다.
            rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg);

            Image image = rect.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        /// <summary>
        /// 트랙 색. **두 레인이 거의 같은 색이다.**
        ///
        /// 예전에는 1P 청록 · 2P 금색으로 뚜렷하게 갈랐는데, 그 금색이 찌르기(노랑)와
        /// 같은 계열로 읽혀 <b>색의 의미가 둘</b>이 됐다. 화면에서 색이 말하는 것은
        /// 공격 종류 하나여야 한다. 사람 구분은 좌우 위치가 이미 하고 있으므로,
        /// 트랙은 옅은 청회색 한 가지로 두고 2P 만 아주 살짝 따뜻하게 기울인다.
        /// </summary>
        // ⚠ **파스텔 톤이 밝은 해변·하늘 배경에 묻혔다.** 원래 값(.62,.78,.92 / .78,.80,.88)은
        //    거의 흰색에 가까워 실측 스크린샷에서 트랙이 거의 안 보였다. 채도를 크게 올린다 —
        //    1P 는 진한 청록, 2P 는 진한 호박색으로 배경과 확실히 갈린다.
        private static Color TrackTint(int lane) => lane == 0
            ? new Color(.05f, .55f, .85f, 1f)
            : new Color(.95f, .60f, .10f, 1f);

        /// <summary>
        /// 판정선 색. <b>두 줄 모두 같다.</b>
        ///
        /// 공격 종류(파랑·빨강·노랑)와 섞이지 않는 청록빛 흰색이다.
        /// 화면에서 색이 뜻하는 것은 공격 종류 하나여야 한다.
        ///
        /// ⚠ 원래 값(.82,.96,1)은 거의 흰색이라 밝은 하늘·모래와 대비가 없었다.
        ///    같은 색 계열을 유지하되 훨씬 진하게 눌러 확실히 도드라지게 한다.
        /// </summary>
        private static readonly Color JudgeLineColor = new(.0f, .65f, .78f, 1f);

        /// <summary>노트에 쓰는 **가운데가 완전히 빈 링**. 한 번 만들어 계속 쓴다.</summary>
        private static Sprite ringSprite;

        /// <summary>링 바깥에 깔 **부드러운 글로우**. 가운데는 여전히 비어 있다.</summary>
        private static Sprite glowRingSprite;

        /// <summary>판정선 뒤에 깔 **위아래로 흐려지는 띠**.</summary>
        private static Sprite glowBarSprite;

        /// <summary>
        /// **링 바깥으로 번지는 글로우.** 링과 같은 자리에 더 크게 깔아 쓴다.
        ///
        /// 왜 필요한가. 배경이 밝은 해변·바다라 얇은 단색 링은 그대로 묻힌다. 링을 두껍게
        /// 하면 <b>기호보다 링이 먼저 읽히는</b> 문제가 다시 생기므로(그래서 7px 로 줄였다),
        /// 링은 얇게 두고 <b>바깥에만</b> 빛을 번지게 한다.
        ///
        /// ⚠ 안쪽은 링과 마찬가지로 <b>알파 0</b> 이다. 가운데를 채우면 원판이 되어 규칙 위반이다.
        /// </summary>
        private static Sprite GlowRingSprite()
        {
            if (glowRingSprite != null) return glowRingSprite;

            const int Size = 128;
            const float Core = 58f;    // 링이 있는 자리
            const float Outer = 62f;   // 여기까지 번진다
            const float Inner = 50f;   // 안쪽도 조금만 번지고 그 안은 0

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Vector2 centre = new Vector2(Size * .5f - .5f, Size * .5f - .5f);
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), centre);

                    // 링 자리에서 1 이고 안팎으로 부드럽게 0 이 된다.
                    float alpha = distance >= Core
                        ? Mathf.Clamp01(1f - (distance - Core) / (Outer - Core))
                        : Mathf.Clamp01(1f - (Core - distance) / (Core - Inner));

                    // 제곱해 가장자리를 더 부드럽게 — 딱딱한 테두리가 생기면 링이 두꺼워 보인다.
                    alpha *= alpha;

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            glowRingSprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(.5f, .5f), 100f);
            glowRingSprite.name = "WarriorsNoteGlow";

            return glowRingSprite;
        }

        /// <summary>
        /// **판정선 뒤에 까는 띠.** 가운데가 가장 밝고 위아래로 흐려진다.
        ///
        /// 어두운 패널을 깔지 않고 선 자체만 빛나게 하는 방법이다. 밝은 배경 위에서도
        /// 선이 "떠 있는" 것처럼 보여 타이밍 기준점이 된다.
        /// </summary>
        private static Sprite GlowBarSprite()
        {
            if (glowBarSprite != null) return glowBarSprite;

            const int Width = 8;
            const int Height = 64;

            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[Width * Height];
            float centre = Height * .5f - .5f;

            for (int y = 0; y < Height; y++)
            {
                float k = 1f - Mathf.Clamp01(Mathf.Abs(y - centre) / centre);
                byte alpha = (byte)Mathf.RoundToInt(k * k * 255f);

                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = new Color32(255, 255, 255, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            glowBarSprite = Sprite.Create(texture, new Rect(0f, 0f, Width, Height), new Vector2(.5f, .5f), 100f);
            glowBarSprite.name = "WarriorsJudgeGlow";

            return glowBarSprite;
        }

        /// <summary>
        /// **링 스프라이트를 코드로 그린다. 가운데 알파는 정확히 0 이다.**
        ///
        /// 프리팹의 노트는 <c>RoundControl.png</c> 라는 <b>꽉 찬 원판</b>을 쓰고 있었다.
        /// 그 스프라이트는 <c>spriteBorder</c> 가 (0,0,0,0) 이라 9-슬라이스가 아니어서,
        /// <c>Image.fillCenter = false</c> 로는 가운데를 비울 수 없다. 설정으로는 불가능한 구조였다.
        ///
        /// 그래서 픽셀을 직접 그린다. 중심에서의 거리가 <c>Inner</c> 보다 가까우면 알파가 0 이므로
        /// 가운데로 뒤의 크라켄과 바다가 그대로 비친다. 색은 링과 기호만 갖는다.
        /// </summary>
        /// <summary>
        /// 아래가 넓고 위가 <see cref="TrackTopScale"/> 배로 좁은 흰 사다리꼴(256×256). 레인 바닥용.
        /// 가로·세로로 따로 늘려도 "위 폭 / 아래 폭" 비율은 그대로라 가장자리 선과 늘 일치한다.
        /// </summary>
        private static Sprite TrapezoidSprite()
        {
            if (trapezoidSprite != null) return trapezoidSprite;

            const int S = 256;
            Texture2D texture = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[S * S];
            float cx = S * .5f - .5f;

            for (int y = 0; y < S; y++)
            {
                // y=0 이 아래(넓은 쪽). 위로 갈수록 반폭이 TrackTopScale 배까지 줄어든다.
                float half = cx * Mathf.Lerp(1f, TrackTopScale, y / (float)(S - 1));

                for (int x = 0; x < S; x++)
                {
                    float a = Mathf.Clamp01(half - Mathf.Abs(x - cx) + .5f);   // 가장자리 1px 만 부드럽게
                    pixels[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            trapezoidSprite = Sprite.Create(texture, new Rect(0f, 0f, S, S), new Vector2(.5f, .5f), 100f);
            trapezoidSprite.name = "WarriorsLaneTrapezoid";
            return trapezoidSprite;
        }

        private static Sprite trapezoidSprite;

        /// <summary>양 끝이 반원인 흰 막대(64×32). 판정선에 쓴다. Simple 로 늘려도 끝이 둥글게 남는다.</summary>
        private static Sprite PillSprite()
        {
            if (pillSprite != null) return pillSprite;

            const int W = 64, H = 32;
            Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[W * H];
            float r = H * .5f - .5f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float cx = Mathf.Clamp(x, r, W - 1 - r);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, r));
                    float a = Mathf.Clamp01(r + .5f - d);
                    pixels[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            pillSprite = Sprite.Create(texture, new Rect(0f, 0f, W, H), new Vector2(.5f, .5f), 100f);
            pillSprite.name = "WarriorsJudgePill";
            return pillSprite;
        }

        private static Sprite pillSprite;

        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;

            const int Size = 128;
            const float Outer = 62f;

            // **링 두께 = 62 - 55 = 7px**, 바깥 반지름의 **11.3%**.
            //
            // 두께를 26px(42%)까지 올렸더니 이번에는 반대 문제가 생겼다 — 링이 너무 굵어
            // <b>가운데 기호(↔ ↕ ⊙)보다 링이 먼저 보였다.</b> 노트에서 제일 먼저 읽혀야 하는 것은
            // "어떤 공격인가" 이고 그것을 말하는 것은 기호다. 링은 색만 거들면 된다.
            //
            // 화면에서 노트는 90px × 0.80~1.30 = 72~117px 이므로 링은 **약 4~6.4px** 로 찍힌다.
            // 얇아진 만큼 색이 죽지 않도록 채도와 알파를 아래 색 값에서 최대로 두었다.
            // 안쪽(반지름 55 이내)은 여전히 **전부 알파 0** 이다.
            // 7px(55) 은 실측에서 4~6px 로 찍혀 밝은 배경에 사라졐다. 10px(52) — 기호보다는
            // 여전히 얇지만 링으로는 읽히는 두께다. 26px 로 갔다가 되돌린 이력이 있으니 더 올리지 말 것.
            const float Inner = 48f;   // 14px — 실측에서 10px 도 연하게 보였다. 잔상을 뺐으니 링은 더 굵어도 된다.

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Vector2 centre = new Vector2(Size * .5f - .5f, Size * .5f - .5f);
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), centre);

                    // 바깥 경계와 안쪽 경계 중 가까운 쪽으로 1px 만 부드럽게. 나머지는 0 또는 1.
                    float alpha = Mathf.Clamp01(Mathf.Min(Outer - distance, distance - Inner));

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(.5f, .5f), 100f);
            ringSprite.name = "WarriorsNoteRing";

            return ringSprite;
        }

        private static TMP_Text MakeLaneLabel(Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);

            TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
            text.fontSize = 26f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>레인 조각 하나. 노트보다 뒤에 둔다.</summary>
        private static RectTransform MakeLanePiece(Transform parent, string name)
        {
            GameObject piece = new GameObject(name, typeof(RectTransform));
            piece.transform.SetParent(parent, false);

            Image image = piece.AddComponent<Image>();
            image.raycastTarget = false;

            RectTransform rect = piece.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.SetAsFirstSibling();

            return rect;
        }

        private static void Paint(RectTransform rect, Vector2 size, Vector2 position, Color color)
        {
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = rect.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        /// <summary><see cref="Paint"/> 와 같되 스프라이트까지 지정한다. 글로우처럼 모양이 있는 것에 쓴다.</summary>
        private static void PaintSprite(
            RectTransform rect, Sprite sprite, Vector2 size, Vector2 position, Color color)
        {
            if (rect == null) return;

            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = rect.GetComponent<Image>();
            if (image == null) return;

            if (image.sprite != sprite)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
            }

            image.color = color;
        }

        
        
        
        private void CacheRhythmNoteFills()
        {
            if (rhythmNotes == null) return;
            if (rhythmNoteFills != null && rhythmNoteFills.Length == rhythmNotes.Length) return;
            rhythmNoteFills = new Image[rhythmNotes.Length];
            for (int i = 0; i < rhythmNotes.Length; i++)
                if (rhythmNotes[i] != null)
                    rhythmNoteFills[i] = rhythmNotes[i].GetComponent<Image>();
        }

        /// <summary>
        /// The phase becomes Clear or Failed the moment the run ends, so the round the
        /// player was actually in is gone by the time the result card is drawn. It is
        /// recorded here while play is still going.
        /// </summary>
        private int reachedRound = 1;

        private void TrackReachedRound()
        {
            if (NetworkMatchActive)
            {
                reachedRound = Mathf.Max(1, NetworkReachedRound);
                return;
            }

            switch (flow.Phase)
            {
                case WarriorsBattlePhase.NormalBattle: reachedRound = 1; break;
                case WarriorsBattlePhase.KrakenTentaclePhase: reachedRound = 2; break;
                case WarriorsBattlePhase.FinalKrakenPhase:
                case WarriorsBattlePhase.FinalSwingPhase: reachedRound = 3; break;
            }
        }

        private void UpdateFinalOverlay()
        {
            bool net = NetworkMatchActive;
            bool swing = !net && flow.Phase == WarriorsBattlePhase.FinalSwingPhase;
            bool clear = net ? NetworkFinal == 1 : flow.Phase == WarriorsBattlePhase.Clear;
            // The co-op swing is still live play, so the retry button only appears once
            // the run has actually ended.
            // 네트워크에서는 [다시 하기] 를 숨긴다. 그 버튼은 이 PC 의 씬만 다시 올려서
            // 서버 세션과 어긋난다. 재시작은 서버 담당이 세션 흐름으로 정할 몫이다.
            SetActive(retryButtonRoot, !swing && !net);

            // One line of context above the verdict. During the swing it is the prompt the
            // flow supplies; once the run is over it simply says which way it went.
            string eyebrow = swing ? Korean(flow.FinalSwingTitle)
                : clear ? "미션 성공" : "미션 실패";
            Set(finalEyebrowText, eyebrow);
            if (finalEyebrowText != null) SetActive(finalEyebrowText.gameObject, !string.IsNullOrEmpty(eyebrow));
            // GAME OVER / TIME OVER are left as they are: translating only the losing
            // verdict left a Korean word facing an English one across the same slot.
            Set(finalTitleText, swing ? Korean(flow.FinalSwingLabel)
                : clear ? "GAME CLEAR"
                : net ? (string.IsNullOrEmpty(NetworkFailureLabel) ? "GAME OVER" : NetworkFailureLabel)
                : flow.FailureLabel);

            // Win or lose, the verdict and the rule under it carry the same accent, so the
            // card reads as one thing rather than a gold frame around a red word.
            Color accent = swing ? SwingAccent : clear ? ClearAccent : FailAccent;
            if (finalTitleText != null) finalTitleText.color = accent;
            if (finalEyebrowText != null) finalEyebrowText.color = accent;
            if (resultRule != null) resultRule.color = accent;

            if (swing && flow.FinalSwingLabel == "SWING!")
                Set(finalDetailText, $"지금, 모두 함께 공격하세요!   {flow.SuccessfulSwingPlayerCount} / {flow.ActivePlayerCount}");
            else if (!swing)
            {
                // A loss used to end on a bare GAME OVER with nothing under it, which left
                // the player no sense of how the run had actually gone. The same three
                // numbers appear either way; only the last line differs.
                // 네트워크에서는 서버가 복제한 값(팀 점수 · 경과 시간 · 팀 처치)을 읽는다.
                int elapsed = Mathf.FloorToInt(net ? NetworkElapsedSeconds : score.TotalElapsedSeconds);
                int finalScore = net ? NetworkScore : score.Score;
                int kills = net ? NetworkKills : score.Kills;
                // Laid out as label on the left and value on the right rather than as four
                // centred sentences, so the numbers line up in a column and can be read down
                // the card. <pos> does that inside the one text object the card already has.
                // 클리어하면 라운드 줄을 빼낸다. 세 라운드를 다 끝내야만 나오는 화면에서
                // "최종 라운드  ROUND 3  클리어" 는 바로 위 GAME CLEAR 를 한 번 더 말하는 것뿐이고,
                // 그만큼 정작 읽혀야 할 보상에서 눈을 뺏는다. 진 판에서는 어디까지 갔는지가
                // 실제 정보이므로 그대로 둔다.
                // **결과 항목은 여기 한곳에서 정한다.** 나중에 공용 보상 화면으로 갈아끼울 때
                // 이 목록만 그대로 넘기면 되도록, 문자열을 짜 붙이지 않고 (이름, 값) 짝으로 모은다.
                resultRows.Clear();

                resultRows.Add(("최종 점수", finalScore.ToString("N0")));
                resultRows.Add(("플레이 시간", $"{elapsed / 60:00}:{elapsed % 60:00}"));
                resultRows.Add(("몬스터 처치", kills.ToString()));

                // 진 판에서는 어디까지 갔는지가 실제 정보다.
                // 이긴 판은 세 라운드를 다 끝낸 것이 자명하므로 한 줄을 더 쓰지 않는다.
                if (!clear) resultRows.Add(("도달 라운드", $"ROUND {reachedRound}"));

                Set(finalDetailText, BuildResultText());
            }
            else Set(finalDetailText, string.Empty);

            // The shard is what the whole run is for, so it stays on the card either way:
            // claimed in full colour on a win, dimmed and unclaimed on a loss. Hiding it on
            // a defeat left a hole in the card and said nothing about what had been at stake.
            SetActive(rewardRoot, !swing);
            if (!swing)
            {
                Set(rewardHeadingText, clear ? "획득한 보상" : "놓친 보상");
                Set(rewardNameText, clear ? "바다의 심장 조각" : "획득 실패");

                // 공용 보상 화면이 읽어 갈 값. 화면을 갈아끼워도 이 두 줄은 그대로 쓴다.
                ResultCleared = clear;
                ResultRewardName = clear ? "바다의 심장 조각" : string.Empty;
                if (rewardGem != null)
                    rewardGem.color = clear ? Color.white : new Color(.34f, .42f, .56f, .5f);
                if (rewardNameText != null)
                    rewardNameText.color = clear
                        ? new Color(.85f, .97f, 1f, 1f)
                        : new Color(.58f, .66f, .78f, 1f);

                // **이긴 판에서는 보상이 이 화면의 주인공이다.** 판을 끝까지 끌고 온 이유가
                // 조각이므로, 숫자 나열과 같은 크기로 조용히 앉아 있으면 안 된다. 진 판에서는
                // 얻지 못한 것을 키울 이유가 없으니 원래 크기로 둔다.
                if (rewardRoot != null)
                {
                    RectTransform rewardRect = rewardRoot.transform as RectTransform;
                    if (rewardRect != null)
                        rewardRect.localScale = Vector3.one * (clear ? 1.18f : 1f);
                }

                // 조각 이름은 보상 칸에서 제일 먼저 읽혀야 한다.
                if (rewardNameText != null)
                    rewardNameText.fontStyle = clear ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // ------------------------------------------------------------
        // 결과 화면의 항목 — 나중에 공용 보상 화면으로 갈아끼울 자리
        // ------------------------------------------------------------

        /// <summary>
        /// 결과 화면에 줄줄이 적는 (이름, 값). **이 게임이 내놓는 성적표다.**
        ///
        /// 공용 보상 화면이 붙으면 그쪽이 <see cref="ResultRows"/> 를 읽어 자기 방식으로 그리면 된다.
        /// 지금은 아래 <see cref="BuildResultText"/> 가 한 TMP 칸에 모아 그린다.
        /// </summary>
        private readonly List<(string Label, string Value)> resultRows = new();

        /// <summary>
        /// 이번 판의 성적표. 결과 화면이 떠 있는 동안 채워져 있다.
        ///
        /// 바깥(공용 보상 화면 등)에서 읽어 갈 수 있도록 공개한다.
        /// </summary>
        public IReadOnlyList<(string Label, string Value)> ResultRows => resultRows;

        /// <summary>이번 판의 보상 이름. 못 얻었으면 빈 문자열.</summary>
        public string ResultRewardName { get; private set; } = string.Empty;

        /// <summary>이번 판을 깼는가. 공용 화면이 제목을 고르는 데 쓴다.</summary>
        public bool ResultCleared { get; private set; }

        /// <summary>
        /// 한 칸에 들어갈 수 있는 최대 줄 수.
        ///
        /// 프리팹의 Detail 칸이 <b>520×176, 폰트 25</b> 다. 줄 높이가 약 30px 이므로 5줄이 한계이고,
        /// 그보다 많으면 <b>아래 보상 상자를 덮는다.</b> 실제로 7줄을 넣었다가
        /// "팀 강화" 줄이 잘리고 보상 칸을 침범했다. 넘치면 채우지 않고 잘라 낸다.
        /// </summary>
        private const int ResultRowLimit = 5;

        /// <summary>
        /// 성적표를 한 TMP 칸에 그린다. 이름은 왼쪽, 값은 <c>pos</c> 로 같은 열에 세운다.
        ///
        /// 가운데 정렬 문장 네 줄보다 표가 읽기 쉽다 — 숫자가 한 줄로 서기 때문이다.
        /// </summary>
        private string BuildResultText()
        {
            StringBuilder text = new();

            int rows = Mathf.Min(resultRows.Count, ResultRowLimit);

            for (int i = 0; i < rows; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(resultRows[i].Label).Append("<pos=58%>").Append(resultRows[i].Value);
            }

            return text.ToString();
        }

        // Gold for a win, red for a loss, plain white while the co-op swing is still
        // live - the colour is the first thing read, before any of the words are.
        private static readonly Color ClearAccent = new(1f, .82f, .35f, 1f);
        private static readonly Color FailAccent = new(1f, .42f, .38f, 1f);
        private static readonly Color SwingAccent = new(.95f, .97f, 1f, 1f);

        private static string Glyph(WarriorsAttackDirection type) => type switch
        {
            WarriorsAttackDirection.HorizontalSlash => "↔",
            WarriorsAttackDirection.VerticalSlash => "↕",
            _ => "⊙"
        };

        /// <summary>
        /// 이 공격을 내는 <b>키 글자</b>. 노트 위에 함께 찍는다.
        ///
        /// <b>왜 노트에 키를 적는가.</b> 화살표만 있으면 3라운드에서 읽는 순서가
        /// <c>↔ → "가로베기구나" → "가로베기는 J 였지"</c> 로 <b>세 단계</b>다. 노트가
        /// 판정선까지 내려오는 시간은 1초 안쪽이라 그 사이에 다 거치지 못하고,
        /// 아래 카드로 눈을 내렸다 오면 이미 지나가 있다. 실제로 "헷갈리고 잘 안 보인다" 는
        /// 지적을 받은 부분이 여기다.
        ///
        /// 키를 노트에 같이 얹으면 한 단계로 줄어든다. 화살표는 그대로 두는데,
        /// IoT 컨트롤러가 붙으면 <b>키가 아니라 동작</b>으로 내게 되고 그때 읽어야 하는 것은
        /// 다시 화살표이기 때문이다. 둘 다 필요하다.
        ///
        /// ⚠ 여기 값은 <c>IOT_INPUT.md</c> 1장 · <c>KeyboardPlayerController</c> 의
        ///    <c>horizontalMotion / verticalMotion / thrustMotion</c> 과 같아야 한다.
        ///    한쪽만 바꾸면 화면이 틀린 키를 가리킨다.
        /// </summary>
        private static string KeyOf(WarriorsAttackDirection type) => type switch
        {
            WarriorsAttackDirection.HorizontalSlash => "J",
            WarriorsAttackDirection.VerticalSlash => "K",
            _ => "L"
        };

        /// <summary>
        /// 노트가 깨질 때 <b>어느 쪽으로</b> 늘어나는가. 공격 종류마다 다르다.
        ///
        ///   ↔ 가로베기  좌우로 찢어진다 (가로 1.9배 · 세로 0.45배)
        ///   ↕ 세로베기  위아래로 갈라진다
        ///   ⊙ 찌르기    사방으로 고르게 (원형 burst)
        ///
        /// <paramref name="burst"/> 는 0(판정된 순간) → 1(다 사라짐).
        /// </summary>
        private static Vector3 BurstSpread(WarriorsAttackDirection type, float burst)
        {
            if (burst <= 0f) return Vector3.one;

            // **베인 순간 얇게 눌리고 길게 늘어난다.**
            //
            // 비트세이버의 쾌감은 "사라졌다" 가 아니라 <b>칼날을 따라 갈라졌다</b> 는 인상에서 온다.
            // 앞의 25% 에서 칼날 두께만큼 확 눌린 뒤(0.45 → 0.18) 그 방향으로 길게 늘어나며
            // 흩어지게 하면, 같은 0.2초 안에서도 "잘렸다" 로 읽힌다.
            float k = Mathf.SmoothStep(0f, 1f, burst);
            float snap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(burst / .25f));   // 초반에 빠르게

            return type switch
            {
                // 가로베기 — 좌우로 길게 찢어지고 세로로 납작해진다.
                WarriorsAttackDirection.HorizontalSlash =>
                    new Vector3(Mathf.Lerp(1f, 2.6f, k), Mathf.Lerp(1f, .18f, snap), 1f),

                // 세로베기 — 위아래로 길게, 가로로 납작하게.
                WarriorsAttackDirection.VerticalSlash =>
                    new Vector3(Mathf.Lerp(1f, .18f, snap), Mathf.Lerp(1f, 2.6f, k), 1f),

                // 찌르기 — 방향이 없으므로 한 번 오므렸다가 사방으로 터진다.
                _ => Vector3.one * Mathf.Lerp(.82f, 1.35f, k),
            };
        }

        /// <summary>공격 종류의 색. 아래 안내 카드(물고기 · 게 · 해파리)와 같은 계열이다.</summary>
        private Color NoteColor(WarriorsAttackDirection type) => type switch
        {
            WarriorsAttackDirection.HorizontalSlash => rhythmHorizontalColor,
            WarriorsAttackDirection.VerticalSlash => rhythmVerticalColor,
            WarriorsAttackDirection.Thrust => rhythmThrustColor,
            _ => rhythmNoteColor,
        };

        private static void Set(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        /// <summary>
        /// 시계 자리("03:00")에 네트워크 문구("1P 목숨 4   2P 목숨 4" · "2P 가 멈췄습니다")가
        /// 들어오면 칸을 넘쳐 카드 밖으로 글자가 샌다. 그때만 자동 축소를 켜고, 시계로
        /// 돌아오면 원래 크기로 되돌린다.
        /// </summary>
        /// <summary>
        /// **안내 패널(Objective) 너비를 문장 길이에 맞춘다.**
        ///
        /// ⚠ 프리팹에 박힌 값(408px)은 "가장 긴 문장" 기준으로 감으로 잡은 고정폭이었다.
        ///    짧은 문장이 올 때는 뒤에 빈 박스가 그대로 남고, 그렇다고 줄일 근거도 없었다 —
        ///    실측(TMP <c>GetPreferredValues</c>)도 없이 숫자만 조정하면 다음 문장에서 또 어긋난다.
        ///
        /// 그래서 매번 <b>이 문장의 실제 폭</b>을 재서 패딩만 더해 쓴다. 최소/최대 폭은
        /// 화면 밖으로 나가거나(너무 넓음) 글자가 테두리에 닿는(너무 좁음) 것만 막는다.
        /// </summary>
        private void FitObjectivePanel(string text)
        {
            if (objectiveText == null || objectiveRoot == null) return;

            RectTransform panel = objectiveRoot.transform as RectTransform;
            if (panel == null) return;

            // ⚠ 좌우 28 씩(56)은 실측에서 "글자보다 칸이 훨씬 길다" 로 보였다. 회색 판 테가 5px 라
            //    글자 끝에서 테까지 14px 면 넉넉하다. 폭은 preferredWidth(자동 크기 최대값 기준으로
            //    실제보다 크게 나옴)가 아니라 **실제 그려진 글자 상자**로 잰다.
            const float horizontalPadding = 28f;
            const float minWidth = 160f;
            const float maxWidth = 520f;

            objectiveText.ForceMeshUpdate();
            float drawn = objectiveText.textBounds.size.x;
            if (drawn <= 0f) drawn = objectiveText.GetPreferredValues(text ?? string.Empty).x;

            float width = Mathf.Clamp(drawn + horizontalPadding, minWidth, maxWidth);

            panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);
        }

        private static void FitLongText(TMP_Text target, bool longText)
        {
            if (target == null || target.enableAutoSizing == longText) return;

            if (longText)
            {
                target.fontSizeMax = target.fontSize;
                target.fontSizeMin = Mathf.Max(12f, target.fontSize * .4f);
                target.enableAutoSizing = true;
                return;
            }

            target.enableAutoSizing = false;
            if (target.fontSizeMax > 0f) target.fontSize = target.fontSizeMax;
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null) target.fillAmount = Mathf.Clamp01(value);
        }

        /// <summary>
        /// **보스 체력 막대를 보스답게 그린다.**
        ///
        /// 평평한 막대 하나로는 지금 몇 대 때렸는지, 방금 깎였는지가 전혀 남지 않는다.
        /// 두 가지를 얹는다.
        ///
        /// <code>
        ///   피해 잔상   방금 깎인 만큼이 옅은 흰 띠로 남았다가 천천히 따라온다
        ///   체력 색     60% 위 주황 · 그 아래 붉게 · 25% 아래 진한 빨강
        /// </code>
        ///
        /// 잔상은 <b>진짜 막대 뒤에</b> 깔린 같은 크기의 Image 다. 프리팹을 고치지 않고
        /// 처음 필요할 때 한 장 만들어 재사용한다.
        /// </summary>
        private void PaintBossBar(Image fill, float remaining)
        {
            if (fill == null) return;

            remaining = Mathf.Clamp01(remaining);

            if (bossTrail == null || bossTrail.transform.parent != fill.transform.parent)
            {
                GameObject made = new("BossTrail", typeof(RectTransform));
                made.transform.SetParent(fill.transform.parent, false);

                RectTransform rect = made.GetComponent<RectTransform>();
                RectTransform source = fill.rectTransform;

                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.anchoredPosition = source.anchoredPosition;
                rect.sizeDelta = source.sizeDelta;

                bossTrail = made.AddComponent<Image>();
                bossTrail.sprite = fill.sprite;
                bossTrail.type = fill.type;
                bossTrail.fillMethod = fill.fillMethod;
                bossTrail.fillOrigin = fill.fillOrigin;
                bossTrail.raycastTarget = false;
                bossTrail.color = new Color(1f, .93f, .82f, .55f);

                // 진짜 막대 **뒤**에 둔다. 앞에 오면 잔상이 체력을 가린다.
                made.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());

                shownBossFill = remaining;
            }

            // 잔상은 늘 실제 체력 이상이고, 깎인 뒤 천천히 따라 내려온다.
            if (remaining > shownBossFill) shownBossFill = remaining;   // 새 판 등으로 되찬 경우
            else shownBossFill = Mathf.MoveTowards(shownBossFill, remaining, Time.unscaledDeltaTime * .45f);

            bossTrail.fillAmount = Mathf.Clamp01(shownBossFill);

            // 남은 체력에 따라 색이 바뀐다 — 숫자를 읽지 않아도 상태가 보인다.
            fill.color = remaining > .6f ? new Color(1f, .58f, .22f, .96f)
                : remaining > .25f ? new Color(.95f, .33f, .24f, .96f)
                : new Color(.80f, .14f, .16f, .98f);

            SetFill(fill, remaining);
        }

        private Image bossTrail;
        private float shownBossFill = 1f;

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }
    }
}

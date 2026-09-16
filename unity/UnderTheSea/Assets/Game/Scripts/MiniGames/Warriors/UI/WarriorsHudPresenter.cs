using System.Collections.Generic;
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
        [SerializeField] private Color rhythmHorizontalColor = new(.2f, .55f, .95f, .95f);   // 물고기 · 가로베기 · 파랑
        [SerializeField] private Color rhythmVerticalColor = new(.9f, .33f, .3f, .95f);      // 게 · 세로베기 · 빨강
        // 찌르기는 **노랑**이다. 보라로 두었더니 판정선(청록)·가로베기(파랑)와 한 계열로 뭉쳐
        // 세 종류가 한눈에 갈리지 않았다. 파랑 · 빨강 · 노랑이 서로 가장 멀다.
        [SerializeField] private Color rhythmThrustColor = new(1f, .82f, .25f, .95f);        // 해파리 · 찌르기 · 노랑

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

        /// <summary>노트가 생기는 높이(NoteTrack 로컬). 화면 중앙 기준 +360 — 좌우 상단 카드(384) 바로 아래.</summary>
        private const float NoteSpawnLocalY = 320f;

        /// <summary>판정선 높이(NoteTrack 로컬). 화면 중앙 기준 -250 — 하단 공격 가이드(-430) 위.</summary>
        private const float JudgeLocalY = -290f;

        /// <summary>
        /// 판정선에서의 입력 열 간격.
        ///
        /// 노트 지름이 90 이고 "지름 = 열 폭의 65%" 로 잡으면 90 / .65 = 138 이다.
        /// 이보다 좁으면 두 노트가 나란히 떨어질 때 어느 열인지 읽히지 않는다.
        /// </summary>
        private const float ColumnPitch = 138f;

        /// <summary>판정선에서의 트랙 반폭. 열 3개 = 414, 그 절반.</summary>
        private const float TrackHalfWidthBottom = ColumnPitch * 1.5f;

        /// <summary>
        /// 트랙 맨 위의 폭은 아래의 몇 배인가. 리듬게임 하이웨이처럼 위가 좁고 아래가 넓다.
        ///
        /// 직사각형이면 "투명한 상자" 로 보이고, 이 기울기가 있어야 노트가 <b>다가온다</b>고 읽힌다.
        /// </summary>
        private const float TrackTopScale = .60f;

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
        }

        private void OnDisable()
        {
            ReleaseIntroHold();
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
            // **더 이상 띄우지 않는다.**
            //
            // 이 줄은 "1P 적중 12   2P 적중 8" 을 보여 줬다. 적중 수는 전투 중에 아무 판단에도
            // 쓰이지 않는 값인데, 2인 플레이에서는 늘 켜져 화면 가장자리를 한 층 더 채웠다.
            // 특히 3라운드에서는 그 자리보다 각자의 리듬 레인과 크라켄 HP 가 훨씬 중요하다.
            //
            // 부품과 갱신 코드는 남겨 둔다. 다시 켜고 싶으면 이 한 줄만 되돌리면 된다.
            SetActive(playerStatusRoot, false);
            SetActive(rhythmRoot, isRhythm);
            SetActive(finalRoot, isFinalOverlay);

            // ROUND 1 teaches the monster -> attack mapping; Fish/Crab/Jellyfish do not
            // appear from ROUND 2 on, so only the three attacks stay on screen there.
            bool monsterRound = NetworkMatchActive
                ? NetworkRound <= 1
                : flow.Phase == WarriorsBattlePhase.NormalBattle;
            SetActive(monsterGuideRoot, monsterRound && !isFinalOverlay);
            SetActive(attackGuideRoot, !monsterRound && !isFinalOverlay);

            TrackReachedRound();
            UpdateRoundIntro(isFinalOverlay);
            UpdateGuidePulse();
            UpdateSharedFeedback(isFinalOverlay);
            if (isRhythm) UpdateRhythmHud();
            else if (isFinalOverlay) UpdateFinalOverlay();
            else UpdateStandardHud();
        }

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
                ? (tentacle ? "ROUND 2  ·  크라켄 등장" : "ROUND 1  ·  몬스터 습격")
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
            Set(comboText, $"COMBO  {(combo != null ? combo.Combo : 0)}");
            int activePlayers = ResolveConnectedPlayers();
            // Health is one shared pool, so putting it on both rows drew the same bar twice
            // and said nothing about either player. What differs between them is what each
            // one has personally connected with, so that is what the strip shows.
            int landedTotal = 0;
            for (int i = 0; i < activePlayers; i++) landedTotal += LandedHitsOf(i);

            for (int i = 0; i < playerStateTexts.Length; i++)
            {
                // Empty slots are hidden rather than parked on WAIT.  The battle never waits
                // for an absent player, so a row that says WAIT for the whole run is a lie.
                bool active = i < activePlayers;
                SetActive(RowOf(playerStateTexts[i], playerStatusRoot), active);
                if (!active) continue;
                int landed = LandedHitsOf(i);
                Set(playerStateTexts[i], $"{i + 1}P   적중 {landed}");
                if (i < playerStateFills.Length)
                    SetFill(playerStateFills[i], landedTotal > 0 ? landed / (float)landedTotal : 0f);
            }
        }

        /// <summary>
        /// The slot count must reflect the players that actually exist in the scene rather
        /// than a configured maximum, otherwise a solo run shows a second player who is not
        /// there.
        /// </summary>
        /// <summary>
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
        /// </summary>
        /// <summary>
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
                        // 콤보 안내는 **시작 화면에서 한 번만.** 아래 objective 에는 넣지 않는다.
                        body = "내려오는 화살표가 판정선에 닿을 때 공격하세요.\n" +
                               "연속으로 성공하면 강한 공격이 발동합니다.";
                        objective = "내려오는 화살표가 판정선에 닿을 때 공격하세요.";
                        break;
                }

                // **앞 라운드를 깬 문구.** 라운드 번호가 오른 순간에만, 한 문장을 잠깐 띄운다.
                //
                // 서버에 따로 값을 두지 않는다 — 라운드 번호는 이미 복제되고 있으므로, 그 값이
                // 올라간 것을 각 화면이 보고 판단하면 두 화면이 같은 순간에 같은 문구를 낸다.
                if (NetworkMatchActive && NetworkRound > shownRound && shownRound > 0)
                {
                    clearNotice = ClearedLine(shownRound);
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
                    introUntil = Time.unscaledTime + roundIntroSeconds;
                    // Keep the briefing above every regular HUD panel regardless of the
                    // prefab's authored sibling order.
                    roundIntroRoot?.transform.SetAsLastSibling();
                }
            }

            float remaining = introUntil - Time.unscaledTime;
            bool show = !finalOverlayVisible && remaining > 0f;
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

        private static void Cache(RectTransform[] cards, ref UnityEngine.UI.Image[] fills, ref Color[] baseColors)
        {
            if (cards == null || (fills != null && fills.Length == cards.Length)) return;
            fills = new UnityEngine.UI.Image[cards.Length];
            baseColors = new Color[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                fills[i] = cards[i].GetComponent<UnityEngine.UI.Image>();
                if (fills[i] != null) baseColors[i] = fills[i].color;
            }
        }

        private static void Pulse(RectTransform[] cards, UnityEngine.UI.Image[] fills, Color[] baseColors, int lit)
        {
            if (cards == null || fills == null) return;
            float step = Time.unscaledDeltaTime * 12f;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                bool on = i == lit;
                Color target = on
                    ? new Color(baseColors[i].r + .16f, baseColors[i].g + .20f, baseColors[i].b + .26f, baseColors[i].a)
                    : baseColors[i];

                // Snap up on the swing and ease back down - a hit cue has to land on the
                // same frame as the input to read as a response rather than a delay.
                if (on)
                {
                    cards[i].localScale = Vector3.one * 1.07f;
                    if (fills[i] != null) fills[i].color = target;
                    continue;
                }

                cards[i].localScale = Vector3.Lerp(cards[i].localScale, Vector3.one, step);
                if (fills[i] != null) fills[i].color = Color.Lerp(fills[i].color, target, step);
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

        private void UpdateSharedFeedback(bool finalOverlayVisible)
        {
            // 네트워크 판에서는 라운드를 깬 문구를 여기로 낸다. 혼자 하는 씬은 예전 경로 그대로.
            bool showCleared = NetworkMatchActive
                               && !finalOverlayVisible
                               && Time.unscaledTime < clearNoticeUntil
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

        private void UpdateRhythmHud()
        {
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
                SetFill(rhythmBossFill, Mathf.Clamp01(NetworkRhythmProgress));
            }
            else
            {
                Set(rhythmBossText, $"크라켄   {flow.FinalKrakenHealthPercent}%");
                SetFill(rhythmBossFill, flow.FinalKrakenHealthPercent / 100f);
            }
            Set(rhythmScoreText, (NetworkMatchActive ? NetworkScore : score.Score).ToString("N0"));
            Set(rhythmComboText, $"COMBO  {rhythm.Combo}");
            // 네트워크 3페이즈는 서버가 정한 판정 문구(콤보 피니시)를 쓴다. 혼자 하는 씬은 예전 그대로.
            string judgement = NetworkRhythmActive
                ? Korean(NetworkRhythmJudgement ?? string.Empty)
                : Korean(rhythm.ActiveJudgement);
            Set(rhythmJudgementText, judgement);
            // The chip is only a backing for the word, so it comes and goes with it rather
            // than sitting on the lane as an empty box.
            SetActive(rhythmJudgementChip, !string.IsNullOrEmpty(judgement));

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
                if (!visible) continue;
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
                float alpha = mine ? 1f : .38f;

                // 멀리 있을수록 작다. 다만 스폰 지점에서도 무엇인지는 읽혀야 하므로
                // 트랙 비율(0.60)을 그대로 쓰지 않고 0.80~1.30 으로 눌러 쓴다.
                // 프리팹 노트가 90px 이라 판정선에서 117px, 스폰에서 72px 이 된다.
                float noteScale = Mathf.LerpUnclamped(.80f, 1.30f, note.Travel);
                rhythmNotes[i].localScale = Vector3.one * noteScale * (mine ? 1f : .92f);

                // **속을 공격 색으로 가득 채우고 기호는 흰색이다.**
                //
                // 한때 속을 비우고 기호에만 색을 줘 봤는데, 실제 화면에서 바다 · 모래 · 크라켄 위에
                // 얹히니 거의 보이지 않았다. 노트는 배경에서 가장 먼저 눈에 들어와야 하는 것이라
                // 채운 원이 맞다. 대신 트랙 바닥을 옅게 두어 노트만 도드라지게 한다.
                Color tone = note.IsSuccessfulHit ? rhythmSuccessfulColor
                    : note.IsMissed ? rhythmMissedColor
                    : NoteColor(note.Type);
                tone.a *= alpha;

                if (i < rhythmNoteFills.Length && rhythmNoteFills[i] != null)
                {
                    Image disc = rhythmNoteFills[i];
                    if (disc.type == Image.Type.Sliced) disc.fillCenter = true;
                    disc.color = tone;
                }

                if (i < rhythmNoteGlyphs.Length && rhythmNoteGlyphs[i] != null)
                {
                    Set(rhythmNoteGlyphs[i], Glyph(note.Type));
                    rhythmNoteGlyphs[i].color = new Color(1f, 1f, 1f, alpha);
                }
            }
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

            laneCentres.Clear();

            for (int i = 0; i < laneVisuals.Count; i++)
            {
                LaneVisual lane = laneVisuals[i];
                bool used = i < playerCount;

                lane.fill.gameObject.SetActive(used);
                lane.leftEdge.gameObject.SetActive(used);
                lane.rightEdge.gameObject.SetActive(used);
                lane.judge.gameObject.SetActive(used);
                lane.label.gameObject.SetActive(used);
                foreach (RectTransform column in lane.columns) column.gameObject.SetActive(used);

                if (!used) continue;

                float x = TrackCentre(i, playerCount);
                laneCentres.Add(x);

                bool mine = !NetworkMatchActive || NetworkLocalLane < 0 || i == NetworkLocalLane;
                float dim = mine ? 1f : .55f;

                float bottom = TrackHalfWidthBottom;
                float top = bottom * TrackTopScale;

                // **트랙 색은 사람마다 다르다.** 1P 청록 · 2P 금색.
                // 두 트랙이 같은 색이면 화면 가운데를 기준으로 좌우가 거울처럼 보여, 자기 줄을
                // 구별하는 단서가 위치 하나뿐이다. 색이 다르면 눈이 먼저 자기 줄을 찾는다.
                Color tint = TrackTint(i);

                // 바닥은 거의 투명하다. 사다리꼴이라 사각형 하나로는 못 그리므로, 가운데를 채우는 대신
                // 가장자리 두 줄로 "길" 을 만든다. 얇게 깔린 바닥은 방향만 거든다.
                Color floorColor = tint;
                floorColor.a = .10f * dim;

                Paint(lane.fill, new Vector2(top * 2f, spawnY - hitLineY),
                    new Vector2(x, (spawnY + hitLineY) * .5f), floorColor);

                // **양쪽 가장자리 — 위가 좁고 아래가 넓게 기울인다.** 이 기울기가 원근을 만든다.
                Color edgeColor = tint;
                edgeColor.a = .85f * dim;

                PaintSegment(lane.leftEdge, x - top, spawnY, x - bottom, hitLineY, 4f, edgeColor);
                PaintSegment(lane.rightEdge, x + top, spawnY, x + bottom, hitLineY, 4f, edgeColor);

                // 트랙을 세로로 나누는 **옅은 구분선 2줄.** 길이 흐르는 방향만 거들 뿐,
                // 공격 종류와는 상관이 없다 — 종류는 노트의 색과 기호가 말한다.
                Color divider = tint;
                divider.a = .22f * dim;

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

                // 레인마다 자기 판정선. 트랙과 같은 색이라 어느 선이 내 것인지 바로 읽힌다.
                Color judgeColor = tint;
                judgeColor.a = .98f * dim;

                Paint(lane.judge, new Vector2(bottom * 2f + 20f, 9f), new Vector2(x, hitLineY), judgeColor);

                // 판정선 바로 아래 "P1" · "P2".
                lane.label.rectTransform.sizeDelta = new Vector2(bottom * 2f, 48f);
                lane.label.rectTransform.anchoredPosition = new Vector2(x, hitLineY - 44f);
                lane.label.text = $"P{i + 1}";
                lane.label.color = new Color(tint.r, tint.g, tint.b, .95f * dim);
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

        /// <summary>이 사람의 트랙 색. 1P 청록 · 2P 금색.</summary>
        private static Color TrackTint(int lane) => lane == 0
            ? new Color(.36f, .86f, 1f, 1f)
            : new Color(1f, .78f, .28f, 1f);

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
                string roundValue = clear ? $"ROUND {reachedRound}  클리어" : $"ROUND {reachedRound}";
                Set(finalDetailText,
                    $"최종 점수<pos=58%>{finalScore:N0}\n플레이 시간<pos=58%>{elapsed / 60:00}:{elapsed % 60:00}\n몬스터 처치<pos=58%>{kills}\n{(clear ? "최종 라운드" : "도달 라운드")}<pos=58%>{roundValue}");
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
                if (rewardGem != null)
                    rewardGem.color = clear ? Color.white : new Color(.34f, .42f, .56f, .5f);
                if (rewardNameText != null)
                    rewardNameText.color = clear
                        ? new Color(.85f, .97f, 1f, 1f)
                        : new Color(.58f, .66f, .78f, 1f);
            }
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

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }
    }
}

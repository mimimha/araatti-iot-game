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

        [Header("Final overlay")]
        [SerializeField] private GameObject finalRoot;
        [SerializeField] private TMP_Text finalEyebrowText;
        [SerializeField] private TMP_Text finalTitleText;
        [SerializeField] private TMP_Text finalDetailText;
        [SerializeField] private GameObject retryButtonRoot;

        private readonly List<WarriorsRhythmNoteView> visibleNotes = new();
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
            bool isRhythm = NetworkRhythmActive ||
                            (flow.Phase == WarriorsBattlePhase.FinalKrakenPhase && rhythm != null && rhythm.IsActive);
            bool isFinalOverlay = flow.Phase == WarriorsBattlePhase.FinalSwingPhase ||
                                  flow.Phase == WarriorsBattlePhase.Clear ||
                                  flow.Phase == WarriorsBattlePhase.Failed;

            SetActive(standardHud, !isRhythm && !isFinalOverlay);
            // The player strip lives outside StandardHUD so it survives the rhythm round,
            // but the result screen should be clean.
            // A solo run has nothing to compare, and the health it would show is already
            // on the HP card - so the strip is a second player's worth of chrome with no
            // second player. It appears only when there is someone to tell apart.
            SetActive(playerStatusRoot, !isFinalOverlay && ResolveConnectedPlayers() > 1);
            SetActive(rhythmRoot, isRhythm);
            SetActive(finalRoot, isFinalOverlay);

            // ROUND 1 teaches the monster -> attack mapping; Fish/Crab/Jellyfish do not
            // appear from ROUND 2 on, so only the three attacks stay on screen there.
            bool monsterRound = flow.Phase == WarriorsBattlePhase.NormalBattle;
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
            bool tentacle = flow.Phase == WarriorsBattlePhase.KrakenTentaclePhase;
            int secondsLeft = Mathf.CeilToInt(score.RemainingSeconds);
            int hp = playerHealth != null ? playerHealth.CurrentHealth : 100;
            int maxHp = playerHealth != null ? playerHealth.MaxHealth : 100;
            float progress = tentacle
                ? flow.TentacleSuccesses / (float)Mathf.Max(1, flow.TentacleSuccessesRequired)
                : score.Progress;

            // 매치 안내가 있으면 그쪽이 먼저다. 대기 · 카운트다운 · 결과에만 쓰인다.
            Set(roundText, string.IsNullOrEmpty(MatchNotice)
                ? (tentacle ? "ROUND 2  ·  크라켄 등장" : "ROUND 1  ·  몬스터 습격")
                : MatchNotice);
            Set(timeText, string.IsNullOrEmpty(MatchDetail)
                ? $"{secondsLeft / 60:00}:{secondsLeft % 60:00}"
                : MatchDetail);
            Set(hpText, $"HP  {hp}");
            SetFill(hpFill, hp / (float)Mathf.Max(1, maxHp));
            Set(phaseValueText, tentacle
                ? $"잘라낸 촉수   {flow.TentacleSuccesses} / {flow.TentacleSuccessesRequired}"
                : $"처치 수   {score.Kills} / {score.TargetKills}");
            SetFill(phaseFill, progress);
            Set(scoreText, score.Score.ToString("N0"));
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
        private void UpdateRoundIntro(bool finalOverlayVisible)
        {
            if (flow.Phase != introPhase)
            {
                introPhase = flow.Phase;
                string title = null, body = null, objective = null, badge = null;
                // The same round is a different problem depending on how many are
                // playing: one player answers a two tentacle pattern in sequence, two
                // answer it at once. The brief has to say which game this is.
                bool solo = ResolveConnectedPlayers() <= 1;
                switch (flow.Phase)
                {
                    case WarriorsBattlePhase.NormalBattle:
                        badge = "ROUND 1";
                        title = "해변으로 몬스터들이 몰려오고 있습니다!";
                        body = "몬스터 종류에 맞는 공격으로 처치하세요!";
                        objective = "몬스터에 맞는 공격으로 해변을 지키세요!";
                        break;
                    case WarriorsBattlePhase.KrakenTentaclePhase:
                        badge = "ROUND 2";
                        title = "크라켄이 섬을 공격하기 시작했습니다!";
                        body = solo
                            ? "표시에 맞는 공격으로, 고리가 닫히기 전에 차례대로 잘라내세요!"
                            : "하나씩 나눠 맡아 동시에 잘라내면 보너스가 들어옵니다!";
                        objective = solo
                            ? "고리가 닫히기 전에 촉수를 잘라내세요!"
                            : "촉수를 하나씩 맡아 동시에 잘라내세요!";
                        break;
                    case WarriorsBattlePhase.FinalKrakenPhase:
                        badge = "ROUND 3";
                        title = "크라켄이 마지막 공격을 준비합니다!";
                        body = solo
                            ? "내려오는 아이콘을 판정선에 맞추어 3연타하세요!"
                            : "각자의 줄을 3연타하면 둘이 함께 마무리합니다!";
                        objective = "판정선에 맞춰 연속 공격하세요!";
                        break;
                }

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
            if (holdGameplayDuringIntro && introHoldsTime != show)
            {
                introHoldsTime = show;
                Time.timeScale = show ? 0f : 1f;
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
            _ => label,
        };

        private void UpdateSharedFeedback(bool finalOverlayVisible)
        {
            bool showAnnouncement = flow.IsTransitioning && !finalOverlayVisible && Time.unscaledTime >= introUntil;
            SetActive(announcementRoot, showAnnouncement);
            Set(announcementText, showAnnouncement ? Korean(flow.TransitionLabel) : string.Empty);
            Set(specialText, combo != null ? combo.ActiveSpecial : string.Empty);
            // The attack name is no longer written across the screen; the card reacts instead.
            Set(actionText, string.Empty);
        }

        private void UpdateRhythmHud()
        {
            int secondsLeft = Mathf.CeilToInt(score.RemainingSeconds);
            Set(rhythmRoundText, string.IsNullOrEmpty(MatchNotice) ? "ROUND 3  ·  최종 결전" : MatchNotice);
            // 네트워크에서는 3분 시계로 지지 않는다. 그 자리에 남은 목숨을 보여 준다.
            Set(rhythmTimeText, NetworkRhythmActive
                ? NetworkRhythmLives
                : $"{secondsLeft / 60:00}:{secondsLeft % 60:00}");
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
                // 네트워크 3페이즈는 크라켄 HP 가 아니라 **팀 합산 목표**로 끝난다.
                // 같은 막대에 같은 뜻(얼마나 남았나)을 그리되 분모가 다르다.
                Set(rhythmBossText, NetworkRhythmDetail);
                SetFill(rhythmBossFill, 1f - Mathf.Clamp01(NetworkRhythmProgress));
            }
            else
            {
                Set(rhythmBossText, $"크라켄   {flow.FinalKrakenHealthPercent}%");
                SetFill(rhythmBossFill, flow.FinalKrakenHealthPercent / 100f);
            }
            Set(rhythmScoreText, score.Score.ToString("N0"));
            Set(rhythmComboText, $"COMBO  {rhythm.Combo}");
            string judgement = Korean(rhythm.ActiveJudgement);
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
            const float laneGap = 96f;
            const float noteSpawnY = 250f;
            float hitLineY = ResolveHitLineY();
            float width = laneGap * (playerCount - 1);
            if (rhythmHitLine != null) rhythmHitLine.sizeDelta = new Vector2(Mathf.Max(180f, width + 140f), 5f);

            for (int i = 0; i < rhythmNotes.Length; i++)
            {
                bool visible = i < visibleNotes.Count;
                rhythmNotes[i].gameObject.SetActive(visible);
                if (!visible) continue;
                WarriorsRhythmNoteView note = visibleNotes[i];
                float x = playerCount <= 1 ? 0f : -width * .5f + Mathf.Min(note.PlayerIndex, playerCount - 1) * laneGap;
                // Travel 1 is the moment the note is due, so it has to be exactly on the line
                // then - the two used to disagree, and the player had to swing when the note
                // was already well past it. Unclamped so a missed note keeps falling through
                // instead of stopping dead on the line.
                float y = Mathf.LerpUnclamped(noteSpawnY, hitLineY, note.Travel);
                rhythmNotes[i].anchoredPosition = new Vector2(x, y);
                // The icon never changes colour on the way down: the player reads the shape,
                // not a colour cue, and a colour that shifts near the line invites pressing
                // early.
                if (i < rhythmNoteFills.Length && rhythmNoteFills[i] != null)
                    rhythmNoteFills[i].color = rhythmNoteColor;
                if (i < rhythmNoteGlyphs.Length && rhythmNoteGlyphs[i] != null)
                {
                    Set(rhythmNoteGlyphs[i], Glyph(note.Type));
                    rhythmNoteGlyphs[i].color = Color.white;
                }
            }
        }

        /// <summary>
        /// Where the judgement line sits, measured in the space the notes are positioned in.
        /// The line and the notes share a parent but not an anchor - the line hangs off the
        /// bottom of the track, the notes off its centre - so the line's own anchoredPosition
        /// is not a number a note can be moved to. Converting through world space keeps the
        /// two in agreement no matter how the prefab is laid out.
        /// </summary>
        private float ResolveHitLineY()
        {
            if (rhythmHitLine == null || rhythmNotes == null || rhythmNotes.Length == 0) return -180f;
            RectTransform reference = rhythmNotes[0];
            if (reference == null || reference.parent == null) return -180f;
            return reference.parent.InverseTransformPoint(rhythmHitLine.position).y;
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
            bool swing = flow.Phase == WarriorsBattlePhase.FinalSwingPhase;
            bool clear = flow.Phase == WarriorsBattlePhase.Clear;
            // The co-op swing is still live play, so the retry button only appears once
            // the run has actually ended.
            SetActive(retryButtonRoot, !swing);

            // One line of context above the verdict. During the swing it is the prompt the
            // flow supplies; once the run is over it simply says which way it went.
            string eyebrow = swing ? Korean(flow.FinalSwingTitle)
                : clear ? "미션 성공" : "미션 실패";
            Set(finalEyebrowText, eyebrow);
            if (finalEyebrowText != null) SetActive(finalEyebrowText.gameObject, !string.IsNullOrEmpty(eyebrow));
            // GAME OVER / TIME OVER are left as they are: translating only the losing
            // verdict left a Korean word facing an English one across the same slot.
            Set(finalTitleText, swing ? Korean(flow.FinalSwingLabel)
                : clear ? "GAME CLEAR" : flow.FailureLabel);

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
                int elapsed = Mathf.FloorToInt(score.TotalElapsedSeconds);
                // Laid out as label on the left and value on the right rather than as four
                // centred sentences, so the numbers line up in a column and can be read down
                // the card. <pos> does that inside the one text object the card already has.
                string roundValue = clear ? $"ROUND {reachedRound}  클리어" : $"ROUND {reachedRound}";
                Set(finalDetailText,
                    $"최종 점수<pos=58%>{score.Score:N0}\n플레이 시간<pos=58%>{elapsed / 60:00}:{elapsed % 60:00}\n몬스터 처치<pos=58%>{score.Kills}\n{(clear ? "최종 라운드" : "도달 라운드")}<pos=58%>{roundValue}");
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

        private static void Set(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
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

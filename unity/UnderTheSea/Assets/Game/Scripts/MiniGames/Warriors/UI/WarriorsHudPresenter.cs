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

        [Header("Top HUD")]
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
        [SerializeField] private TMP_Text rhythmBossText;
        [SerializeField] private Image rhythmBossFill;
        [SerializeField] private TMP_Text rhythmScoreText;
        [SerializeField] private TMP_Text rhythmComboText;
        [SerializeField] private RectTransform rhythmHitLine;
        [SerializeField] private TMP_Text rhythmJudgementText;
        [SerializeField] private RectTransform[] rhythmNotes;
        [SerializeField] private TMP_Text[] rhythmNoteGlyphs;

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

            bool isRhythm = flow.Phase == WarriorsBattlePhase.FinalKrakenPhase && rhythm != null && rhythm.IsActive;
            bool isFinalOverlay = flow.Phase == WarriorsBattlePhase.FinalSwingPhase ||
                                  flow.Phase == WarriorsBattlePhase.Clear ||
                                  flow.Phase == WarriorsBattlePhase.Failed;

            SetActive(standardHud, !isRhythm && !isFinalOverlay);
            // The player strip lives outside StandardHUD so it survives the rhythm round,
            // but the result screen should be clean.
            SetActive(playerStatusRoot, !isFinalOverlay);
            SetActive(rhythmRoot, isRhythm);
            SetActive(finalRoot, isFinalOverlay);

            // ROUND 1 teaches the monster -> attack mapping; Fish/Crab/Jellyfish do not
            // appear from ROUND 2 on, so only the three attacks stay on screen there.
            bool monsterRound = flow.Phase == WarriorsBattlePhase.NormalBattle;
            SetActive(monsterGuideRoot, monsterRound && !isFinalOverlay);
            SetActive(attackGuideRoot, !monsterRound && !isFinalOverlay);

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

            Set(roundText, tentacle ? "ROUND 2  ·  촉수 절단" : "ROUND 1  ·  해변 방어");
            Set(timeText, $"{secondsLeft / 60:00}:{secondsLeft % 60:00}");
            Set(hpText, $"HP  {hp}");
            SetFill(hpFill, hp / (float)Mathf.Max(1, maxHp));
            Set(phaseValueText, tentacle
                ? $"잘라낸 촉수   {flow.TentacleSuccesses} / {flow.TentacleSuccessesRequired}"
                : $"처치 수   {score.Kills} / {score.TargetKills}");
            SetFill(phaseFill, progress);
            Set(scoreText, score.Score.ToString("N0"));
            Set(comboText, $"COMBO  {(combo != null ? combo.Combo : 0)}");
            int activePlayers = ResolveConnectedPlayers();
            for (int i = 0; i < playerStateTexts.Length; i++)
            {
                bool active = i < activePlayers;
                Set(playerStateTexts[i], $"{i + 1}P   {(active ? "READY" : "WAIT")}");
                if (i < playerStateFills.Length) SetFill(playerStateFills[i], active ? hp / (float)Mathf.Max(1, maxHp) : 0f);
            }
        }

        /// <summary>
        /// READY/WAIT must reflect the players that actually exist in the scene.
        /// flow.ActivePlayerCount is the gameplay slot count and is pinned to the
        /// configured 4P maximum, so every slot would read READY in a solo test.
        /// </summary>
        private int ResolveConnectedPlayers()
        {
            if (Time.unscaledTime >= nextPlayerScanTime)
            {
                nextPlayerScanTime = Time.unscaledTime + .5f;
                int found = FindObjectsByType<WarriorsPlayerCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                connectedPlayers = Mathf.Clamp(found, 1, 4);
            }
            return connectedPlayers;
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
                string title = null, body = null, objective = null;
                switch (flow.Phase)
                {
                    case WarriorsBattlePhase.NormalBattle:
                        title = "해변으로 몬스터들이 몰려오고 있습니다!";
                        body = "몬스터 종류에 맞는 공격으로 처치하세요!";
                        objective = "몬스터에 맞는 공격으로 처치하세요";
                        break;
                    case WarriorsBattlePhase.KrakenTentaclePhase:
                        title = "크라켄이 섬을 공격하기 시작했습니다!";
                        body = "촉수의 표시와 같은 공격을 사용하세요!";
                        objective = "촉수의 표시와 같은 공격을 사용하세요";
                        break;
                    case WarriorsBattlePhase.FinalKrakenPhase:
                        title = "크라켄이 마지막 공격을 준비합니다!";
                        body = "내려오는 공격 아이콘을 타이밍에 맞춰 공격하세요!";
                        objective = "공격 아이콘을 타이밍에 맞춰 공격하세요";
                        break;
                }

                if (title == null) { introUntil = 0f; objectiveLabel = string.Empty; }
                else
                {
                    Set(roundIntroTitle, title);
                    Set(roundIntroBody, body);
                    objectiveLabel = objective;
                    Set(objectiveText, objective);
                    introUntil = Time.unscaledTime + roundIntroSeconds;
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
                roundIntroRoot.transform.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, a);
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
            Set(rhythmRoundText, "ROUND 3  ·  최후의 일격");
            Set(rhythmTimeText, $"TIME  {secondsLeft / 60:00}:{secondsLeft % 60:00}");
            Set(rhythmBossText, "크라켄  ·  최종 전투");
            SetFill(rhythmBossFill, flow.FinalKrakenHealthPercent / 100f);
            Set(rhythmScoreText, $"SCORE  {score.Score:N0}");
            Set(rhythmComboText, $"COMBO  {rhythm.Combo}");
            Set(rhythmJudgementText, Korean(rhythm.ActiveJudgement));

            rhythm.CopyVisibleNotes(visibleNotes);

            // Notes are authored across the 4P slot count, but the lane only needs as many
            // columns as there are players actually here. Solo play therefore reads as one
            // clean central column instead of four columns spilling outside the lane.
            int playerCount = Mathf.Clamp(ResolveConnectedPlayers(), 1, 4);
            const float laneGap = 96f;
            float width = laneGap * (playerCount - 1);
            if (rhythmHitLine != null) rhythmHitLine.sizeDelta = new Vector2(Mathf.Max(180f, width + 140f), 5f);

            for (int i = 0; i < rhythmNotes.Length; i++)
            {
                bool visible = i < visibleNotes.Count;
                rhythmNotes[i].gameObject.SetActive(visible);
                if (!visible) continue;
                WarriorsRhythmNoteView note = visibleNotes[i];
                float x = playerCount <= 1 ? 0f : -width * .5f + Mathf.Min(note.PlayerIndex, playerCount - 1) * laneGap;
                float y = Mathf.Lerp(250f, -110f, note.Travel);
                rhythmNotes[i].anchoredPosition = new Vector2(x, y);
                if (i < rhythmNoteGlyphs.Length) Set(rhythmNoteGlyphs[i], Glyph(note.Type));
            }
        }

        private void UpdateFinalOverlay()
        {
            bool swing = flow.Phase == WarriorsBattlePhase.FinalSwingPhase;
            bool clear = flow.Phase == WarriorsBattlePhase.Clear;
            // The co-op swing is still live play, so the retry button only appears once
            // the run has actually ended.
            SetActive(retryButtonRoot, !swing);

            Set(finalEyebrowText, swing ? Korean(flow.FinalSwingTitle) : string.Empty);
            Set(finalTitleText, swing ? Korean(flow.FinalSwingLabel) : clear ? "승리!" : Korean(flow.FailureLabel));

            if (swing && flow.FinalSwingLabel == "SWING!")
                Set(finalDetailText, $"지금, 모두 함께 공격하세요!   {flow.SuccessfulSwingPlayerCount} / {flow.ActivePlayerCount}");
            else if (clear)
            {
                int elapsed = Mathf.FloorToInt(score.ElapsedSeconds);
                Set(finalDetailText, $"최종 점수   {score.Score:N0}\n플레이 시간   {elapsed / 60:00}:{elapsed % 60:00}\n몬스터 처치   {score.Kills}");
            }
            else Set(finalDetailText, string.Empty);
        }

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

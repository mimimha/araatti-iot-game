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
        [SerializeField] private TMP_Text[] playerStateTexts;
        [SerializeField] private Image[] playerStateFills;
        [SerializeField] private TMP_Text guideText;
        [SerializeField] private GameObject attackCardsRoot;

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

        private readonly List<WarriorsRhythmNoteView> visibleNotes = new();

        private void Awake()
        {
            ResolveSources();
            DisableLegacyHud();
        }

        private void OnEnable()
        {
            ResolveSources();
            DisableLegacyHud();
        }

        private void OnDisable()
        {
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
            SetActive(rhythmRoot, isRhythm);
            SetActive(finalRoot, isFinalOverlay);
            SetActive(attackCardsRoot, !isFinalOverlay);

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

            Set(roundText, tentacle ? "ROUND 2  ·  TENTACLE" : "ROUND 1  ·  BEACH RAID");
            Set(timeText, $"{secondsLeft / 60:00}:{secondsLeft % 60:00}");
            Set(hpText, $"HP  {hp}");
            SetFill(hpFill, hp / (float)Mathf.Max(1, maxHp));
            Set(phaseEyebrowText, tentacle ? "TENTACLE PHASE" : "WAVE PROGRESS");
            Set(phaseValueText, tentacle
                ? $"촉수 전투   {flow.TentacleSuccesses} / {flow.TentacleSuccessesRequired}"
                : $"처치 수   {score.Kills} / {score.TargetKills}");
            SetFill(phaseFill, progress);
            Set(scoreText, score.Score.ToString("N0"));
            Set(comboText, $"COMBO  {(combo != null ? combo.Combo : 0)}");
            Set(guideText, tentacle ? "4개의 촉수를 올바른 방향으로 베어 BREAK를 만드세요." : string.Empty);

            int activePlayers = Mathf.Clamp(flow.ActivePlayerCount, 1, 4);
            for (int i = 0; i < playerStateTexts.Length; i++)
            {
                bool active = i < activePlayers;
                Set(playerStateTexts[i], $"{i + 1}P   {(active ? "READY" : "WAIT")}");
                if (i < playerStateFills.Length) SetFill(playerStateFills[i], active ? hp / (float)Mathf.Max(1, maxHp) : 0f);
            }
        }

        private void UpdateSharedFeedback(bool finalOverlayVisible)
        {
            bool showAnnouncement = flow.IsTransitioning && !finalOverlayVisible;
            SetActive(announcementRoot, showAnnouncement);
            Set(announcementText, showAnnouncement ? flow.TransitionLabel : string.Empty);
            Set(specialText, combo != null ? combo.ActiveSpecial : string.Empty);
            Set(actionText, combatHud != null ? combatHud.ActiveActionLabel : string.Empty);
        }

        private void UpdateRhythmHud()
        {
            int secondsLeft = Mathf.CeilToInt(score.RemainingSeconds);
            Set(rhythmRoundText, "ROUND 3  ·  FINAL");
            Set(rhythmTimeText, $"TIME  {secondsLeft / 60:00}:{secondsLeft % 60:00}");
            Set(rhythmBossText, "KRAKEN  ·  RHYTHM BREAK");
            SetFill(rhythmBossFill, flow.FinalKrakenHealthPercent / 100f);
            Set(rhythmScoreText, $"SCORE  {score.Score:N0}");
            Set(rhythmComboText, $"COMBO  {rhythm.Combo}");
            Set(rhythmJudgementText, rhythm.ActiveJudgement);

            rhythm.CopyVisibleNotes(visibleNotes);
            int playerCount = rhythm.ActivePlayerCount;
            const float laneGap = 150f;
            float width = laneGap * (playerCount - 1);
            if (rhythmHitLine != null) rhythmHitLine.sizeDelta = new Vector2(Mathf.Max(220f, width + 150f), 5f);

            for (int i = 0; i < rhythmNotes.Length; i++)
            {
                bool visible = i < visibleNotes.Count;
                rhythmNotes[i].gameObject.SetActive(visible);
                if (!visible) continue;
                WarriorsRhythmNoteView note = visibleNotes[i];
                float x = -width * .5f + note.PlayerIndex * laneGap;
                float y = Mathf.Lerp(300f, -210f, note.Travel);
                rhythmNotes[i].anchoredPosition = new Vector2(x, y);
                if (i < rhythmNoteGlyphs.Length) Set(rhythmNoteGlyphs[i], Glyph(note.Type));
            }
        }

        private void UpdateFinalOverlay()
        {
            bool swing = flow.Phase == WarriorsBattlePhase.FinalSwingPhase;
            bool clear = flow.Phase == WarriorsBattlePhase.Clear;
            Set(finalEyebrowText, swing ? flow.FinalSwingTitle : string.Empty);
            Set(finalTitleText, swing ? flow.FinalSwingLabel : clear ? "GAME CLEAR" : flow.FailureLabel);

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

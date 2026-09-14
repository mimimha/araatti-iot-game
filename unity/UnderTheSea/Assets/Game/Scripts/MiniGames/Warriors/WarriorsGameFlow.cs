using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsGameFlow : MonoBehaviour
    {
        [SerializeField] private WarriorsBattleScore score;
        [SerializeField] private WarriorsEnemySpawner spawner;
        [SerializeField] private Transform normalEnemyRoot;
        [SerializeField] private Transform playersRoot;
        [SerializeField] private Transform stageStartPoint;
        [SerializeField] private WarriorsLocalPlayerController localPlayer;
        [SerializeField] private WarriorsHealth playerHealth;
        [SerializeField] private WarriorsThirdPersonCamera followCamera;
        [SerializeField] private WarriorsKrakenBoss kraken;
        [SerializeField] private WarriorsRhythmBattle rhythmBattle;
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private WarriorsIoTFeedbackHub iotFeedback;
        [SerializeField] private WarriorsIoTInput iotInput;
        private IWarriorsInputSource InputSource => inputSource as IWarriorsInputSource;
        private IWarriorsPlayerInputSource PlayerInputSource => inputSource as IWarriorsPlayerInputSource;
        private bool finalSwingReady;
        private bool finalSwingSucceeded;
        private bool transitioning;
        private string transitionLabel;
        private int countdown;
        private readonly HashSet<int> successfulSwingPlayers = new();
        private bool resultSent;
        private bool normalTransitionStarted;
        private CooperativeSwingKind swingKind;
        private Vector3 stageStartPosition;
        private Quaternion stageStartRotation;
        private bool hasStageStartPose;

        private enum CooperativeSwingKind { None, Ultimate, Finish }

        public WarriorsBattlePhase Phase { get; private set; } = WarriorsBattlePhase.NormalBattle;
        public int RemainingTentacles => kraken != null ? kraken.RemainingTentacles : 0;
        public int TentacleSuccesses => kraken != null ? kraken.TentacleSuccesses : 0;
        public int TentacleSuccessesRequired => kraken != null ? kraken.TentacleSuccessesRequired : 0;
        public int FinalKrakenHealthPercent => kraken != null ? kraken.FinalFormHealthPercent : 0;
        public WarriorsAttackDirection FinalWeakness => kraken != null ? kraken.FinalWeakness : WarriorsAttackDirection.HorizontalSlash;
        public float TeamGaugeNormalized => kraken != null ? kraken.TeamGaugeNormalized : 0f;
        public string FinalSwingTitle => swingKind == CooperativeSwingKind.Finish ? "FINISH CHANCE" : "ULTIMATE READY";
        public string FinalSwingLabel => finalSwingSucceeded ? "SUCCESS!" : finalSwingReady ? (swingKind == CooperativeSwingKind.Finish ? "FINAL SWING" : "SWING!") : countdown.ToString();
        public bool IsTransitioning => transitioning;
        public string TransitionLabel => transitionLabel;
        public string FailureLabel { get; private set; } = "GAME OVER";
        // Warriors is a 1-2 player game.  The four fixed tentacles that once justified a
        // 4P lobby are gone, so nothing in the battle assumes more than two swords.
        // One run is meant to land around three to four minutes, split across the rounds.
        [SerializeField, Min(20f)] private float round1Seconds = 75f;
        [SerializeField, Min(20f)] private float round2Seconds = 60f;
        [SerializeField, Min(20f)] private float round3Seconds = 90f;
        [SerializeField, Min(0)] private int patternScore = 200;
        [SerializeField, Min(0)] private int coopBonusScore = 500;
        [SerializeField, Min(0)] private int roundClearHeal = 25;
        [SerializeField, Range(1, 2)] private int configuredPlayerCount = 1;
        public int ActivePlayerCount { get; private set; } = 1;
        public int SuccessfulSwingPlayerCount => successfulSwingPlayers.Count;
        public event Action<WarriorsResult> Completed;

        private void OnEnable()
        {
            Phase = WarriorsBattlePhase.NormalBattle;
            FailureLabel = "GAME OVER";
            resultSent = false;
            normalTransitionStarted = false;
            if (localPlayer != null) localPlayer.enabled = true;
            if (playerHealth == null && localPlayer != null) playerHealth = localPlayer.GetComponent<WarriorsHealth>();
            if (playerHealth != null)
            {
                playerHealth.ResetHealth();
                playerHealth.Died -= HandlePlayerDied;
                playerHealth.Died += HandlePlayerDied;
                playerHealth.Damaged -= HandlePlayerDamaged;
                playerHealth.Damaged += HandlePlayerDamaged;
            }
            if (iotFeedback == null)
                iotFeedback = UnityEngine.Object.FindFirstObjectByType<WarriorsIoTFeedbackHub>(FindObjectsInactive.Include);
            if (iotInput == null)
                iotInput = UnityEngine.Object.FindFirstObjectByType<WarriorsIoTInput>(FindObjectsInactive.Include);
            if (rhythmBattle == null)
                rhythmBattle = UnityEngine.Object.FindFirstObjectByType<WarriorsRhythmBattle>(FindObjectsInactive.Include);
            rhythmBattle?.Configure(inputSource, score, kraken);
            CacheStageStartPose();
            ResetStagePosition();
            WarriorsPlayers.Changed -= RefreshActivePlayerCount;
            WarriorsPlayers.Changed += RefreshActivePlayerCount;
            RefreshActivePlayerCount();
            // Scaled from whatever the scene authored rather than from a number in here, so
            // a test scene that wants a short round keeps its short round.
            if (baseTargetKills <= 0 && score != null) baseTargetKills = score.TargetKills;
            score?.ConfigureTargetKills(baseTargetKills * ActivePlayerCount);
            spawner?.ConfigureForPlayers(ActivePlayerCount);
            score?.RestartClock(round1Seconds);
            if (score != null) score.BattleFinished += HandleNormalBattleFinished;
            // Neither the boss prefab nor the rhythm round can reference a scene player
            // on its own, and both need one now that they can hurt it.
            if (kraken != null) kraken.ConfigurePlayer(playerHealth);
            if (rhythmBattle != null) rhythmBattle.ConfigurePlayer(playerHealth);
            if (kraken != null) kraken.AllTentaclesDefeated += HandleTentaclesDefeated;
            if (kraken != null) kraken.PatternCleared += HandlePatternCleared;
            if (kraken != null) kraken.FinalFormDefeated += HandleFinalKrakenDefeated;
            if (InputSource != null) InputSource.AttackRequested += HandleAttack;
            if (PlayerInputSource != null) PlayerInputSource.PlayerAttackRequested += HandlePlayerAttack;
            if (rhythmBattle != null) rhythmBattle.Completed += HandleRhythmCompleted;
        }

        private void OnDisable()
        {
            WarriorsPlayers.Changed -= RefreshActivePlayerCount;
            if (score != null) score.BattleFinished -= HandleNormalBattleFinished;
            if (kraken != null) kraken.AllTentaclesDefeated -= HandleTentaclesDefeated;
            if (kraken != null) kraken.PatternCleared -= HandlePatternCleared;
            if (kraken != null) kraken.FinalFormDefeated -= HandleFinalKrakenDefeated;
            if (InputSource != null) InputSource.AttackRequested -= HandleAttack;
            if (PlayerInputSource != null) PlayerInputSource.PlayerAttackRequested -= HandlePlayerAttack;
            if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
            if (playerHealth != null) playerHealth.Damaged -= HandlePlayerDamaged;
            if (rhythmBattle != null) rhythmBattle.Completed -= HandleRhythmCompleted;
        }

        private void HandleNormalBattleFinished(bool success, int finalScore)
        {
            if (!success)
            {
                FailureLabel = "TIME OVER";
                Phase = WarriorsBattlePhase.Failed;
                score?.StopClock();
                if (localPlayer != null) localPlayer.enabled = false;
                PublishResult(false);
                return;
            }
            TryBeginKrakenBattle();
        }

        private void Update()
        {
            // The score component and flow can be enabled in either order when the
            // scene/domain reloads. Keep the event path, but also guard the phase
            // transition from a missed completion notification.
            if (Phase == WarriorsBattlePhase.NormalBattle &&
                score != null && score.Kills >= score.TargetKills)
                TryBeginKrakenBattle();
        }

        private void TryBeginKrakenBattle()
        {
            if (normalTransitionStarted || Phase != WarriorsBattlePhase.NormalBattle) return;
            normalTransitionStarted = true;
            StartCoroutine(BeginKrakenRoutine());
        }

        private IEnumerator BeginKrakenRoutine()
        {
            RefreshActivePlayerCount();
            // ROUND 1 owns its own clock.  It used to keep running into the tentacle and
            // rhythm rounds, so reaching ROUND 2 with time left still ended the whole run
            // in TIME OVER once that first countdown hit zero.
            score?.StopClock();
            playerHealth?.Heal(roundClearHeal);
            transitioning = true;
            transitionLabel = "해변의 몬스터를 막아냈습니다!";
            if (spawner != null) spawner.enabled = false;
            ClearBeachEnemies();
            yield return new WaitForSeconds(.85f);
            transitionLabel = "하지만 바다에서 거대한 기척이 느껴집니다...";
            yield return new WaitForSeconds(.9f);
            // Swept once more right before the kraken takes the stage: anything that landed
            // during the transition has no business being in the boss fight.
            ClearBeachEnemies();
            ResetStagePosition();
            transitioning = false;
            transitionLabel = string.Empty;
            Phase = WarriorsBattlePhase.KrakenTentaclePhase;
            score?.RestartClock(round2Seconds);
            kraken?.BeginBattle();
        }

        private void ClearBeachEnemies()
        {
            if (normalEnemyRoot == null) return;
            foreach (Transform child in normalEnemyRoot) Destroy(child.gameObject);
        }

        private void HandleTentaclesDefeated() => StartCoroutine(BeginRhythmRoundRoutine());

        /// <summary>
        /// Co-op is never required, so clearing a two tentacle pattern together pays extra
        /// rather than being the only way through it.
        /// </summary>
        private void HandlePatternCleared(bool coop)
        {
            score?.RegisterBossHit(coop ? coopBonusScore : patternScore);
            if (!coop) return;
            transitionLabel = "CO-OP BONUS";
            iotFeedback?.Request(0, WarriorsIoTFeedbackType.CorrectAttack, 1f);
        }

        private IEnumerator BeginRhythmRoundRoutine()
        {
            RefreshActivePlayerCount();
            playerHealth?.Heal(roundClearHeal);
            transitioning = true;
            transitionLabel = "모든 촉수를 무력화했습니다!\n크라켄의 방어가 무너집니다!";
            yield return new WaitForSeconds(1.2f);
            ResetStagePosition();
            kraken?.ShowFinalForm();
            Phase = WarriorsBattlePhase.FinalKrakenPhase;
            transitioning = false;
            transitionLabel = string.Empty;
            score?.RestartClock(round3Seconds);
            rhythmBattle?.Begin();
        }
        private IEnumerator FinalSwingRoutine(CooperativeSwingKind kind)
        {
            swingKind = kind;
            Phase = WarriorsBattlePhase.FinalSwingPhase;
            finalSwingReady = false;
            finalSwingSucceeded = false;
            successfulSwingPlayers.Clear();
            RefreshActivePlayerCount();
            for (countdown = 3; countdown >= 1; countdown--)
                yield return new WaitForSeconds(1f);
            finalSwingReady = true;
            for (int i = 0; i < ActivePlayerCount; i++)
                iotFeedback?.Request(i, WarriorsIoTFeedbackType.FinalSwingReady, 1f);
        }

        private void HandleAttack(WarriorsAttackDirection direction, float strength)
        {
            if (Phase == WarriorsBattlePhase.FinalSwingPhase && finalSwingReady)
            {
                RegisterPlayerSwing(0);
                return;
            }

            if (Phase == WarriorsBattlePhase.FinalKrakenPhase && rhythmBattle != null && rhythmBattle.IsActive) return;
            if (Phase != WarriorsBattlePhase.FinalKrakenPhase || kraken == null) return;
            if (!kraken.TryDamageFinalForm(direction, strength))
            {
                iotFeedback?.Request(0, WarriorsIoTFeedbackType.WrongAttack, strength);
                return;
            }

            score?.RegisterBossHit(250);
            iotFeedback?.Request(0, WarriorsIoTFeedbackType.CorrectAttack, strength);
            if (kraken.FinalFormHealthPercent <= 10)
            {
                StartCoroutine(FinalSwingRoutine(CooperativeSwingKind.Finish));
                return;
            }
            if (kraken.IsTeamGaugeReady)
            {
                for (int i = 0; i < ActivePlayerCount; i++)
                    iotFeedback?.Request(i, WarriorsIoTFeedbackType.UltimateReady, 1f);
                StartCoroutine(FinalSwingRoutine(CooperativeSwingKind.Ultimate));
            }
        }

        private void HandlePlayerAttack(WarriorsAttackInput input)
        {
            if (Phase == WarriorsBattlePhase.FinalSwingPhase && finalSwingReady)
                RegisterPlayerSwing(input.PlayerId);
        }

        public void RegisterPlayerSwing(int playerIndex)
        {
            if (Phase != WarriorsBattlePhase.FinalSwingPhase || !finalSwingReady) return;
            if (playerIndex < 0 || playerIndex >= ActivePlayerCount) return;
            successfulSwingPlayers.Add(playerIndex);
            if (successfulSwingPlayers.Count < ActivePlayerCount) return;
            finalSwingReady = false;
            finalSwingSucceeded = true;
            StartCoroutine(ResolveCooperativeSwing());
        }

        private IEnumerator ResolveCooperativeSwing()
        {
            yield return new WaitForSeconds(.65f);
            // Co-op is "faster and louder together", never "required": a solo run still
            // resolves the swing, a pair simply scores more for it.
            float bonus = ActivePlayerCount == 2 ? 1.3f : 1f;
            bool finish = swingKind == CooperativeSwingKind.Finish;
            // FinalFormDefeated is guarded by the active battle phase. Restore it
            // before applying cooperative damage so a finishing swing can clear.
            Phase = WarriorsBattlePhase.FinalKrakenPhase;
            kraken?.ApplyCooperativeDamage(bonus, finish);
            if (kraken != null && kraken.FinalFormHealth > 0)
            {
                Phase = WarriorsBattlePhase.FinalKrakenPhase;
                finalSwingSucceeded = false;
                swingKind = CooperativeSwingKind.None;
            }
        }

        private IEnumerator BeginFinalKrakenRoutine()
        {
            yield return new WaitForSeconds(.65f);
            ResetStagePosition();
            transitioning = true;
            transitionLabel = "최종 크라켄 등장";
            kraken?.ShowFinalForm();
            Phase = WarriorsBattlePhase.FinalKrakenPhase;
            yield return new WaitForSeconds(.8f);
            transitioning = false;
            transitionLabel = string.Empty;
            finalSwingSucceeded = false;
        }

        public void SetActivePlayerCount(int count)
        {
            ActivePlayerCount = Mathf.Clamp(count, 1, WarriorsPlayers.Max);
            successfulSwingPlayers.RemoveWhere(index => index >= ActivePlayerCount);
        }

        public void BindPlayersRoot(Transform root)
        {
            playersRoot = root;
            if (localPlayer == null && playersRoot != null)
                localPlayer = playersRoot.GetComponentInChildren<WarriorsLocalPlayerController>(true);
            CacheStageStartPose();
            ResetStagePosition();
            RefreshActivePlayerCount();
        }

        /// <summary>
        /// Taken from the live roster, so a player joining or leaving is picked up at once
        /// rather than leaving the rest of the run on the number that was true at startup.
        /// The players root is only a fallback for a scene that has registered nobody.
        /// </summary>
        private void RefreshActivePlayerCount()
        {
            if (WarriorsPlayers.Count > 0) { SetActivePlayerCount(WarriorsPlayers.Count); return; }
            if (playersRoot == null) { SetActivePlayerCount(configuredPlayerCount); return; }
            int count = playersRoot.GetComponentsInChildren<WarriorsPlayerCombat>(false).Length;
            SetActivePlayerCount(Mathf.Max(1, count));
        }

        private void HandleFinalKrakenDefeated()
        {
            if (Phase != WarriorsBattlePhase.FinalKrakenPhase || clearStarted) return;
            clearStarted = true;
            StartCoroutine(ClearRoutine());
        }

        private void HandleRhythmCompleted()
        {
            if (Phase != WarriorsBattlePhase.FinalKrakenPhase || clearStarted) return;
            clearStarted = true;
            StartCoroutine(ClearRoutine());
        }

        private void HandlePlayerDied()
        {
            if (Phase == WarriorsBattlePhase.Clear || Phase == WarriorsBattlePhase.Failed) return;
            StopAllCoroutines();
            transitioning = false;
            transitionLabel = string.Empty;
            FailureLabel = "GAME OVER";
            Phase = WarriorsBattlePhase.Failed;
            score?.StopClock();
            if (localPlayer != null) localPlayer.enabled = false;
            PublishResult(false);
        }

        private void HandlePlayerDamaged(int amount)
        {
            iotFeedback?.Request(0, WarriorsIoTFeedbackType.PlayerDamaged, Mathf.Clamp01(amount / 15f));
        }

        // The kraken can now be finished from either the pattern damage or the cooperative
        // swing, so the clear has to be one-shot.
        private bool clearStarted;
        private int baseTargetKills;

        private IEnumerator ClearRoutine()
        {
            transitioning = true;
            transitionLabel = "크라켄 격퇴 성공!\n바다의 심장 조각을 되찾았습니다!";
            score?.StopClock();
            kraken?.Defeat();
            yield return new WaitForSeconds(1.25f);
            Phase = WarriorsBattlePhase.Clear;
            transitioning = false;
            transitionLabel = string.Empty;
            if (localPlayer != null) localPlayer.enabled = false;
            PublishResult(true);
        }

        private void PublishResult(bool success)
        {
            if (resultSent) return;
            resultSent = true;
            Completed?.Invoke(new WarriorsResult(success, score != null ? score.Score : 0));
        }

        private void ResetStagePosition()
        {
            if (localPlayer == null && playersRoot != null)
                localPlayer = playersRoot.GetComponentInChildren<WarriorsLocalPlayerController>(true);
            CacheStageStartPose();
            if (!hasStageStartPose || localPlayer == null) return;
            localPlayer.RespawnAt(stageStartPosition, stageStartRotation);
            if (followCamera == null)
                followCamera = UnityEngine.Object.FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include);
            followCamera?.SnapToTarget(stageStartRotation.eulerAngles.y);
        }

        private void CacheStageStartPose()
        {
            if (hasStageStartPose) return;
            if (stageStartPoint == null)
            {
                Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Transform candidate in transforms)
                    if (candidate.name == "StageStartPoint") { stageStartPoint = candidate; break; }
                if (stageStartPoint == null)
                    foreach (Transform candidate in transforms)
                        if (candidate.name == "PlayerSpawn_01") { stageStartPoint = candidate; break; }
            }

            if (stageStartPoint != null)
            {
                stageStartPosition = stageStartPoint.position;
                stageStartRotation = stageStartPoint.rotation;
                hasStageStartPose = true;
            }
            else if (localPlayer != null)
            {
                // Prefab-only/dev scenes may not author a marker. Capture the player's
                // initial spawn once, never their later ROUND 1 position.
                stageStartPosition = localPlayer.transform.position;
                stageStartRotation = localPlayer.transform.rotation;
                hasStageStartPose = true;
            }
        }
    }
}

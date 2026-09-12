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
        [SerializeField, Range(1, 4)] private int configuredPlayerCount = 4;
        public int ActivePlayerCount { get; private set; } = 4;
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
            ResetStagePosition();
            RefreshActivePlayerCount();
            if (score != null) score.BattleFinished += HandleNormalBattleFinished;
            if (kraken != null) kraken.AllTentaclesDefeated += HandleTentaclesDefeated;
            if (kraken != null) kraken.FinalFormDefeated += HandleFinalKrakenDefeated;
            if (InputSource != null) InputSource.AttackRequested += HandleAttack;
            if (PlayerInputSource != null) PlayerInputSource.PlayerAttackRequested += HandlePlayerAttack;
            if (rhythmBattle != null) rhythmBattle.Completed += HandleRhythmCompleted;
        }

        private void OnDisable()
        {
            if (score != null) score.BattleFinished -= HandleNormalBattleFinished;
            if (kraken != null) kraken.AllTentaclesDefeated -= HandleTentaclesDefeated;
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
            transitioning = true;
            transitionLabel = "일반 전투 완료";
            if (spawner != null) spawner.enabled = false;
            if (normalEnemyRoot != null)
                foreach (Transform child in normalEnemyRoot) Destroy(child.gameObject);
            yield return new WaitForSeconds(1.5f);
            ResetStagePosition();
            transitioning = false;
            transitionLabel = string.Empty;
            Phase = WarriorsBattlePhase.KrakenTentaclePhase;
            kraken?.BeginBattle();
        }

        private void HandleTentaclesDefeated() => StartCoroutine(BeginRhythmRoundRoutine());

        private IEnumerator BeginRhythmRoundRoutine()
        {
            transitioning = true;
            transitionLabel = "BREAK!";
            yield return new WaitForSeconds(1f);
            ResetStagePosition();
            kraken?.ShowFinalForm();
            Phase = WarriorsBattlePhase.FinalKrakenPhase;
            transitioning = false;
            transitionLabel = string.Empty;
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
            float bonus = ActivePlayerCount switch { 2 => 1.3f, 3 => 1.6f, 4 => 2f, _ => 1f };
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
            ActivePlayerCount = Mathf.Clamp(count, 1, 4);
            successfulSwingPlayers.RemoveWhere(index => index >= ActivePlayerCount);
        }

        public void BindPlayersRoot(Transform root)
        {
            playersRoot = root;
            RefreshActivePlayerCount();
        }

        private void RefreshActivePlayerCount()
        {
            if (playersRoot == null) { SetActivePlayerCount(configuredPlayerCount); return; }
            int count = 0;
            foreach (Transform player in playersRoot)
                if (player.gameObject.activeInHierarchy) count++;
            SetActivePlayerCount(Mathf.Max(configuredPlayerCount, count));
        }

        private void HandleFinalKrakenDefeated()
        {
            if (Phase != WarriorsBattlePhase.FinalKrakenPhase) return;
            StartCoroutine(ClearRoutine());
        }

        private void HandleRhythmCompleted()
        {
            if (Phase != WarriorsBattlePhase.FinalKrakenPhase) return;
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

        private IEnumerator ClearRoutine()
        {
            transitioning = true;
            transitionLabel = "크라켄 격파";
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
            if (stageStartPoint == null) return;
            if (localPlayer == null && playersRoot != null)
                localPlayer = playersRoot.GetComponentInChildren<WarriorsLocalPlayerController>(true);
            localPlayer?.RespawnAt(stageStartPoint);
            if (followCamera == null)
                followCamera = UnityEngine.Object.FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include);
            followCamera?.SnapToTarget(stageStartPoint.eulerAngles.y);
        }
    }
}

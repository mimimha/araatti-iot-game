using System;
using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [Serializable]
    public sealed class FishingFishConfig
    {
        [SerializeField] private string fishId = "blue_mackerel";
        [SerializeField] private string displayName = "Blue Mackerel";
        [SerializeField] private string difficultyLabel = "Easy";
        [SerializeField, Min(0)] private int baseScore = 80;
        [SerializeField, Min(1f)] private float maxStamina = 65f;
        [SerializeField, Range(0f, 1f)] private float pullStrength = 0.32f;
        [SerializeField, Min(0.05f)] private float minBiteDelaySeconds = 1f;
        [SerializeField, Min(0.05f)] private float maxBiteDelaySeconds = 2.2f;
        [SerializeField, Min(0.05f)] private float hookWindowSeconds = 1.3f;
        [SerializeField, Min(1f)] private float fightTimeoutSeconds = 22f;
        [SerializeField, Min(0.1f)] private float reelEfficiencyMultiplier = 1.25f;
        [SerializeField, Min(0.1f)] private float difficultyScoreMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float rarityWeight = 5f;
        [SerializeField] private string visualKey = "blue_mackerel";

        [Header("Fight personality")]
        [SerializeField, Min(0.35f)] private float minBehaviorPhaseSeconds = 0.55f;
        [SerializeField, Min(0.35f)] private float maxBehaviorPhaseSeconds = 1.25f;
        [SerializeField, Range(0f, 1f)] private float runChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float restChance = 0.20f;
        [SerializeField, Range(0.02f, 0.42f)] private float maxTensionShift = 0.16f;
        [SerializeField, Range(0f, 1f)] private float directionChangeChance = 0.80f;

        public FishProfile BuildProfile()
        {
            return new FishProfile
            {
                FishId = fishId,
                DisplayName = displayName,
                DifficultyLabel = difficultyLabel,
                BaseScore = baseScore,
                MaxStamina = maxStamina,
                PullStrength = pullStrength,
                UseSpeciesRuleOverrides = true,
                MinBiteDelaySeconds = minBiteDelaySeconds,
                MaxBiteDelaySeconds = maxBiteDelaySeconds,
                HookWindowSeconds = hookWindowSeconds,
                FightTimeoutSeconds = fightTimeoutSeconds,
                ReelEfficiencyMultiplier = reelEfficiencyMultiplier,
                DifficultyScoreMultiplier = difficultyScoreMultiplier,
                RarityWeight = rarityWeight,
                VisualKey = visualKey,
                MinBehaviorPhaseSeconds = minBehaviorPhaseSeconds,
                MaxBehaviorPhaseSeconds = maxBehaviorPhaseSeconds,
                RunChance = runChance,
                RestChance = restChance,
                MaxTensionShift = maxTensionShift,
                DirectionChangeChance = directionChangeChance
            };
        }

        public static FishingFishConfig Create(
            string id,
            string name,
            string difficulty,
            int score,
            float stamina,
            float pull,
            float biteMin,
            float biteMax,
            float hookWindow,
            float fightTimeout,
            float reelEfficiency,
            float scoreMultiplier,
            float rarity,
            float phaseMin,
            float phaseMax,
            float runWeight,
            float restWeight,
            float tensionShift,
            float directionChanges)
        {
            return new FishingFishConfig
            {
                fishId = id,
                displayName = name,
                difficultyLabel = difficulty,
                baseScore = score,
                maxStamina = stamina,
                pullStrength = pull,
                minBiteDelaySeconds = biteMin,
                maxBiteDelaySeconds = biteMax,
                hookWindowSeconds = hookWindow,
                fightTimeoutSeconds = fightTimeout,
                reelEfficiencyMultiplier = reelEfficiency,
                difficultyScoreMultiplier = scoreMultiplier,
                rarityWeight = rarity,
                visualKey = id,
                minBehaviorPhaseSeconds = phaseMin,
                maxBehaviorPhaseSeconds = phaseMax,
                runChance = runWeight,
                restChance = restWeight,
                maxTensionShift = tensionShift,
                directionChangeChance = directionChanges
            };
        }
    }

    [CreateAssetMenu(fileName = "FishingCheckpointBConfig", menuName = "Fishing Mini Game/Checkpoint B Config")]
    public sealed class FishingGameConfigAsset : ScriptableObject
    {
        [Header("Game mode")]
        [SerializeField] private FishingGameMode gameMode = FishingGameMode.LegacyRound;
        [SerializeField] private string sessionFishId = "red_sea_bream";

        [Header("Round")]
        [SerializeField, Min(0.1f)] private float roundDurationSeconds = 180f;
        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField] private bool cycleFishInCatalogOrder;
        [SerializeField] private int randomSeed = 20260821;

        [Header("Cast and default bite rules")]
        [SerializeField, Min(0.1f)] private float maxCastChargeSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float minBiteDelaySeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float maxBiteDelaySeconds = 3.5f;
        [SerializeField, Min(0.05f)] private float hookWindowSeconds = 0.9f;
        [SerializeField, Min(0f)] private float hookSettleSeconds = 0.35f;

        [Header("Fishing V2 pre-fight")]
        [SerializeField, Min(0f)] private float nibbleLeadMinSeconds = 0.65f;
        [SerializeField, Min(0f)] private float nibbleLeadMaxSeconds = 1f;
        [SerializeField, Min(0.01f)] private float nibbleDurationSeconds = 0.28f;
        [SerializeField, Min(0f)] private float nibbleToBiteGapSeconds = 0.18f;
        [SerializeField, Range(0f, 1f)] private float nibbleIntensityNormalized = 0.28f;
        [SerializeField, Min(0f)] private float earlyHookPenaltyMinSeconds = 0.6f;
        [SerializeField, Min(0f)] private float earlyHookPenaltyMaxSeconds = 1f;
        [SerializeField, Min(0.01f)] private float missedBiteRetryMinSeconds = 0.75f;
        [SerializeField, Min(0.01f)] private float missedBiteRetryMaxSeconds = 1.2f;

        [Header("Fight")]
        [SerializeField, Range(0f, 1f)] private float safeTensionMin = 0.30f;
        [SerializeField, Range(0f, 1f)] private float safeTensionMax = 0.75f;
        [SerializeField, Min(0.1f)] private float slackEscapeSeconds = 2f;
        [SerializeField, Min(0.1f)] private float highTensionGraceSeconds = 1.2f;
        [SerializeField, Min(0f)] private float highTensionDamagePerSecond = 60f;
        [SerializeField, Min(0.1f)] private float fishDamagePerSecond = 36f;
        [SerializeField, Min(1f)] private float fightTimeoutSeconds = 25f;

        [Header("Flow")]
        [SerializeField, Min(0f)] private float outcomeDisplaySeconds = 1.3f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 1f;

        [Header("Three-fish catalog")]
        [SerializeField] private List<FishingFishConfig> fishCatalog = new List<FishingFishConfig>();

        public int RandomSeed => randomSeed;
        public FishingGameMode GameMode => gameMode;
        public string SessionFishId => sessionFishId;
        public float RoundDurationSeconds => roundDurationSeconds;
        public float CountdownSeconds => countdownSeconds;
        public bool CycleFishInCatalogOrder => cycleFishInCatalogOrder;
        public int FishCount => fishCatalog?.Count ?? 0;

        public FishingRules BuildRules()
        {
            return new FishingRules
            {
                MaxCastChargeSeconds = maxCastChargeSeconds,
                MinBiteDelaySeconds = minBiteDelaySeconds,
                MaxBiteDelaySeconds = maxBiteDelaySeconds,
                HookWindowSeconds = hookWindowSeconds,
                HookSettleSeconds = hookSettleSeconds,
                FightTimeoutSeconds = fightTimeoutSeconds,
                SafeTensionMin = safeTensionMin,
                SafeTensionMax = safeTensionMax,
                SlackEscapeSeconds = slackEscapeSeconds,
                HighTensionGraceSeconds = highTensionGraceSeconds,
                HighTensionDamagePerSecond = highTensionDamagePerSecond,
                FishDamagePerSecond = fishDamagePerSecond,
                OutcomeDisplaySeconds = outcomeDisplaySeconds,
                CooldownSeconds = cooldownSeconds
            };
        }

        public FishingV2PreFightTuning BuildV2PreFightTuning()
        {
            FishingV2PreFightTuning tuning = new FishingV2PreFightTuning
            {
                NibbleLeadMinSeconds = nibbleLeadMinSeconds,
                NibbleLeadMaxSeconds = nibbleLeadMaxSeconds,
                NibbleDurationSeconds = nibbleDurationSeconds,
                NibbleToBiteGapSeconds = nibbleToBiteGapSeconds,
                NibbleIntensityNormalized = nibbleIntensityNormalized,
                EarlyHookPenaltyMinSeconds = earlyHookPenaltyMinSeconds,
                EarlyHookPenaltyMaxSeconds = earlyHookPenaltyMaxSeconds,
                MissedBiteRetryMinSeconds = missedBiteRetryMinSeconds,
                MissedBiteRetryMaxSeconds = missedBiteRetryMaxSeconds
            };
            tuning.Sanitize();
            return tuning;
        }

        public FishProfile[] BuildFishProfiles()
        {
            EnsureCheckpointBDefaults();
            FishProfile[] profiles = new FishProfile[fishCatalog.Count];
            for (int i = 0; i < fishCatalog.Count; i++)
            {
                profiles[i] = (fishCatalog[i] ?? new FishingFishConfig()).BuildProfile();
                profiles[i].Sanitize();
            }
            return profiles;
        }

        public FishProfile BuildFishProfile()
        {
            return BuildFishProfiles()[0];
        }

        public FishProfile BuildSessionFishProfile()
        {
            FishProfile[] profiles = BuildFishProfiles();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i].FishId, sessionFishId, StringComparison.OrdinalIgnoreCase))
                {
                    return profiles[i];
                }
            }

            return profiles[0];
        }

        public FishingLaunchContext BuildLaunchContext(string roundId = "standalone-round")
        {
            return new FishingLaunchContext
            {
                RoundId = roundId,
                LocalParticipantId = "local-player",
                RoundDurationSeconds = roundDurationSeconds,
                CountdownSeconds = countdownSeconds,
                Seed = randomSeed
            };
        }

        public void EnsureCheckpointBDefaults()
        {
            bool hasSeaCatalog = fishCatalog != null &&
                fishCatalog.Count >= 3 &&
                fishCatalog[0] != null &&
                fishCatalog[0].BuildProfile().FishId == "blue_mackerel";
            if (hasSeaCatalog) return;
            fishCatalog = new List<FishingFishConfig>
            {
                FishingFishConfig.Create("blue_mackerel", "Blue Mackerel", "Easy", 80, 65f, 0.32f, 1f, 2.2f, 1.3f, 22f, 1.25f, 1f, 5f, 0.55f, 1.25f, 0.55f, 0.20f, 0.16f, 0.80f),
                FishingFishConfig.Create("red_sea_bream", "Red Sea Bream", "Normal", 170, 105f, 0.58f, 1.5f, 3.2f, 0.95f, 25f, 1f, 1.3f, 3f, 0.85f, 1.90f, 0.35f, 0.35f, 0.21f, 0.48f),
                FishingFishConfig.Create("greater_amberjack", "Greater Amberjack", "Hard", 340, 160f, 0.90f, 1.8f, 4f, 0.65f, 24f, 0.78f, 1.7f, 1f, 0.70f, 2.40f, 0.62f, 0.14f, 0.30f, 0.72f)
            };
        }
    }
}

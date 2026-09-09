using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [RequireComponent(typeof(FishingMiniGameFacade))]
    [DisallowMultipleComponent]
    public sealed class FishingStandaloneBootstrap : MonoBehaviour
    {
        [SerializeField] private FishingGameConfigAsset config;
        [SerializeField] private FishingMiniGameFacade facade;
        private bool _initializedForPlay;

        public void Configure(FishingGameConfigAsset gameConfig)
        {
            config = gameConfig;
        }

        public void RestartRound()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
            if (config == null || facade == null) return;

            facade.Initialize(BuildStandaloneLaunchContext());
            facade.BeginRound();
            _initializedForPlay = true;
        }

        private void OnEnable()
        {
            _initializedForPlay = false;
            TryInitializeStandaloneRound();
        }

        private void Start()
        {
            TryInitializeStandaloneRound();
        }

        private void Update()
        {
            if (!_initializedForPlay) TryInitializeStandaloneRound();
        }

        private void TryInitializeStandaloneRound()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
            if (config == null || facade == null || facade.Round == null) return;
            if (_initializedForPlay && facade.Round.State != FishingRoundState.Uninitialized) return;

            facade.Initialize(BuildStandaloneLaunchContext());
            _initializedForPlay = true;
        }

        private FishingLaunchContext BuildStandaloneLaunchContext()
        {
            FishingLaunchContext context = config.BuildLaunchContext($"standalone-{DateTime.UtcNow.Ticks}");
            context.Seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Ticks.GetHashCode());
            return context;
        }
    }
}

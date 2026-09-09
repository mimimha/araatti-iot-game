using System;
using FishingMiniGame.Core;

namespace FishingMiniGame.Runtime
{
    public sealed class LocalFishingAuthority : IFishingAuthority
    {
        private FishingStateMachine _stateMachine;

        public FishingSnapshot Current => _stateMachine?.Current;
        public FishingCycleResult LastResult => _stateMachine?.LastResult;
        public event Action<FishingCycleResult> CycleFinished;

        public void Initialize(FishingRoundContext context)
        {
            _stateMachine = new FishingStateMachine();
            _stateMachine.CycleFinished += OnCycleFinished;
            _stateMachine.Initialize(context);
        }

        public void Tick(FishingInputFrame input, float deltaTime)
        {
            if (_stateMachine == null) throw new InvalidOperationException("LocalFishingAuthority is not initialized.");
            _stateMachine.Tick(input, deltaTime);
        }

        public void PrepareCycle(FishProfile fish, FishingRules rules = null)
        {
            if (_stateMachine == null) throw new InvalidOperationException("LocalFishingAuthority is not initialized.");
            _stateMachine.PrepareCycle(fish, rules);
        }

        public void ResetCycle()
        {
            if (_stateMachine == null) return;
            _stateMachine.ResetCycle();
        }

        private void OnCycleFinished(FishingCycleResult result)
        {
            CycleFinished?.Invoke(result);
        }
    }
}

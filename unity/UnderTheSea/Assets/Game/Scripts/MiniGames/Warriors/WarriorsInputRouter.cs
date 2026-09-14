using System;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsInputRouter : MonoBehaviour, IWarriorsInputSource, IWarriorsPlayerInputSource
    {
        [SerializeField] private MonoBehaviour[] inputSources;

        public event Action<WarriorsAttackDirection, float> AttackRequested;
        public event Action DodgeRequested;
        public event Action<WarriorsAttackInput> PlayerAttackRequested;
        private bool subscribed;

        private void OnEnable()
        {
            Subscribe(inputSources ?? Array.Empty<MonoBehaviour>());
        }

        private void OnDisable()
        {
            Unsubscribe(inputSources ?? Array.Empty<MonoBehaviour>());
        }

        public void Configure(params MonoBehaviour[] sources)
        {
            if (isActiveAndEnabled) Unsubscribe(inputSources ?? Array.Empty<MonoBehaviour>());
            inputSources = sources ?? Array.Empty<MonoBehaviour>();
            if (isActiveAndEnabled) Subscribe(inputSources);
        }

        private void Subscribe(MonoBehaviour[] sources)
        {
            if (subscribed) return;
            foreach (MonoBehaviour source in sources)
            {
                if (source is not IWarriorsInputSource input || ReferenceEquals(input, this)) continue;
                input.AttackRequested += ForwardAttack;
                input.DodgeRequested += ForwardDodge;
                if (source is IWarriorsPlayerInputSource playerInput)
                    playerInput.PlayerAttackRequested += ForwardPlayerAttack;
            }
            subscribed = true;
        }

        private void Unsubscribe(MonoBehaviour[] sources)
        {
            if (!subscribed) return;
            foreach (MonoBehaviour source in sources)
            {
                if (source is not IWarriorsInputSource input || ReferenceEquals(input, this)) continue;
                input.AttackRequested -= ForwardAttack;
                input.DodgeRequested -= ForwardDodge;
                if (source is IWarriorsPlayerInputSource playerInput)
                    playerInput.PlayerAttackRequested -= ForwardPlayerAttack;
            }
            subscribed = false;
        }
        private void ForwardAttack(WarriorsAttackDirection type, float strength) => AttackRequested?.Invoke(type, strength);
        private void ForwardDodge() => DodgeRequested?.Invoke();
        private void ForwardPlayerAttack(WarriorsAttackInput input) => PlayerAttackRequested?.Invoke(input);
    }
}

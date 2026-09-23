using System;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// A request raised by a <see cref="FishingSpot"/> after an external interaction
    /// system has validated and forwarded an interaction.
    /// </summary>
    public readonly struct FishingSpotInteractionRequest
    {
        public FishingSpotInteractionRequest(FishingSpot spot, GameObject interactor)
        {
            Spot = spot;
            Interactor = interactor;
        }

        public FishingSpot Spot { get; }

        public GameObject Interactor { get; }
    }

    /// <summary>
    /// Reusable world endpoint for requesting entry into fishing mode.
    /// Input, player ownership, and session startup belong to external integration code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingSpot : MonoBehaviour
    {
        [SerializeField] private bool interactionEnabled = true;
        [SerializeField] private string promptText = "낚시하기";

        private bool _isBusy;

        public event Action<FishingSpotInteractionRequest> FishingRequested;

        public bool InteractionEnabled => interactionEnabled;

        public bool IsBusy => _isBusy;

        public bool CanInteract => isActiveAndEnabled && interactionEnabled && !_isBusy;

        public string PromptText => promptText;

        /// <summary>
        /// Raises one fishing request and reserves the spot until <see cref="Release"/>
        /// is called. The reservation also rejects re-entrant duplicate requests.
        /// </summary>
        public bool TryInteract(GameObject interactor)
        {
            if (!CanInteract || interactor == null)
            {
                return false;
            }

            _isBusy = true;
            FishingRequested?.Invoke(new FishingSpotInteractionRequest(this, interactor));
            return true;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;

            if (!enabled)
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// Releases the local pending/in-use guard. The future fishing mode owner calls
        /// this after rejecting a request or leaving fishing mode.
        /// </summary>
        public void Release()
        {
            _isBusy = false;
        }

        private void OnDisable()
        {
            _isBusy = false;
        }
    }
}

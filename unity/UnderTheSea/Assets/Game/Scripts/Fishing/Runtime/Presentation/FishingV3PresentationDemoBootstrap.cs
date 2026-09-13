using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Initialization host for the isolated V3 presentation development scene.
    /// Production integration should configure the same facade from FishingMode.
    /// </summary>
    [RequireComponent(typeof(FishingMiniGameFacade))]
    [DisallowMultipleComponent]
    public sealed class FishingV3PresentationDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private FishingV3FishState initialFishState = FishingV3FishState.Fight;

        private void Start()
        {
            if (facade == null) facade = GetComponent<FishingMiniGameFacade>();
            facade.ConfigureV3Runtime();
            facade.SetV3FishState(initialFishState);
            facade.BeginRound();
        }

    }
}

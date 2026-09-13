using UnityEngine;
using UnityEngine.UI;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Read-only presentation adapter for the public Fishing V3 snapshot.
    /// It never advances gameplay or derives gameplay values from input/state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingV3HudPresenter : MonoBehaviour
    {
        [SerializeField] private FishingMiniGameFacade facade;
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Image tensionFill;
        [SerializeField] private Image captureFill;
        [SerializeField] private Text tensionValueLabel;
        [SerializeField] private Text captureValueLabel;

        public float DisplayedTensionNormalized { get; private set; }
        public float DisplayedCaptureProgressNormalized { get; private set; }
        public bool IsHudVisible => hudRoot != null && hudRoot.activeSelf;
        public bool HasConfiguredView => hudRoot != null && tensionFill != null && captureFill != null;

        public void Configure(
            FishingMiniGameFacade snapshotSource,
            GameObject root,
            Image tensionGaugeFill,
            Image captureGaugeFill,
            Text tensionLabel = null,
            Text captureLabel = null)
        {
            facade = snapshotSource;
            hudRoot = root;
            tensionFill = tensionGaugeFill;
            captureFill = captureGaugeFill;
            tensionValueLabel = tensionLabel;
            captureValueLabel = captureLabel;
            ConfigureFill(tensionFill);
            ConfigureFill(captureFill);
            HideAndReset();
        }

        private void Awake()
        {
            ResolveFacade();
            ConfigureFill(tensionFill);
            ConfigureFill(captureFill);
            RefreshNow();
        }

        private void OnEnable()
        {
            RefreshNow();
        }

        private void Update()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            ResolveFacade();
            FishingV3Snapshot snapshot = facade != null ? facade.V3Current : null;
            if (!HasConfiguredView ||
                facade == null ||
                facade.GameplayRuntimeMode != FishingGameplayRuntimeMode.V3 ||
                snapshot == null)
            {
                HideAndReset();
                return;
            }

            switch (snapshot.RuntimeState)
            {
                case FishingV3RuntimeState.Running:
                case FishingV3RuntimeState.Paused:
                case FishingV3RuntimeState.Completed:
                    SetVisible(true);
                    ApplySnapshot(snapshot);
                    break;

                default:
                    HideAndReset();
                    break;
            }
        }

        public static float NormalizeGaugeValue(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
        }

        private void ApplySnapshot(FishingV3Snapshot snapshot)
        {
            DisplayedTensionNormalized = NormalizeGaugeValue(snapshot.TensionNormalized);
            DisplayedCaptureProgressNormalized = NormalizeGaugeValue(
                snapshot.CaptureProgressNormalized);
            SetGauge(tensionFill, tensionValueLabel, DisplayedTensionNormalized);
            SetGauge(captureFill, captureValueLabel, DisplayedCaptureProgressNormalized);
        }

        private void HideAndReset()
        {
            DisplayedTensionNormalized = 0f;
            DisplayedCaptureProgressNormalized = 0f;
            SetGauge(tensionFill, tensionValueLabel, 0f);
            SetGauge(captureFill, captureValueLabel, 0f);
            SetVisible(false);
        }

        private void ResolveFacade()
        {
            if (facade == null) facade = GetComponentInParent<FishingMiniGameFacade>();
        }

        private void SetVisible(bool visible)
        {
            if (hudRoot != null && hudRoot != gameObject && hudRoot.activeSelf != visible)
            {
                hudRoot.SetActive(visible);
            }
        }

        private static void ConfigureFill(Image fill)
        {
            if (fill == null) return;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private static void SetGauge(Image fill, Text label, float value)
        {
            float normalized = NormalizeGaugeValue(value);
            if (fill != null) fill.fillAmount = normalized;
            if (label != null) label.text = $"{normalized * 100f:0}%";
        }
    }
}

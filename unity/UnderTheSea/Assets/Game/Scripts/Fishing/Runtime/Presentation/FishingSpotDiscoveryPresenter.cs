using UnityEngine;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Keeps the fishing spot discovery label local-only and presentation-only.
    /// The label is a child of the spot, so level designers can move both together.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FishingSpot))]
    public sealed class FishingSpotDiscoveryPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject labelRoot;
        [SerializeField, Min(0f)] private float visibleDistance = 12f;

        private FishingSpot _spot;
        private FishingModeController _modeController;
        private Camera _viewCamera;

        public GameObject LabelRoot => labelRoot;
        public float VisibleDistance => visibleDistance;
        public bool IsLabelVisible => labelRoot != null && labelRoot.activeSelf;

        private void Awake()
        {
            _spot = GetComponent<FishingSpot>();
            _modeController = FindAnyObjectByType<FishingModeController>();
            SetLabelVisible(false);
        }

        private void LateUpdate()
        {
            Camera view = ResolveViewCamera();
            FishingModeController modeController = ResolveModeController();
            bool visible = view != null &&
                           _spot != null &&
                           !_spot.IsBusy &&
                           (modeController == null || !modeController.IsFishing) &&
                           (view.transform.position - transform.position).sqrMagnitude <=
                           visibleDistance * visibleDistance;

            SetLabelVisible(visible);
            if (visible)
            {
                labelRoot.transform.forward = view.transform.forward;
            }
        }

        private void OnDisable()
        {
            SetLabelVisible(false);
        }

        private void OnValidate()
        {
            visibleDistance = Mathf.Max(0f, visibleDistance);
        }

        private Camera ResolveViewCamera()
        {
            if (_viewCamera == null || !_viewCamera.isActiveAndEnabled)
            {
                _viewCamera = Camera.main;
            }

            return _viewCamera;
        }

        private FishingModeController ResolveModeController()
        {
            if (_modeController == null)
            {
                _modeController = FindAnyObjectByType<FishingModeController>();
            }

            return _modeController;
        }

        private void SetLabelVisible(bool visible)
        {
            if (labelRoot != null && labelRoot.activeSelf != visible)
            {
                labelRoot.SetActive(visible);
            }
        }
    }
}

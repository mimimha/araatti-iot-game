using FishingMiniGame.Core;
using Fusion;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Replicates only the semantic state required to render fishing on another
    /// client. Local gameplay remains owned by the scene FishingModeController.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerFishingPresentation : NetworkBehaviour
    {
        private const string RemoteAnchorName = "RemoteFishingVisualAnchor";

        [SerializeField] private Vector3 remoteFishAnchorLocalPosition =
            new Vector3(0f, -0.63f, 2.4f);
        [SerializeField] private Vector3 remoteFishAnchorLocalEulerAngles =
            new Vector3(0f, 90f, 0f);

        [Networked] public NetworkBool FishingActive { get; private set; }
        [Networked] public NetworkBool FishingPaused { get; private set; }
        [Networked] public int FishingPhaseCode { get; private set; }
        [Networked] public int TerminalResultCode { get; private set; }
        [Networked] public int FishProfileCode { get; private set; }
        [Networked] public NetworkString<_16> FishVisualId { get; private set; }
        [Networked] public int FishStateCode { get; private set; }
        [Networked] public int TensionZoneCode { get; private set; }
        [Networked] public int HeadShakeEventSequence { get; private set; }
        [Networked] public int HeadShakeIntensityByte { get; private set; }
        [Networked] public int SessionSequence { get; private set; }
        [Networked] public int PresentationRevision { get; private set; }

        private bool _spawned;
        private bool _hasPublishedState;
        private int _localSessionSequence;
        private FishingV3NetworkPresentationState _lastPublishedState =
            FishingV3NetworkPresentationState.Inactive;
        private FishingV3NetworkPresentationState _lastRenderedState =
            FishingV3NetworkPresentationState.Inactive;
        private Transform _remoteFishAnchor;
        private FishingV3PlayerPresentation _remotePlayerPresenter;

        public FishingV3NetworkPresentationState LastRenderedState => _lastRenderedState;
        public bool HasRemoteRod =>
            _remotePlayerPresenter != null && _remotePlayerPresenter.HasRodVisual;
        public int RemoteRodCount => HasRemoteRod ? 1 : 0;
        public int RemoteFishCount => 0;
        public Transform RemoteFishAnchor => _remoteFishAnchor;

        public override void Spawned()
        {
            _spawned = true;
            if (!HasInputAuthority && Runner != null && !Runner.IsServer)
            {
                EnsureRemotePresenters();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _spawned = false;
            CleanupRemotePresentation(true);
        }

        private void OnDestroy()
        {
            CleanupRemotePresentation(true);
        }

        public override void Render()
        {
            if (!_spawned)
            {
                return;
            }

            // The owning client already has the full local presentation. The
            // dedicated server never creates cosmetic GameObjects.
            if (HasInputAuthority || Runner == null || Runner.IsServer)
            {
                CleanupRemotePresentation(false);
                return;
            }

            FishingV3NetworkPresentationState state = ReadReplicatedState();
            _lastRenderedState = state;
            if (!state.HasPresentation &&
                _remotePlayerPresenter == null)
            {
                return;
            }

            EnsureRemotePresenters();
            FishingV3RemotePresentationFrame frame = state.ToRemoteFrame();
            float deltaTime = Time.unscaledDeltaTime;
            _remotePlayerPresenter.StepRemotePresentation(frame, deltaTime);
        }

        /// <summary>
        /// Called only by the local PlayerFishingAdapter. Equality filtering
        /// keeps RPC traffic to meaningful presentation-state changes.
        /// </summary>
        public bool PublishLocalSnapshot(
            bool isFishing,
            bool isPaused,
            FishingV3Snapshot snapshot)
        {
            if (!_spawned || !HasInputAuthority)
            {
                return false;
            }

            if (isFishing && (!_hasPublishedState || !_lastPublishedState.IsFishing))
            {
                _localSessionSequence = _localSessionSequence == int.MaxValue
                    ? 1
                    : _localSessionSequence + 1;
            }

            FishingV3NetworkPresentationState state =
                FishingV3NetworkPresentationState.FromSnapshot(
                    _localSessionSequence,
                    isFishing,
                    isPaused,
                    snapshot);
            if (_hasPublishedState && state.Equals(_lastPublishedState))
            {
                return false;
            }

            _lastPublishedState = state;
            _hasPublishedState = true;
            Rpc_SubmitFishingPresentation(
                state.SessionSequence,
                state.IsFishing,
                state.IsPaused,
                (int)state.GameplayPhase,
                (int)state.Result,
                (int)state.FishProfileId,
                state.FishVisualId,
                (int)state.FishState,
                (int)state.TensionZone,
                state.HeadShakeEventSequence,
                QuantizeNormalized(state.HeadShakeIntensityNormalized));
            return true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SubmitFishingPresentation(
            int sessionSequence,
            bool isFishing,
            bool isPaused,
            int gameplayPhaseCode,
            int terminalResultCode,
            int fishProfileCode,
            string fishVisualId,
            int fishStateCode,
            int tensionZoneCode,
            int headShakeEventSequence,
            int headShakeIntensityByte,
            RpcInfo info = default)
        {
            if (info.Source != Object.InputAuthority)
            {
                Debug.LogWarning(
                    $"[NetworkFishing] 주인이 아닌 {info.Source}의 낚시 연출 상태를 버립니다. " +
                    $"주인={Object.InputAuthority}",
                    this);
                return;
            }

            FishingV3NetworkPresentationState state =
                new FishingV3NetworkPresentationState(
                    sessionSequence,
                    isFishing,
                    isPaused,
                    (FishingV3GameplayPhase)gameplayPhaseCode,
                    (FishingV3Result)terminalResultCode,
                    (FishingV3FishProfileId)fishProfileCode,
                    fishVisualId,
                    (FishingV3FishState)fishStateCode,
                    (FishingV3TensionZone)tensionZoneCode,
                    headShakeEventSequence,
                    DequantizeNormalized(headShakeIntensityByte));

            FishingActive = state.IsFishing;
            FishingPaused = state.IsPaused;
            FishingPhaseCode = (int)state.GameplayPhase;
            TerminalResultCode = (int)state.Result;
            FishProfileCode = (int)state.FishProfileId;
            FishVisualId = state.FishVisualId;
            FishStateCode = (int)state.FishState;
            TensionZoneCode = (int)state.TensionZone;
            HeadShakeEventSequence = state.HeadShakeEventSequence;
            HeadShakeIntensityByte = QuantizeNormalized(
                state.HeadShakeIntensityNormalized);
            SessionSequence = state.SessionSequence;
            PresentationRevision = PresentationRevision == int.MaxValue
                ? 1
                : PresentationRevision + 1;
        }

        private FishingV3NetworkPresentationState ReadReplicatedState()
        {
            return new FishingV3NetworkPresentationState(
                SessionSequence,
                FishingActive,
                FishingPaused,
                (FishingV3GameplayPhase)FishingPhaseCode,
                (FishingV3Result)TerminalResultCode,
                (FishingV3FishProfileId)FishProfileCode,
                FishVisualId.Value,
                (FishingV3FishState)FishStateCode,
                (FishingV3TensionZone)TensionZoneCode,
                HeadShakeEventSequence,
                DequantizeNormalized(HeadShakeIntensityByte));
        }

        private void EnsureRemotePresenters()
        {
            if (_remoteFishAnchor == null)
            {
                Transform existing = transform.Find(RemoteAnchorName);
                if (existing != null)
                {
                    _remoteFishAnchor = existing;
                }
                else
                {
                    GameObject anchor = new GameObject(RemoteAnchorName);
                    _remoteFishAnchor = anchor.transform;
                    _remoteFishAnchor.SetParent(transform, false);
                }

                _remoteFishAnchor.localPosition = remoteFishAnchorLocalPosition;
                _remoteFishAnchor.localRotation = Quaternion.Euler(
                    remoteFishAnchorLocalEulerAngles);
                _remoteFishAnchor.localScale = Vector3.one;
            }

            if (_remotePlayerPresenter == null)
            {
                _remotePlayerPresenter = GetComponent<FishingV3PlayerPresentation>();
                if (_remotePlayerPresenter == null)
                {
                    _remotePlayerPresenter = gameObject.AddComponent<
                        FishingV3PlayerPresentation>();
                }
                _remotePlayerPresenter.ConfigureRemotePlayer(
                    transform,
                    _remoteFishAnchor);
            }
        }

        private void CleanupRemotePresentation(bool destroyInfrastructure)
        {
            _remotePlayerPresenter?.ClearRemotePresentation();
            _lastRenderedState = FishingV3NetworkPresentationState.Inactive;

            if (!destroyInfrastructure || _remoteFishAnchor == null)
            {
                return;
            }

            GameObject anchor = _remoteFishAnchor.gameObject;
            _remoteFishAnchor = null;
            if (Application.isPlaying)
            {
                Destroy(anchor);
            }
            else
            {
                DestroyImmediate(anchor);
            }
        }

        private static int QuantizeNormalized(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * byte.MaxValue);
        }

        private static float DequantizeNormalized(int value)
        {
            return Mathf.Clamp(value, 0, byte.MaxValue) / (float)byte.MaxValue;
        }
    }
}

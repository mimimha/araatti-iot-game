using UnderTheSea.Audio;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    /// <summary>
    /// Bridges device-independent fishing cue requests to the project-wide audio hub.
    /// The presenter owns cue timing; this integration boundary owns playback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingV3AudioHubAdapter : MonoBehaviour
    {
        [SerializeField] private FishingV3AudioPresenter presenter;

        private AudioHub _audioHub;
        private bool _subscribed;

        public bool HasPresenter => presenter != null;
        public int PlaybackSequence { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(FishingV3AudioPresenter sourcePresenter)
        {
            Unsubscribe();
            presenter = sourcePresenter;
            ResolveReferences();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void ResolveReferences()
        {
            if (presenter == null)
            {
                presenter = GetComponent<FishingV3AudioPresenter>();
            }

            if (_audioHub == null)
            {
                _audioHub = AudioHub.Instance;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || presenter == null)
            {
                return;
            }

            presenter.CueRequested += HandleCueRequested;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || presenter == null)
            {
                _subscribed = false;
                return;
            }

            presenter.CueRequested -= HandleCueRequested;
            _subscribed = false;
        }

        private void HandleCueRequested(FishingV3AudioCue cue)
        {
            AudioClip clip = presenter != null
                ? presenter.GetConfiguredClip(cue)
                : null;
            if (clip == null)
            {
                return;
            }

            if (_audioHub == null)
            {
                _audioHub = AudioHub.Instance;
            }

            if (_audioHub == null || !_audioHub.CanHear)
            {
                return;
            }

            _audioHub.PlayOneShot(clip, presenter.GetConfiguredVolume(cue));
            PlaybackSequence++;
        }
    }
}

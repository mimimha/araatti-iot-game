using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    public sealed class KeyboardFishingInputSource : IFishingInputSource
    {
        private const float TensionAdjustPerSecond = 0.48f;
        private readonly string _participantId;
        private readonly RodPoseAccumulator _rodPose = new RodPoseAccumulator();
        private long _sequence;
        private float _mockTension = 0.5f;

        public KeyboardFishingInputSource(string participantId)
        {
            _participantId = string.IsNullOrWhiteSpace(participantId) ? "local-player" : participantId;
        }

        public bool IsConnected => true;
        public float MockTension => _mockTension;
        public float RodPitch => _rodPose.Pitch;
        public float RodYaw => _rodPose.Yaw;
        public float MotionStrength => _rodPose.MotionStrength;

        public FishingInputFrame ReadFrame()
        {
            float tensionDirection = 0f;
            if (Input.GetKey(KeyCode.E)) tensionDirection += 1f;
            if (Input.GetKey(KeyCode.Q)) tensionDirection -= 1f;
            _mockTension = Mathf.Clamp01(_mockTension + (tensionDirection * TensionAdjustPerSecond * Time.unscaledDeltaTime));

            float pitchDirection = 0f;
            if (Input.GetKey(KeyCode.UpArrow)) pitchDirection += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) pitchDirection -= 1f;

            float yawDirection = 0f;
            if (Input.GetKey(KeyCode.RightArrow)) yawDirection += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) yawDirection -= 1f;

            if (Input.GetKeyDown(KeyCode.T)) _rodPose.Recenter();
            else _rodPose.Step(pitchDirection, yawDirection, Time.unscaledDeltaTime);

            bool spaceDown = Input.GetKeyDown(KeyCode.Space);
            bool fishingActionDown = Input.GetKeyDown(KeyCode.J);
            return new FishingInputFrame
            {
                ParticipantId = _participantId,
                Sequence = ++_sequence,
                TimestampSeconds = Time.unscaledTimeAsDouble,
                CastPressed = spaceDown,
                CastReleased = Input.GetKeyUp(KeyCode.Space),
                // The physical J key is intentionally shared, while the
                // gameplay meanings remain separate for future device bindings.
                HookPressed = fishingActionDown,
                TimingPressed = fishingActionDown,
                ReelDelta = Input.GetKey(KeyCode.R) || Input.GetMouseButton(0) ? 1f : 0f,
                TensionNormalized = _mockTension,
                RodPitch = _rodPose.Pitch,
                RodYaw = _rodPose.Yaw,
                MotionStrength = _rodPose.MotionStrength,
                IsDeviceConnected = true
            };
        }

        public void ResetState()
        {
            _sequence = 0;
            _mockTension = 0.5f;
            _rodPose.Reset();
        }
    }
}

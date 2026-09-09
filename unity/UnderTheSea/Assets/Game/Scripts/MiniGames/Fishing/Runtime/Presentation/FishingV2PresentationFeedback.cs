using System;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [Serializable]
    public sealed class FishingV2PresentationTuning
    {
        public float NibblePulseSeconds = 0.24f;
        public float BitePulseSeconds = 0.34f;
        public float HeadShakePulseSeconds = 0.32f;
        public float CameraImpulseSeconds = 0.26f;

        public float NibbleRodKick = 0.10f;
        public float BiteRodKick = 0.34f;
        public float HookRodKick = 0.28f;
        public float RestBendScale = 0.14f;
        public float FightBendScale = 0.38f;
        public float RunBendScale = 0.66f;
        public float TelegraphPullScale = 0.24f;
        public float HeadShakeFrequency = 16f;
        public float HeadShakeAmplitude = 0.18f;
        public float FinalRunVisualMultiplier = 1.16f;
        public float RodLoadCap = 0.96f;

        public float LineSlackSag = 0.72f;
        public float LineTautSag = 0.035f;
        public float PreFightWaitingSag = 0.56f;
        public float PreFightNibbleSag = 0.38f;
        public float PreFightBiteSag = 0.075f;
        public float PreFightHookedSag = 0.055f;

        public float CameraPositionCap = 0.03f;
        public float CameraRotationCapDegrees = 1f;
        public float CameraKickScale = 1f;

        public FishingV2PresentationTuning Copy()
        {
            return (FishingV2PresentationTuning)MemberwiseClone();
        }

        public void Sanitize()
        {
            NibblePulseSeconds = Mathf.Max(0.01f, NibblePulseSeconds);
            BitePulseSeconds = Mathf.Max(0.01f, BitePulseSeconds);
            HeadShakePulseSeconds = Mathf.Max(0.01f, HeadShakePulseSeconds);
            CameraImpulseSeconds = Mathf.Max(0.01f, CameraImpulseSeconds);
            NibbleRodKick = ClampFinite01(NibbleRodKick);
            BiteRodKick = ClampFinite01(BiteRodKick);
            HookRodKick = ClampFinite01(HookRodKick);
            RestBendScale = ClampFinite01(RestBendScale);
            FightBendScale = Mathf.Clamp(ClampFinite01(FightBendScale), RestBendScale, 1f);
            RunBendScale = Mathf.Clamp(ClampFinite01(RunBendScale), FightBendScale, 1f);
            TelegraphPullScale = ClampFinite01(TelegraphPullScale);
            HeadShakeFrequency = ClampFinite(HeadShakeFrequency, 0f, 40f);
            HeadShakeAmplitude = ClampFinite01(HeadShakeAmplitude);
            FinalRunVisualMultiplier = ClampFinite(FinalRunVisualMultiplier, 1f, 2f);
            RodLoadCap = Mathf.Max(0.01f, ClampFinite01(RodLoadCap));
            LineSlackSag = ClampFinite(LineSlackSag, 0f, 2f);
            LineTautSag = ClampFinite(LineTautSag, 0f, LineSlackSag);
            PreFightWaitingSag = ClampFinite(PreFightWaitingSag, 0f, 2f);
            PreFightNibbleSag = ClampFinite(PreFightNibbleSag, 0f, PreFightWaitingSag);
            PreFightBiteSag = ClampFinite(PreFightBiteSag, 0f, PreFightNibbleSag);
            PreFightHookedSag = ClampFinite(PreFightHookedSag, 0f, PreFightNibbleSag);
            CameraPositionCap = ClampFinite(CameraPositionCap, 0f, 0.03f);
            CameraRotationCapDegrees = ClampFinite(CameraRotationCapDegrees, 0f, 1f);
            CameraKickScale = ClampFinite(CameraKickScale, 0f, 2f);
        }

        private static float ClampFinite01(float value)
        {
            return ClampFinite(value, 0f, 1f);
        }

        private static float ClampFinite(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? min : Mathf.Clamp(value, min, max);
        }
    }

    public struct FishingV2PresentationInput
    {
        public FishingPlayerState PlayerState;
        public float StateElapsedSeconds;
        public bool IsNibbling;
        public float NibbleIntensityNormalized;
        public int NibbleEventSequence;
        public int BiteEventSequence;
        public FishingV2BehaviorState BehaviorState;
        public float FishForceNormalized;
        public float FishDirectionNormalized;
        public float VirtualLineTensionNormalized;
        public FishingV2TensionZone VirtualTensionZone;
        public bool IsRunTelegraphing;
        public float RunTelegraphDirectionNormalized;
        public bool HeadShakeActive;
        public float HeadShakeIntensityNormalized;
        public int HeadShakeEventSequence;
        public bool FinalRunPending;
        public bool IsFinalRun;

        public static FishingV2PresentationInput FromSnapshot(FishingSnapshot snapshot)
        {
            if (snapshot == null) return default;
            return new FishingV2PresentationInput
            {
                PlayerState = snapshot.State,
                StateElapsedSeconds = snapshot.StateElapsedSeconds,
                IsNibbling = snapshot.IsNibbling,
                NibbleIntensityNormalized = snapshot.NibbleIntensityNormalized,
                NibbleEventSequence = snapshot.NibbleEventSequence,
                BiteEventSequence = snapshot.BiteEventSequence,
                BehaviorState = snapshot.V2BehaviorState,
                FishForceNormalized = snapshot.V2FishForceNormalized,
                FishDirectionNormalized = snapshot.V2FishDirectionNormalized,
                VirtualLineTensionNormalized = snapshot.VirtualLineTensionNormalized,
                VirtualTensionZone = snapshot.VirtualTensionZone,
                IsRunTelegraphing = snapshot.IsRunTelegraphing,
                RunTelegraphDirectionNormalized = snapshot.RunTelegraphDirectionNormalized,
                HeadShakeActive = snapshot.HeadShakeActive,
                HeadShakeIntensityNormalized = snapshot.HeadShakeIntensityNormalized,
                HeadShakeEventSequence = snapshot.HeadShakeEventSequence,
                FinalRunPending = snapshot.FinalRunPending,
                IsFinalRun = snapshot.IsFinalRun
            };
        }
    }

    public struct FishingV2VisualFrame
    {
        public bool NibbleStarted;
        public bool BiteStarted;
        public bool HeadShakeStarted;
        public bool FinalRunStarted;
        public float NibblePulseNormalized;
        public float BitePulseNormalized;
        public float HeadShakePulseNormalized;
        public float RodLoadNormalized;
        public float RodKickNormalized;
        public float LateralPullNormalized;
        public float SurfaceDisturbanceNormalized;
        public float FishMotionNormalized;
        public float LineSagMeters;
        public float LineJitterNormalized;
        public float BobberDipMeters;
        public float AnimationPhaseSeconds;
        public Vector3 CameraPositionOffset;
        public Vector3 CameraRotationEuler;
    }

    /// <summary>
    /// Stateful, presentation-only interpretation of public FishingSnapshot signals.
    /// It never writes to gameplay state and owns all pause-sensitive visual phases.
    /// </summary>
    public sealed class FishingV2PresentationFeedback
    {
        private readonly FishingV2PresentationTuning _tuning;
        private int _lastNibbleSequence;
        private int _lastBiteSequence;
        private int _lastHeadShakeSequence;
        private float _nibblePulseRemaining;
        private float _bitePulseRemaining;
        private float _headShakePulseRemaining;
        private float _cameraImpulseRemaining;
        private float _cameraImpulseStrength;
        private float _cameraImpulseDirection = 1f;
        private float _animationPhase;
        private bool _wasFinalRun;

        public FishingV2PresentationFeedback(FishingV2PresentationTuning tuning = null)
        {
            _tuning = (tuning ?? new FishingV2PresentationTuning()).Copy();
            _tuning.Sanitize();
        }

        public FishingV2VisualFrame Tick(FishingV2PresentationInput input, float deltaTime, bool paused)
        {
            SanitizeInput(ref input);
            bool nibbleStarted = ConsumeSequence(ref _lastNibbleSequence, input.NibbleEventSequence);
            bool biteStarted = ConsumeSequence(ref _lastBiteSequence, input.BiteEventSequence);
            bool headShakeStarted = ConsumeSequence(ref _lastHeadShakeSequence, input.HeadShakeEventSequence);
            bool finalRunStarted = input.IsFinalRun && !_wasFinalRun;
            _wasFinalRun = input.IsFinalRun;

            if (nibbleStarted) _nibblePulseRemaining = _tuning.NibblePulseSeconds;
            if (biteStarted)
            {
                _bitePulseRemaining = _tuning.BitePulseSeconds;
                StartCameraImpulse(1f, 1f);
            }
            if (headShakeStarted)
            {
                _headShakePulseRemaining = _tuning.HeadShakePulseSeconds;
                StartCameraImpulse(0.48f + input.HeadShakeIntensityNormalized * 0.34f,
                    NonZeroDirection(input.FishDirectionNormalized));
            }
            if (finalRunStarted)
            {
                StartCameraImpulse(0.62f, NonZeroDirection(input.FishDirectionNormalized));
            }

            float nibblePulse = Pulse(_nibblePulseRemaining, _tuning.NibblePulseSeconds);
            float bitePulse = Pulse(_bitePulseRemaining, _tuning.BitePulseSeconds);
            float headShakePulse = Pulse(_headShakePulseRemaining, _tuning.HeadShakePulseSeconds);
            float headShakeWave = input.HeadShakeActive
                ? Mathf.Sin(_animationPhase * _tuning.HeadShakeFrequency * Mathf.PI * 2f) *
                  input.HeadShakeIntensityNormalized
                : 0f;

            bool fighting = input.PlayerState == FishingPlayerState.Fighting;
            float behaviorAccent = 0f;
            float behaviorLateralScale = 0f;
            float surface = 0f;
            float fishMotion = 0f;
            if (fighting)
            {
                switch (input.BehaviorState)
                {
                    case FishingV2BehaviorState.Rest:
                        behaviorAccent = _tuning.RestBendScale;
                        behaviorLateralScale = 0.12f;
                        surface = 0.14f;
                        fishMotion = 0.22f;
                        break;
                    case FishingV2BehaviorState.Run:
                        behaviorAccent = _tuning.RunBendScale;
                        behaviorLateralScale = 0.86f;
                        surface = 0.78f;
                        fishMotion = 0.92f;
                        break;
                    default:
                        behaviorAccent = _tuning.FightBendScale;
                        behaviorLateralScale = 0.44f;
                        surface = 0.38f;
                        fishMotion = 0.52f;
                        break;
                }
            }

            float tensionLoad = fighting ? input.VirtualLineTensionNormalized : 0f;
            float rodLoad = fighting
                ? tensionLoad * 0.40f + behaviorAccent * 0.60f
                : 0f;
            float lateralPull = fighting
                ? input.FishDirectionNormalized * input.FishForceNormalized * behaviorLateralScale
                : 0f;

            if (input.IsRunTelegraphing)
            {
                lateralPull += input.RunTelegraphDirectionNormalized * _tuning.TelegraphPullScale;
                surface += 0.20f;
                rodLoad += 0.06f;
            }

            float finalMultiplier = input.IsFinalRun ? _tuning.FinalRunVisualMultiplier : 1f;
            if (input.IsFinalRun)
            {
                rodLoad *= finalMultiplier;
                lateralPull *= finalMultiplier;
                surface *= finalMultiplier;
                fishMotion *= finalMultiplier;
            }
            else if (input.FinalRunPending && input.IsRunTelegraphing)
            {
                surface += 0.08f;
                rodLoad += 0.04f;
            }

            float nibbleSustain = input.IsNibbling ? input.NibbleIntensityNormalized : 0f;
            float rodKick = nibblePulse * _tuning.NibbleRodKick +
                bitePulse * _tuning.BiteRodKick +
                headShakePulse * _tuning.HeadShakeAmplitude;
            if (input.PlayerState == FishingPlayerState.Hooked)
            {
                rodKick += _tuning.HookRodKick * (1f - Mathf.Clamp01(input.StateElapsedSeconds / 0.35f));
                rodLoad = Mathf.Max(rodLoad, 0.58f);
            }

            surface += nibbleSustain * 0.15f + nibblePulse * 0.18f + bitePulse * 0.62f +
                headShakePulse * 0.18f + Mathf.Abs(headShakeWave) * 0.25f;
            fishMotion += headShakePulse * 0.20f + Mathf.Abs(headShakeWave) * 0.35f;
            float lineJitter = headShakeWave * _tuning.HeadShakeAmplitude +
                headShakePulse * 0.12f + nibblePulse * 0.10f + bitePulse * 0.24f;

            float sag = CalculateLineSag(input, fighting, nibblePulse, bitePulse);
            float bobberDip = nibbleSustain * 0.055f + nibblePulse * 0.075f + bitePulse * 0.25f;
            CalculateCamera(out Vector3 cameraPosition, out Vector3 cameraRotation);

            FishingV2VisualFrame result = new FishingV2VisualFrame
            {
                NibbleStarted = nibbleStarted,
                BiteStarted = biteStarted,
                HeadShakeStarted = headShakeStarted,
                FinalRunStarted = finalRunStarted,
                NibblePulseNormalized = ClampFinite01(nibblePulse),
                BitePulseNormalized = ClampFinite01(bitePulse),
                HeadShakePulseNormalized = ClampFinite01(headShakePulse),
                RodLoadNormalized = Mathf.Clamp(ClampFinite01(rodLoad), 0f, _tuning.RodLoadCap),
                RodKickNormalized = ClampFinite01(rodKick),
                LateralPullNormalized = ClampFinite(lateralPull, -1f, 1f),
                SurfaceDisturbanceNormalized = ClampFinite01(surface),
                FishMotionNormalized = ClampFinite01(fishMotion),
                LineSagMeters = ClampFinite(sag, 0f, 2f),
                LineJitterNormalized = ClampFinite(lineJitter, -1f, 1f),
                BobberDipMeters = ClampFinite(bobberDip, 0f, 0.5f),
                AnimationPhaseSeconds = ClampFinite(_animationPhase, 0f, float.MaxValue),
                CameraPositionOffset = SanitizeVector(cameraPosition),
                CameraRotationEuler = SanitizeVector(cameraRotation)
            };

            if (!paused)
            {
                float dt = ClampFinite(deltaTime, 0f, 0.1f);
                _animationPhase += dt;
                _nibblePulseRemaining = Mathf.Max(0f, _nibblePulseRemaining - dt);
                _bitePulseRemaining = Mathf.Max(0f, _bitePulseRemaining - dt);
                _headShakePulseRemaining = Mathf.Max(0f, _headShakePulseRemaining - dt);
                _cameraImpulseRemaining = Mathf.Max(0f, _cameraImpulseRemaining - dt);
            }

            return result;
        }

        private float CalculateLineSag(
            FishingV2PresentationInput input,
            bool fighting,
            float nibblePulse,
            float bitePulse)
        {
            if (fighting)
            {
                return Mathf.Lerp(_tuning.LineSlackSag, _tuning.LineTautSag,
                    input.VirtualLineTensionNormalized);
            }

            switch (input.PlayerState)
            {
                case FishingPlayerState.Waiting:
                    return input.IsNibbling || nibblePulse > 0f
                        ? _tuning.PreFightNibbleSag
                        : _tuning.PreFightWaitingSag;
                case FishingPlayerState.BiteWindow:
                    return Mathf.Lerp(_tuning.PreFightNibbleSag, _tuning.PreFightBiteSag,
                        Mathf.Max(0.75f, bitePulse));
                case FishingPlayerState.Hooked:
                    return _tuning.PreFightHookedSag;
                case FishingPlayerState.Casting:
                    return _tuning.PreFightBiteSag;
                default:
                    return 0f;
            }
        }

        private void StartCameraImpulse(float strength, float direction)
        {
            strength = ClampFinite01(strength) * _tuning.CameraKickScale;
            if (strength < _cameraImpulseStrength * Pulse(_cameraImpulseRemaining, _tuning.CameraImpulseSeconds)) return;
            _cameraImpulseRemaining = _tuning.CameraImpulseSeconds;
            _cameraImpulseStrength = strength;
            _cameraImpulseDirection = NonZeroDirection(direction);
        }

        private void CalculateCamera(out Vector3 position, out Vector3 rotation)
        {
            float envelope = Pulse(_cameraImpulseRemaining, _tuning.CameraImpulseSeconds);
            float strength = _cameraImpulseStrength * envelope;
            position = new Vector3(
                _cameraImpulseDirection * _tuning.CameraPositionCap * 0.35f,
                -_tuning.CameraPositionCap * 0.18f,
                -_tuning.CameraPositionCap * 0.82f) * strength;
            rotation = new Vector3(
                -_tuning.CameraRotationCapDegrees * 0.45f,
                _cameraImpulseDirection * _tuning.CameraRotationCapDegrees,
                _cameraImpulseDirection * _tuning.CameraRotationCapDegrees * 0.30f) * strength;
        }

        private static bool ConsumeSequence(ref int last, int current)
        {
            current = Mathf.Max(0, current);
            if (current < last)
            {
                last = current;
                return false;
            }
            if (current == last) return false;
            last = current;
            return true;
        }

        private static float Pulse(float remaining, float duration)
        {
            return duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
        }

        private static float NonZeroDirection(float value)
        {
            return value < 0f ? -1f : 1f;
        }

        private static void SanitizeInput(ref FishingV2PresentationInput input)
        {
            input.StateElapsedSeconds = ClampFinite(input.StateElapsedSeconds, 0f, float.MaxValue);
            input.NibbleIntensityNormalized = ClampFinite01(input.NibbleIntensityNormalized);
            input.FishForceNormalized = ClampFinite01(input.FishForceNormalized);
            input.FishDirectionNormalized = ClampFinite(input.FishDirectionNormalized, -1f, 1f);
            input.VirtualLineTensionNormalized = ClampFinite01(input.VirtualLineTensionNormalized);
            input.RunTelegraphDirectionNormalized = ClampFinite(input.RunTelegraphDirectionNormalized, -1f, 1f);
            input.HeadShakeIntensityNormalized = ClampFinite01(input.HeadShakeIntensityNormalized);
        }

        private static float ClampFinite01(float value)
        {
            return ClampFinite(value, 0f, 1f);
        }

        private static float ClampFinite(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? min : Mathf.Clamp(value, min, max);
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            value.x = ClampFinite(value.x, -1000f, 1000f);
            value.y = ClampFinite(value.y, -1000f, 1000f);
            value.z = ClampFinite(value.z, -1000f, 1000f);
            return value;
        }
    }
}

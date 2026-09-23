using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3ReelInputAdapterTests
    {
        [Test]
        public void ZeroNormalizedInput_ProducesZeroCanonicalDelta()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            FishingV3ReelInput input = adapter.ConvertNormalizedInput(0f, 0.1f);

            Assert.That(input.ReelDeltaRevolutions, Is.Zero);
        }

        [Test]
        public void PositiveNormalizedInput_ProducesPositiveCanonicalDelta()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            FishingV3ReelInput input = adapter.ConvertNormalizedInput(0.5f, 0.1f);

            Assert.That(input.ReelDeltaRevolutions, Is.GreaterThan(0f));
        }

        [Test]
        public void DoubleDeltaTime_DoublesCanonicalDelta()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            float shortDelta = adapter.ConvertNormalizedInput(0.75f, 0.1f)
                .ReelDeltaRevolutions;
            float longDelta = adapter.ConvertNormalizedInput(0.75f, 0.2f)
                .ReelDeltaRevolutions;

            Assert.That(longDelta, Is.EqualTo(shortDelta * 2f).Within(0.000001f));
        }

        [Test]
        public void SameInputDuration_IsFrameSliceIndependent()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            float whole = adapter.ConvertNormalizedInput(0.8f, 1f).ReelDeltaRevolutions;
            float sliced = 0f;
            for (int i = 0; i < 10; i++)
            {
                sliced += adapter.ConvertNormalizedInput(0.8f, 0.1f)
                    .ReelDeltaRevolutions;
            }

            Assert.That(sliced, Is.EqualTo(whole).Within(0.000001f));
        }

        [Test]
        public void LegacyFullInput_IsNotOneRevolutionPerTick()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            float delta = adapter.ConvertNormalizedInput(1f, 0.02f).ReelDeltaRevolutions;

            Assert.That(delta, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(delta, Is.Not.EqualTo(1f));
        }

        [Test]
        public void Conversion_UsesReelRevolutions()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(2f);

            float delta = adapter.ConvertNormalizedInput(0.25f, 0.5f)
                .ReelDeltaRevolutions;

            Assert.That(delta, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void NegativeNormalizedInput_DoesNotProducePositiveDelta()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            FishingV3ReelInput input = adapter.ConvertNormalizedInput(-1f, 0.1f);

            Assert.That(input.ReelDeltaRevolutions, Is.Zero);
        }

        [Test]
        public void MockFrame_UsesSameLegacyConversionWithoutChangingItsValue()
        {
            MockFishingInputSource mock = new MockFishingInputSource();
            mock.SetNextFrame(new FishingInputFrame { ReelDelta = 0.5f });
            FishingV3ReelInputAdapter adapter = CreateAdapter(2f);

            FishingInputFrame legacyFrame = mock.ReadFrame();
            FishingV3ReelInput input = adapter.ConvertLegacyFrame(legacyFrame, 0.25f);

            Assert.That(legacyFrame.ReelDelta, Is.EqualTo(0.5f));
            Assert.That(input.ReelDeltaRevolutions, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void CanonicalDelta_CanDriveFishingV3Model()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);
            FishingV3Model model = CreateStableSafeModel();

            FishingV3ReelInput input = adapter.ConvertNormalizedInput(1f, 0.2f);
            model.Tick(FishingV3FishState.Fight, input.ReelDeltaRevolutions, 0.2f);

            Assert.That(input.ReelDeltaRevolutions, Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(model.CaptureProgressNormalized, Is.EqualTo(0.1f).Within(0.000001f));
        }

        [Test]
        public void AdapterAndCore_SameDurationAcrossSlicing_ProduceSameCapture()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);
            FishingV3Model whole = CreateStableSafeModel();
            FishingV3Model sliced = CreateStableSafeModel();

            FishingV3ReelInput wholeInput = adapter.ConvertNormalizedInput(0.8f, 1f);
            whole.Tick(FishingV3FishState.Fight, wholeInput.ReelDeltaRevolutions, 1f);

            for (int i = 0; i < 10; i++)
            {
                FishingV3ReelInput frameInput = adapter.ConvertNormalizedInput(0.8f, 0.1f);
                sliced.Tick(FishingV3FishState.Fight, frameInput.ReelDeltaRevolutions, 0.1f);
            }

            Assert.That(sliced.CaptureProgressNormalized,
                Is.EqualTo(whole.CaptureProgressNormalized).Within(0.000001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0f)]
        [TestCase(-0.1f)]
        public void InvalidDeltaTime_ProducesSafeZero(float deltaTime)
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            FishingV3ReelInput input = adapter.ConvertNormalizedInput(1f, deltaTime);

            Assert.That(input.ReelDeltaRevolutions, Is.Zero);
        }

        [Test]
        public void NormalizedInputAboveOne_IsClamped()
        {
            FishingV3ReelInputAdapter adapter = CreateAdapter(0.5f);

            float fullInput = adapter.ConvertNormalizedInput(1f, 0.1f)
                .ReelDeltaRevolutions;
            float excessiveInput = adapter.ConvertNormalizedInput(10f, 0.1f)
                .ReelDeltaRevolutions;

            Assert.That(excessiveInput, Is.EqualTo(fullInput).Within(0.000001f));
        }

        private static FishingV3ReelInputAdapter CreateAdapter(float revolutionsPerSecond)
        {
            return new FishingV3ReelInputAdapter(new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = revolutionsPerSecond
            });
        }

        private static FishingV3Model CreateStableSafeModel()
        {
            return new FishingV3Model(new FishingV3Tuning
            {
                InitialTensionNormalized = 0.5f,
                FightBaseTension = 0.5f,
                ReelTensionGain = 0f,
                CaptureScale = 1f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            });
        }
    }
}

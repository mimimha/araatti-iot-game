using System;
using System.Collections.Generic;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingModeControllerTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in _objects)
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }

            _objects.Clear();
        }

        [Test]
        public void ValidSpotRequest_StartsV3AndStoresContextWithoutTickingGameplay()
        {
            Fixture fixture = CreateFixture();
            var input = new CountingInputSource(Frame(1f));
            fixture.Controller.SetInputSource(input);

            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));
            Assert.That(fixture.Mode.CurrentSpot, Is.SameAs(fixture.Spot));
            Assert.That(fixture.Mode.CurrentInteractor, Is.SameAs(fixture.Interactor));
            Assert.That(fixture.Facade.GameplayRuntimeMode,
                Is.EqualTo(FishingGameplayRuntimeMode.V3));
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Running));
            Assert.That(fixture.Facade.V3Current.FishState,
                Is.EqualTo(FishingV3FishState.Fight));
            Assert.That(fixture.Facade.V3Current.ReelControlMode,
                Is.EqualTo(FishingV3ReelControlMode.Timing));
            Assert.That(fixture.Facade.V3Current.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.WaitingForBite));
            Assert.That(fixture.Facade.V3Current.IsTimingReelActive, Is.False);
            Assert.That(fixture.Facade.V3Current.CaptureProgressNormalized, Is.Zero);
            Assert.That(input.ReadCount, Is.Zero);
        }

        [TestCase(
            FishingV3FishProfileSelectionMode.ForceSmall,
            FishingV3FishProfileId.Small)]
        [TestCase(
            FishingV3FishProfileSelectionMode.ForceNormal,
            FishingV3FishProfileId.Normal)]
        [TestCase(
            FishingV3FishProfileSelectionMode.ForceStrong,
            FishingV3FishProfileId.Strong)]
        public void ForcedFishProfile_IsAppliedWhenSpotStartsSession(
            FishingV3FishProfileSelectionMode mode,
            FishingV3FishProfileId expected)
        {
            Fixture fixture = CreateFixture();
            fixture.Mode.ConfigureFishProfileSelection(mode, 731);

            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);

            Assert.That(fixture.Mode.LastSelectedFishProfile.Id, Is.EqualTo(expected));
            Assert.That(fixture.Facade.V3Current.FishProfileId, Is.EqualTo(expected));
            Assert.That(fixture.Facade.V3FishProfile.Id, Is.EqualTo(expected));
        }

        [Test]
        public void BindingSameSpotRepeatedly_CreatesOnlyOneSubscription()
        {
            Fixture fixture = CreateFixture();

            fixture.Mode.Bind(fixture.Spot);
            fixture.Mode.Bind(fixture.Spot);

            Assert.That(SubscriptionCount(fixture.Spot), Is.EqualTo(1));
        }

        [Test]
        public void BindingAnotherSpot_RemovesPreviousSubscription()
        {
            Fixture fixture = CreateFixture();
            FishingSpot second = CreateObject("SecondSpot").AddComponent<FishingSpot>();

            fixture.Mode.Bind(second);

            Assert.That(SubscriptionCount(fixture.Spot), Is.Zero);
            Assert.That(SubscriptionCount(second), Is.EqualTo(1));
        }

        [Test]
        public void MissingFacade_ReleasesSpotAndLeavesNoStaleContext()
        {
            GameObject host = CreateObject("ModeWithoutFacade");
            FishingModeController mode = host.AddComponent<FishingModeController>();
            FishingSpot spot = CreateObject("FishingSpot").AddComponent<FishingSpot>();
            GameObject interactor = CreateObject("Interactor");
            mode.Bind(spot);

            Assert.That(spot.TryInteract(interactor), Is.True);

            Assert.That(mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(mode.CurrentSpot, Is.Null);
            Assert.That(mode.CurrentInteractor, Is.Null);
            Assert.That(spot.IsBusy, Is.False);
        }

        [Test]
        public void ActiveSession_RejectsAnotherSpotWithoutReleasingCurrentSpot()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);
            FishingV3Snapshot originalSession = fixture.Facade.V3Current;
            FishingSpot second = CreateObject("SecondSpot").AddComponent<FishingSpot>();
            fixture.Mode.Bind(second);

            Assert.That(second.TryInteract(CreateObject("SecondInteractor")), Is.True);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));
            Assert.That(fixture.Mode.CurrentSpot, Is.SameAs(fixture.Spot));
            Assert.That(fixture.Spot.IsBusy, Is.True);
            Assert.That(second.IsBusy, Is.False);
            Assert.That(fixture.Facade.V3Current, Is.SameAs(originalSession));
        }

        [Test]
        public void PausedSession_RejectsAnotherSpotWithoutStartingNewSession()
        {
            Fixture fixture = CreateFixture();
            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);
            Assert.That(fixture.Mode.RequestPause(), Is.True);
            FishingV3Snapshot pausedSession = fixture.Facade.V3Current;
            FishingSpot second = CreateObject("SecondSpot").AddComponent<FishingSpot>();
            fixture.Mode.Bind(second);

            Assert.That(second.TryInteract(CreateObject("SecondInteractor")), Is.True);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Paused));
            Assert.That(fixture.Mode.CurrentSpot, Is.SameAs(fixture.Spot));
            Assert.That(fixture.Spot.IsBusy, Is.True);
            Assert.That(second.IsBusy, Is.False);
            Assert.That(fixture.Facade.V3Current, Is.SameAs(pausedSession));
        }

        [Test]
        public void PauseAndResume_UseFacadeLifecycleAndPreserveContext()
        {
            Fixture fixture = CreateFixture();
            fixture.Spot.TryInteract(fixture.Interactor);

            Assert.That(fixture.Mode.RequestPause(), Is.True);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Paused));
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Paused));
            Assert.That(fixture.Mode.CurrentSpot, Is.SameAs(fixture.Spot));
            Assert.That(fixture.Mode.CurrentInteractor, Is.SameAs(fixture.Interactor));
            Assert.That(fixture.Mode.RequestPause(), Is.False);

            Assert.That(fixture.Mode.RequestResume(), Is.True);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Running));
            Assert.That(fixture.Mode.RequestResume(), Is.False);
        }

        [Test]
        public void InactivePauseResumeAndAbort_DoNotCreateASession()
        {
            Fixture fixture = CreateFixture();

            Assert.That(fixture.Mode.RequestPause(), Is.False);
            Assert.That(fixture.Mode.RequestResume(), Is.False);
            Assert.That(fixture.Mode.Abort(), Is.False);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Facade.GameplayRuntimeMode,
                Is.EqualTo(FishingGameplayRuntimeMode.LegacyV2));
        }

        [TestCase(FishingV3Result.Caught)]
        [TestCase(FishingV3Result.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped)]
        public void TerminalResult_IsStoredBeforeContextRelease(FishingV3Result result)
        {
            Fixture fixture = CreateFixture();
            fixture.Spot.TryInteract(fixture.Interactor);

            ConfigureTerminal(fixture, result);
            Observe(fixture.Mode);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Mode.HasLastResult, Is.True);
            Assert.That(fixture.Mode.LastResult, Is.EqualTo(result));
            Assert.That(fixture.Mode.CurrentSpot, Is.Null);
            Assert.That(fixture.Mode.CurrentInteractor, Is.Null);
            Assert.That(fixture.Spot.IsBusy, Is.False);
            Assert.That(fixture.Facade.V3Current.Result, Is.EqualTo(result));
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Completed));
        }

        [Test]
        public void TerminalObservation_IsIdempotent()
        {
            Fixture fixture = CreateFixture();
            fixture.Spot.TryInteract(fixture.Interactor);
            ConfigureTerminal(fixture, FishingV3Result.Caught);

            Observe(fixture.Mode);
            Observe(fixture.Mode);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Mode.LastResult, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(fixture.Spot.IsBusy, Is.False);
        }

        [Test]
        public void Abort_UsesOfficialLifecycleReleasesOnceAndDoesNotInventResult()
        {
            Fixture fixture = CreateFixture();
            fixture.Spot.TryInteract(fixture.Interactor);

            Assert.That(fixture.Mode.Abort(), Is.True);
            Assert.That(fixture.Mode.Abort(), Is.False);

            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Aborted));
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Mode.HasLastResult, Is.False);
            Assert.That(fixture.Mode.LastResult, Is.EqualTo(FishingV3Result.Active));
            Assert.That(fixture.Spot.IsBusy, Is.False);
        }

        [Test]
        public void StartingNewSession_ClearsPreviousTerminalResult()
        {
            Fixture fixture = CreateFixture();
            fixture.Spot.TryInteract(fixture.Interactor);
            ConfigureTerminal(fixture, FishingV3Result.Caught);
            Observe(fixture.Mode);
            Assert.That(fixture.Mode.HasLastResult, Is.True);

            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));
            Assert.That(fixture.Mode.HasLastResult, Is.False);
            Assert.That(fixture.Mode.LastResult, Is.EqualTo(FishingV3Result.Active));
        }

        [Test]
        public void SourceOwnsOnlyLifecycleObservationAndNoInputPlayerHudOrResistance()
        {
            const string sourcePath =
                "Assets/Game/Scripts/Fishing/Runtime/Integration/FishingModeController.cs";
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(sourcePath);
            Assert.That(script, Is.Not.Null);

            string[] forbiddenTokens =
            {
                "Input.GetKey",
                "Keyboard.current",
                "InputAction",
                "ReadFrame(",
                "TickRuntime(",
                "FishingV3Model",
                "PlayerMovement",
                "fillAmount",
                "FishingV3HudPresenter",
                "IFishingResistanceOutput",
                "Time.timeScale",
                "OnTriggerEnter",
                "OnTriggerExit"
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(script.text, Does.Not.Contain(token), token);
            }
        }

        private Fixture CreateFixture()
        {
            GameObject host = CreateObject("FishingModeHost");
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            var serializedFacade = new SerializedObject(facade);
            serializedFacade.FindProperty("controller").objectReferenceValue = controller;
            serializedFacade.ApplyModifiedPropertiesWithoutUndo();
            FishingModeController mode = host.AddComponent<FishingModeController>();
            FishingSpot spot = CreateObject("FishingSpot").AddComponent<FishingSpot>();
            GameObject interactor = CreateObject("Interactor");
            mode.Bind(spot);
            return new Fixture(controller, facade, mode, spot, interactor);
        }

        private void ConfigureTerminal(Fixture fixture, FishingV3Result result)
        {
            float tension = result == FishingV3Result.LineBroken ? 0.95f :
                result == FishingV3Result.FishEscaped ? 0.05f : 0.5f;
            float reel = result == FishingV3Result.Caught ? 1f : 0f;
            var tuning = new FishingV3Tuning
            {
                InitialTensionNormalized = tension,
                CalmBaseTension = tension,
                FightBaseTension = tension,
                RunBaseTension = tension,
                ReelTensionGain = 0f,
                CaptureScale = result == FishingV3Result.Caught ? 2f : 0f,
                BreakStressPerSecond = result == FishingV3Result.LineBroken ? 2f : 0f,
                EscapeRiskPerSecond = result == FishingV3Result.FishEscaped ? 2f : 0f
            };
            fixture.Controller.SetInputSource(new CountingInputSource(Frame(reel)));
            fixture.Controller.ConfigureV3Runtime(tuning, new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = 1f
            });
            fixture.Controller.SetV3FishState(FishingV3FishState.Fight);
            fixture.Controller.BeginRound();
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(result));
        }

        private static int SubscriptionCount(FishingSpot spot)
        {
            FieldInfo field = typeof(FishingSpot).GetField(
                "FishingRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate handlers = field?.GetValue(spot) as Delegate;
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private static void Observe(FishingModeController mode)
        {
            MethodInfo update = typeof(FishingModeController).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(mode, null);
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            _objects.Add(instance);
            return instance;
        }

        private static FishingInputFrame Frame(float reel)
        {
            return new FishingInputFrame
            {
                ReelDelta = reel,
                IsDeviceConnected = true
            };
        }

        private readonly struct Fixture
        {
            public Fixture(
                FishingGameController controller,
                FishingMiniGameFacade facade,
                FishingModeController mode,
                FishingSpot spot,
                GameObject interactor)
            {
                Controller = controller;
                Facade = facade;
                Mode = mode;
                Spot = spot;
                Interactor = interactor;
            }

            public FishingGameController Controller { get; }
            public FishingMiniGameFacade Facade { get; }
            public FishingModeController Mode { get; }
            public FishingSpot Spot { get; }
            public GameObject Interactor { get; }
        }

        private sealed class CountingInputSource : IFishingInputSource
        {
            private readonly FishingInputFrame _frame;

            public CountingInputSource(FishingInputFrame frame)
            {
                _frame = frame;
            }

            public bool IsConnected => true;

            public int ReadCount { get; private set; }

            public FishingInputFrame ReadFrame()
            {
                ReadCount++;
                return _frame;
            }

            public void ResetState()
            {
            }
        }
    }
}

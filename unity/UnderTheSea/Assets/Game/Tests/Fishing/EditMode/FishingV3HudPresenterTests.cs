using System.Collections.Generic;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3HudPresenterTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in _objects)
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            _objects.Clear();
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void TensionGauge_ReadsNormalizedValueFromV3Snapshot(float tension)
        {
            Fixture fixture = CreateFixture(StableTuning(tension), 0f);

            fixture.Presenter.RefreshNow();

            Assert.That(fixture.TensionFill.fillAmount, Is.EqualTo(tension).Within(0.000001f));
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        public void CaptureGauge_ReadsProgressFromV3Snapshot(float reelDelta)
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), reelDelta);

            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.CaptureFill.fillAmount,
                Is.EqualTo(reelDelta).Within(0.000001f));
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
        }

        [Test]
        public void GaugeNormalization_ClampsAndRejectsNonFiniteValues()
        {
            Assert.That(FishingV3HudPresenter.NormalizeGaugeValue(-1f), Is.Zero);
            Assert.That(FishingV3HudPresenter.NormalizeGaugeValue(2f), Is.EqualTo(1f));
            Assert.That(FishingV3HudPresenter.NormalizeGaugeValue(float.NaN), Is.Zero);
            Assert.That(FishingV3HudPresenter.NormalizeGaugeValue(float.PositiveInfinity), Is.Zero);
        }

        [Test]
        public void Refresh_IsReadOnlyAndDoesNotSampleInputOrAdvanceGameplay()
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), 1f);
            FishingV3Snapshot before = fixture.Controller.V3Snapshot;
            int readsBefore = fixture.Input.ReadCount;

            fixture.Presenter.RefreshNow();
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Input.ReadCount, Is.EqualTo(readsBefore));
            Assert.That(fixture.Controller.V3Snapshot.TensionNormalized,
                Is.EqualTo(before.TensionNormalized));
            Assert.That(fixture.Controller.V3Snapshot.CaptureProgressNormalized,
                Is.EqualTo(before.CaptureProgressNormalized));
            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(before.Result));
        }

        [Test]
        public void Lifecycle_HidesBeforeBegin_HoldsOnPause_ResumesAndResetsOnAbort()
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), 0f, begin: false);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsHudVisible, Is.False);
            Assert.That(fixture.TensionFill.fillAmount, Is.Zero);

            fixture.Controller.BeginRound();
            fixture.Controller.SetV3FishState(FishingV3FishState.Fight);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
            float displayed = fixture.TensionFill.fillAmount;

            fixture.Controller.SetPaused(true);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
            Assert.That(fixture.TensionFill.fillAmount, Is.EqualTo(displayed));

            fixture.Controller.SetPaused(false);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);

            fixture.Controller.AbortRound();
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.IsHudVisible, Is.False);
            Assert.That(fixture.TensionFill.fillAmount, Is.Zero);
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
        }

        [Test]
        public void Shutdown_HidesAndResetsHud()
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), 0f);

            fixture.Controller.ShutdownRuntime();
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.IsHudVisible, Is.False);
            Assert.That(fixture.TensionFill.fillAmount, Is.Zero);
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
        }

        [TestCase(FishingV3Result.Caught)]
        [TestCase(FishingV3Result.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped)]
        public void TerminalResult_HoldsFinalSnapshotWithoutFurtherProgress(FishingV3Result result)
        {
            FishingV3Tuning tuning;
            float reel;
            switch (result)
            {
                case FishingV3Result.LineBroken:
                    tuning = TerminalTuning(0.95f, breakRate: 2f);
                    reel = 0f;
                    break;
                case FishingV3Result.FishEscaped:
                    tuning = TerminalTuning(0.05f, escapeRate: 2f);
                    reel = 0f;
                    break;
                default:
                    tuning = TerminalTuning(0.5f, captureScale: 1f);
                    reel = 1f;
                    break;
            }

            Fixture fixture = CreateFixture(tuning, reel);
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            float tension = fixture.TensionFill.fillAmount;
            float capture = fixture.CaptureFill.fillAmount;

            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(result));
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
            Assert.That(fixture.TensionFill.fillAmount, Is.EqualTo(tension));
            Assert.That(fixture.CaptureFill.fillAmount, Is.EqualTo(capture));
        }

        [Test]
        public void NewSession_ReplacesTerminalValuesWithoutStaleFrame()
        {
            Fixture fixture = CreateFixture(TerminalTuning(0.5f, captureScale: 1f), 1f);
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.CaptureFill.fillAmount, Is.EqualTo(1f));

            fixture.Controller.BeginRound();
            fixture.Controller.SetV3FishState(FishingV3FishState.Fight);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
            Assert.That(fixture.TensionFill.fillAmount,
                Is.EqualTo(fixture.Controller.V3Snapshot.TensionNormalized).Within(0.000001f));
        }

        [Test]
        public void HudPrefab_HasPresenterAndBothConfiguredGauges()
        {
            const string prefabPath = "Assets/Game/Prefabs/Fishing/FishingV3Hud.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            FishingV3HudPresenter presenter = prefab.GetComponent<FishingV3HudPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.HasConfiguredView, Is.True);
        }

        [Test]
        public void PresentationDevelopmentScene_WiresV3RuntimeAndHudWithoutReplacingLegacyScenes()
        {
            const string scenePath =
                "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingV3Presentation.unity";

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindInScene<FishingGameController>(scene), Is.Not.Null);
                Assert.That(FindInScene<FishingMiniGameFacade>(scene), Is.Not.Null);
                Assert.That(FindInScene<FishingV3PresentationDemoBootstrap>(scene), Is.Not.Null);
                FishingV3HudPresenter presenter = FindInScene<FishingV3HudPresenter>(scene);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter.HasConfiguredView, Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private Fixture CreateFixture(
            FishingV3Tuning tuning,
            float reelDelta,
            bool begin = true)
        {
            GameObject host = new GameObject("FishingV3HudPresenterTests");
            _objects.Add(host);
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            SerializedObject facadeObject = new SerializedObject(facade);
            facadeObject.FindProperty("controller").objectReferenceValue = controller;
            facadeObject.ApplyModifiedPropertiesWithoutUndo();
            CountingInputSource input = new CountingInputSource(reelDelta);
            controller.SetInputSource(input);
            controller.ConfigureV3Runtime(
                tuning,
                new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                });
            controller.SetV3FishState(FishingV3FishState.Fight);
            if (begin) controller.BeginRound();

            GameObject viewRoot = new GameObject("HudRoot", typeof(RectTransform));
            viewRoot.transform.SetParent(host.transform, false);
            Image tensionFill = CreateImage("TensionFill", viewRoot.transform);
            Image captureFill = CreateImage("CaptureFill", viewRoot.transform);
            FishingV3HudPresenter presenter = host.AddComponent<FishingV3HudPresenter>();
            presenter.Configure(facade, viewRoot, tensionFill, captureFill);
            return new Fixture(controller, presenter, input, tensionFill, captureFill);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject instance = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<Image>();
        }

        private static FishingV3Tuning StableTuning(float tension)
        {
            return new FishingV3Tuning
            {
                InitialTensionNormalized = tension,
                CalmBaseTension = tension,
                FightBaseTension = tension,
                RunBaseTension = tension,
                ReelTensionGain = 0f,
                CaptureScale = 1f,
                BreakStressPerSecond = 0f,
                EscapeRiskPerSecond = 0f
            };
        }

        private static FishingV3Tuning TerminalTuning(
            float tension,
            float captureScale = 0f,
            float breakRate = 0f,
            float escapeRate = 0f)
        {
            FishingV3Tuning tuning = StableTuning(tension);
            tuning.CaptureScale = captureScale;
            tuning.BreakStressPerSecond = breakRate;
            tuning.EscapeRiskPerSecond = escapeRate;
            return tuning;
        }

        private sealed class CountingInputSource : IFishingInputSource
        {
            private readonly float _reelDelta;
            public bool IsConnected => true;
            public int ReadCount { get; private set; }

            public CountingInputSource(float reelDelta)
            {
                _reelDelta = reelDelta;
            }

            public FishingInputFrame ReadFrame()
            {
                ReadCount++;
                return new FishingInputFrame
                {
                    ReelDelta = _reelDelta,
                    IsDeviceConnected = true
                };
            }

            public void ResetState()
            {
            }
        }

        private readonly struct Fixture
        {
            public FishingGameController Controller { get; }
            public FishingV3HudPresenter Presenter { get; }
            public CountingInputSource Input { get; }
            public Image TensionFill { get; }
            public Image CaptureFill { get; }

            public Fixture(
                FishingGameController controller,
                FishingV3HudPresenter presenter,
                CountingInputSource input,
                Image tensionFill,
                Image captureFill)
            {
                Controller = controller;
                Presenter = presenter;
                Input = input;
                TensionFill = tensionFill;
                CaptureFill = captureFill;
            }
        }
    }
}

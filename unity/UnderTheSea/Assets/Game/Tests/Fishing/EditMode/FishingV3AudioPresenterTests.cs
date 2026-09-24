using System.Collections.Generic;
using System.Linq;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3AudioPresenterTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void HookWindowEntry_RequestsBiteExactlyOnce()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.WaitingForBite));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue, Is.EqualTo(FishingV3AudioCue.Bite));
        }

        [Test]
        public void HookWindowToFighting_RequestsHookSuccessExactlyOnce()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue,
                Is.EqualTo(FishingV3AudioCue.HookSuccess));
        }

        [TestCase(FishingV3TimingGrade.Perfect, FishingV3AudioCue.Perfect)]
        [TestCase(FishingV3TimingGrade.Good, FishingV3AudioCue.Good)]
        [TestCase(FishingV3TimingGrade.Miss, FishingV3AudioCue.Miss)]
        public void TimingJudgementSequence_RequestsMatchingCueExactlyOnce(
            FishingV3TimingGrade grade,
            FishingV3AudioCue expectedCue)
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            FishingV3AudioFrame judgement = Active(
                FishingV3GameplayPhase.Fighting,
                timingGrade: grade,
                timingSequence: 1);

            presenter.StepPresentation(judgement);
            presenter.StepPresentation(judgement);

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue, Is.EqualTo(expectedCue));
        }

        [Test]
        public void StableTimingJudgementAcrossFrames_DoesNotDuplicateCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Good,
                1));

            for (int index = 0; index < 5; index++)
            {
                presenter.StepPresentation(Active(
                    FishingV3GameplayPhase.Fighting,
                    FishingV3TimingGrade.Good,
                    1));
            }

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void PauseResume_WithStableJudgement_DoesNotDuplicateCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Good,
                1));
            presenter.StepPresentation(new FishingV3AudioFrame(
                FishingV3RuntimeState.Paused,
                FishingV3GameplayPhase.Fighting,
                FishingV3Result.Active,
                FishingV3TimingGrade.Good,
                1));
            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Good,
                1));

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue, Is.EqualTo(FishingV3AudioCue.Good));
        }

        [TestCase(FishingV3Result.Caught, FishingV3AudioCue.Caught)]
        [TestCase(FishingV3Result.LineBroken, FishingV3AudioCue.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped, FishingV3AudioCue.FishEscaped)]
        public void TerminalResult_RequestsMatchingCueExactlyOnce(
            FishingV3Result result,
            FishingV3AudioCue expectedCue)
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            FishingV3AudioFrame terminal = Terminal(result);

            presenter.StepPresentation(terminal);
            presenter.StepPresentation(terminal);

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue, Is.EqualTo(expectedCue));
        }

        [Test]
        public void TimingAndTerminalOnSameFrame_RequestTimingBeforeResult()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            List<FishingV3AudioCue> requested = new List<FishingV3AudioCue>();
            presenter.CueRequested += requested.Add;
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));

            presenter.StepPresentation(new FishingV3AudioFrame(
                FishingV3RuntimeState.Completed,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.Caught,
                FishingV3TimingGrade.Good,
                1));

            Assert.That(requested, Is.EqualTo(new[]
            {
                FishingV3AudioCue.Good,
                FishingV3AudioCue.Caught
            }));
        }

        [Test]
        public void HookTimeout_RequestsOnlyFishEscapedCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            List<FishingV3AudioCue> requested = new List<FishingV3AudioCue>();
            presenter.CueRequested += requested.Add;
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));

            presenter.StepPresentation(Terminal(FishingV3Result.FishEscaped));

            Assert.That(requested,
                Is.EqualTo(new[] { FishingV3AudioCue.FishEscaped }));
        }

        [Test]
        public void AbortAndShutdown_DoNotRequestTerminalCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            presenter.StepPresentation(new FishingV3AudioFrame(
                FishingV3RuntimeState.Aborted,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.Active,
                FishingV3TimingGrade.None,
                0));
            presenter.StepPresentation(new FishingV3AudioFrame(
                FishingV3RuntimeState.Shutdown,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.Active,
                FishingV3TimingGrade.None,
                0));

            Assert.That(presenter.CueRequestSequence, Is.Zero);
        }

        [Test]
        public void FirstObservedHistoricalFrame_IsBaselineAndDoesNotReplayCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            FishingV3AudioFrame terminal = new FishingV3AudioFrame(
                FishingV3RuntimeState.Completed,
                FishingV3GameplayPhase.Terminal,
                FishingV3Result.Caught,
                FishingV3TimingGrade.Perfect,
                3);

            presenter.StepPresentation(terminal);
            presenter.StepPresentation(terminal);

            Assert.That(presenter.CueRequestSequence, Is.Zero);
        }

        [Test]
        public void NewSession_ResetsTrackingAndAllowsFreshCues()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            List<FishingV3AudioCue> requested = new List<FishingV3AudioCue>();
            presenter.CueRequested += requested.Add;
            presenter.StepPresentation(Active(FishingV3GameplayPhase.WaitingForBite));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));
            presenter.StepPresentation(Terminal(FishingV3Result.Caught));

            presenter.StepPresentation(Active(FishingV3GameplayPhase.WaitingForBite));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.Fighting));

            Assert.That(requested.Count(cue => cue == FishingV3AudioCue.Bite),
                Is.EqualTo(2));
            Assert.That(requested.Count(cue => cue == FishingV3AudioCue.HookSuccess),
                Is.EqualTo(2));
            Assert.That(requested.Count(cue => cue == FishingV3AudioCue.Caught),
                Is.EqualTo(1));
        }

        [Test]
        public void TimingSequenceRegression_RebaselinesWithoutReplayingOldCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Good,
                5));
            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Miss,
                0));
            Assert.That(presenter.CueRequestSequence, Is.Zero);

            presenter.StepPresentation(Active(
                FishingV3GameplayPhase.Fighting,
                FishingV3TimingGrade.Perfect,
                1));

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue,
                Is.EqualTo(FishingV3AudioCue.Perfect));
        }

        [Test]
        public void AssignedClip_IsExposedForAudioHubPlayback()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            AudioClip clip = AudioClip.Create("FishingV3AudioTest", 64, 1, 8000, false);
            _objects.Add(clip);
            presenter.ConfigureClips(clip, clip, clip, clip, clip, clip, clip, clip);

            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Bite), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.HookSuccess), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Perfect), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Good), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Miss), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Caught), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.LineBroken), Is.SameAs(clip));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.FishEscaped), Is.SameAs(clip));
        }

        [Test]
        public void MissingAudioClip_IsNullSafeAndStillReportsCue()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            presenter.StepPresentation(Active(FishingV3GameplayPhase.WaitingForBite));

            Assert.DoesNotThrow(() =>
                presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow)));
            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.GetConfiguredClip(FishingV3AudioCue.Bite), Is.Null);
        }

        [Test]
        public void ConfiguredClip_StillRequestsExactlyOnceForOneTransition()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();
            AudioClip clip = AudioClip.Create("FishingV3AudioPlaybackTest", 64, 1, 8000, false);
            _objects.Add(clip);
            presenter.ConfigureClips(clip, clip, clip, clip, clip, clip, clip, clip);
            presenter.StepPresentation(Active(FishingV3GameplayPhase.WaitingForBite));

            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));
            presenter.StepPresentation(Active(FishingV3GameplayPhase.HookWindow));

            Assert.That(presenter.CueRequestSequence, Is.EqualTo(1));
            Assert.That(presenter.LastRequestedCue, Is.EqualTo(FishingV3AudioCue.Bite));
        }

        [Test]
        public void DefaultVolumes_MatchFishingSfxTuning()
        {
            FishingV3AudioPresenter presenter = CreatePresenter();

            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.Bite), Is.EqualTo(0.70f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.HookSuccess), Is.EqualTo(0.75f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.Perfect), Is.EqualTo(0.75f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.Good), Is.EqualTo(0.60f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.Miss), Is.EqualTo(0.50f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.Caught), Is.EqualTo(0.85f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.LineBroken), Is.EqualTo(0.80f));
            Assert.That(presenter.GetConfiguredVolume(FishingV3AudioCue.FishEscaped), Is.EqualTo(0.75f));
        }

        [Test]
        public void PlayerIntegrationScene_WiresAudioHubAdapterWithoutDedicatedAudioSource()
        {
            const string scenePath =
                "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingV3PlayerIntegration.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                FishingV3AudioPresenter presenter = FindInScene<FishingV3AudioPresenter>(scene);
                FishingMiniGameFacade facade = FindInScene<FishingMiniGameFacade>(scene);
                System.Type adapterType = System.Type.GetType(
                    "FishingMiniGame.Runtime.FishingV3AudioHubAdapter, Assembly-CSharp",
                    false);
                Assert.That(adapterType, Is.Not.Null);
                Component adapter = presenter != null
                    ? presenter.GetComponent(adapterType)
                    : null;

                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter.HasFacade, Is.True);
                Assert.That(presenter.GetComponent<AudioSource>(), Is.Null);
                Assert.That(adapter, Is.Not.Null);
                Assert.That(presenter.AssignedClipCount, Is.EqualTo(8));

                SerializedObject serialized = new SerializedObject(presenter);
                Assert.That(serialized.FindProperty("facade").objectReferenceValue,
                    Is.EqualTo(facade));

                var adapterData = new SerializedObject(adapter);
                Assert.That(adapterData.FindProperty("presenter").objectReferenceValue,
                    Is.EqualTo(presenter));

                string[] clipPropertyNames =
                {
                    "biteClip",
                    "hookSuccessClip",
                    "perfectClip",
                    "goodClip",
                    "missClip",
                    "caughtClip",
                    "lineBrokenClip",
                    "fishEscapedClip"
                };
                AudioClip[] assignedClips = clipPropertyNames
                    .Select(propertyName =>
                        serialized.FindProperty(propertyName).objectReferenceValue as AudioClip)
                    .ToArray();

                Assert.That(assignedClips, Has.All.Not.Null);
                Assert.That(assignedClips.Distinct().Count(), Is.EqualTo(8));
                Assert.That(assignedClips.Select(AssetDatabase.GetAssetPath),
                    Has.All.StartsWith("Assets/Game/Audio/Fishing/"));

                MonoScript adapterScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/Game/Scripts/Fishing/Integration/FishingV3AudioHubAdapter.cs");
                Assert.That(adapterScript, Is.Not.Null);
                Assert.That(adapterScript.text, Does.Contain("AudioHub.Instance"));
                Assert.That(adapterScript.text, Does.Contain(".PlayOneShot("));
                Assert.That(adapterScript.text, Does.Not.Contain("GetComponent<AudioSource>"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private FishingV3AudioPresenter CreatePresenter()
        {
            GameObject host = new GameObject("FishingV3AudioPresenterTests");
            _objects.Add(host);
            FishingV3AudioPresenter presenter = host.AddComponent<FishingV3AudioPresenter>();
            presenter.Configure(null);
            return presenter;
        }

        private static FishingV3AudioFrame Active(
            FishingV3GameplayPhase phase,
            FishingV3TimingGrade timingGrade = FishingV3TimingGrade.None,
            int timingSequence = 0)
        {
            return new FishingV3AudioFrame(
                FishingV3RuntimeState.Running,
                phase,
                FishingV3Result.Active,
                timingGrade,
                timingSequence);
        }

        private static FishingV3AudioFrame Terminal(FishingV3Result result)
        {
            return new FishingV3AudioFrame(
                FishingV3RuntimeState.Completed,
                FishingV3GameplayPhase.Terminal,
                result,
                FishingV3TimingGrade.None,
                0);
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
    }
}

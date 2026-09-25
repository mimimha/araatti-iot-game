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
            Assert.That(fixture.TensionFill.rectTransform.anchorMax.x,
                Is.EqualTo(tension).Within(0.000001f));
            Assert.That(fixture.Presenter.DisplayedTensionMarkerNormalized,
                Is.EqualTo(tension).Within(0.000001f));
            Assert.That(fixture.TensionValueLabel.text,
                Is.EqualTo($"{tension * 100f:0}%"));
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
            Assert.That(fixture.CaptureFill.rectTransform.anchorMax.x, Is.Zero);
            Assert.That(fixture.CaptureValueLabel.text, Is.EqualTo("0%"));
        }

        [TestCase(0.5f)]
        [TestCase(0.75f)]
        public void CaptureGauge_ReadsProgressFromV3Snapshot(float reelDelta)
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), reelDelta);

            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.CaptureFill.fillAmount,
                Is.EqualTo(reelDelta).Within(0.000001f));
            Assert.That(fixture.CaptureFill.rectTransform.anchorMax.x,
                Is.EqualTo(reelDelta).Within(0.000001f));
            Assert.That(fixture.CaptureValueLabel.text,
                Is.EqualTo($"{reelDelta * 100f:0}%"));
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        public void GaugeFill_UsesNormalizedRectWidthWithoutSprite(float normalized)
        {
            GameObject host = new GameObject("GaugeFillVisualTest", typeof(RectTransform));
            _objects.Add(host);
            Image fill = CreateImage("Fill", host.transform);
            Assert.That(fill.sprite, Is.Null);

            FishingV3HudPresenter.ApplyGaugeFill(fill, normalized);

            Assert.That(fill.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(fill.fillAmount, Is.EqualTo(normalized).Within(0.000001f));
            Assert.That(fill.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(fill.rectTransform.anchorMax.x,
                Is.EqualTo(normalized).Within(0.000001f));
            Assert.That(fill.rectTransform.anchorMax.y, Is.EqualTo(1f));
            Assert.That(fill.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(fill.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
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
        public void RedesignedGauge_CreatesRiskBandMarkerAndEnglishScaleLabels()
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), 0f);

            fixture.Presenter.RefreshNow();

            Transform riskBand = fixture.Presenter.transform.Find(
                "HudRoot/GaugePanel/Tension/Track/RiskBand");
            Transform marker = fixture.Presenter.transform.Find(
                "HudRoot/GaugePanel/Tension/Track/TensionMarker");
            Transform riskShine = fixture.Presenter.transform.Find(
                "HudRoot/GaugePanel/Tension/Track/RiskBandShine");
            Transform panelSurface = fixture.Presenter.transform.Find(
                "HudRoot/GaugePanel/Tension/Surface");
            Text low = FindText(fixture.Presenter.transform, "LowLabel");
            Text danger = FindText(fixture.Presenter.transform, "DangerLabel");
            Text start = FindText(fixture.Presenter.transform, "StartLabel");
            Text catchLabel = FindText(fixture.Presenter.transform, "CatchLabel");

            Assert.That(fixture.Presenter.HasTensionRiskGauge, Is.True);
            Assert.That(fixture.Presenter.HasCaptureProgressScale, Is.True);
            Assert.That(riskBand, Is.Not.Null);
            Assert.That(riskBand.childCount, Is.EqualTo(5));
            Assert.That(riskBand.GetComponent<Mask>(), Is.Not.Null);
            Assert.That(riskBand.GetComponent<Mask>().showMaskGraphic, Is.False);
            RectTransform riskBandRect = riskBand as RectTransform;
            Assert.That(riskBandRect, Is.Not.Null);
            Assert.That(riskBandRect.offsetMin.x, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(riskBandRect.offsetMax.x, Is.EqualTo(-1f).Within(0.000001f));
            Assert.That(marker, Is.Not.Null);
            Assert.That(riskShine, Is.Not.Null);
            Assert.That(riskShine.GetComponent<Image>().color.a, Is.GreaterThan(0f));
            Assert.That(panelSurface, Is.Not.Null);
            Color panelSurfaceColor = panelSurface.GetComponent<Image>().color;
            Assert.That(panelSurfaceColor.r, Is.LessThan(0.12f));
            Assert.That(panelSurfaceColor.g, Is.GreaterThan(panelSurfaceColor.r));
            Assert.That(panelSurfaceColor.b, Is.GreaterThan(panelSurfaceColor.g));
            Image gaugeTrack = riskBand.parent.GetComponent<Image>();
            Assert.That(gaugeTrack, Is.Not.Null);
            Assert.That(gaugeTrack.color.r, Is.InRange(0.40f, 0.46f));
            Assert.That(gaugeTrack.color.g, Is.GreaterThan(gaugeTrack.color.r));
            Assert.That(gaugeTrack.color.b, Is.GreaterThan(gaugeTrack.color.g));
            Assert.That(gaugeTrack.color.a, Is.EqualTo(1f).Within(0.000001f));
            Transform markerArrow = marker.Find("Arrow");
            Assert.That(markerArrow, Is.Not.Null);
            Assert.That(markerArrow.GetComponent<Image>(), Is.Not.Null);
            Assert.That(((RectTransform)markerArrow).sizeDelta.x, Is.GreaterThanOrEqualTo(26f));
            Assert.That(low, Is.Not.Null);
            Assert.That(danger, Is.Not.Null);
            Assert.That(start, Is.Not.Null);
            Assert.That(catchLabel, Is.Not.Null);
            Assert.That(((RectTransform)marker).anchorMin.x,
                Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(((RectTransform)marker).anchorMax.x,
                Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(low.text, Is.EqualTo("낮음"));
            Assert.That(danger.text, Is.EqualTo("위험"));
            Assert.That(start.text, Is.EqualTo("시작"));
            Assert.That(catchLabel.text, Is.EqualTo("포획"));

            string[] expectedSegmentNames =
            {
                "SlackBand",
                "LowBand",
                "SafeBand",
                "HighBand",
                "DangerBand"
            };
            float[] expectedSegmentEnds = { 0.25f, 0.30f, 0.75f, 0.85f, 1f };
            float previousEnd = 0f;
            Color previousColor = Color.clear;
            for (int index = 0; index < riskBand.childCount; index++)
            {
                RectTransform segment = riskBand.GetChild(index) as RectTransform;
                Image segmentImage = segment != null ? segment.GetComponent<Image>() : null;
                Assert.That(segment, Is.Not.Null);
                Assert.That(segmentImage, Is.Not.Null);
                Assert.That(segment.name, Is.EqualTo(expectedSegmentNames[index]));
                Assert.That(segment.anchorMin.x,
                    Is.EqualTo(previousEnd).Within(0.000001f));
                Assert.That(segment.anchorMax.x,
                    Is.EqualTo(expectedSegmentEnds[index]).Within(0.000001f));
                if (index > 0) Assert.That(segmentImage.color, Is.Not.EqualTo(previousColor));
                previousEnd = segment.anchorMax.x;
                previousColor = segmentImage.color;
            }
            Assert.That(previousEnd, Is.EqualTo(1f).Within(0.000001f));
        }

        [TestCase(FishingV3TensionZone.Slack, "느슨함", "지금 감으세요!")]
        [TestCase(FishingV3TensionZone.Low, "느슨함", "지금 감으세요!")]
        [TestCase(FishingV3TensionZone.Safe, "안전", "계속 감으세요!")]
        [TestCase(FishingV3TensionZone.High, "주의", "천천히!")]
        [TestCase(FishingV3TensionZone.Danger, "위험", "릴링을 멈추세요!")]
        public void TensionZone_MapsToReadableLabelAndAction(
            FishingV3TensionZone zone,
            string expectedLabel,
            string expectedHint)
        {
            Assert.That(FishingV3HudPresenter.GetTensionZoneLabel(zone),
                Is.EqualTo(expectedLabel));
            Assert.That(FishingV3HudPresenter.GetTensionHint(zone),
                Is.EqualTo(expectedHint));
        }

        [TestCase(0.20f, FishingV3TensionZone.Slack, "느슨함")]
        [TestCase(0.275f, FishingV3TensionZone.Low, "느슨함")]
        [TestCase(0.50f, FishingV3TensionZone.Safe, "안전")]
        [TestCase(0.80f, FishingV3TensionZone.High, "주의")]
        [TestCase(0.90f, FishingV3TensionZone.Danger, "위험")]
        public void Fighting_UsesSnapshotTensionZoneForStatusAndFillColor(
            float tension,
            FishingV3TensionZone expectedZone,
            string expectedLabel)
        {
            Fixture fixture = CreateFixture(StableTuning(tension), 0f);

            fixture.Presenter.RefreshNow();

            Color expectedColor = FishingV3HudPresenter.GetTensionZoneColor(expectedZone);
            Assert.That(fixture.Presenter.HasTensionStatusView, Is.True);
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.True);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.True);
            Assert.That(fixture.Presenter.DisplayedTensionZone, Is.EqualTo(expectedZone));
            Assert.That(fixture.Presenter.DisplayedTensionZoneLabel, Is.EqualTo(expectedLabel));
            Assert.That(fixture.TensionFill.color, Is.EqualTo(expectedColor));
        }

        [Test]
        public void TensionVisuals_UseFourDistinctReadableColors()
        {
            Color slack = FishingV3HudPresenter.GetTensionZoneColor(FishingV3TensionZone.Slack);
            Color safe = FishingV3HudPresenter.GetTensionZoneColor(FishingV3TensionZone.Safe);
            Color high = FishingV3HudPresenter.GetTensionZoneColor(FishingV3TensionZone.High);
            Color danger = FishingV3HudPresenter.GetTensionZoneColor(FishingV3TensionZone.Danger);

            Assert.That(safe, Is.Not.EqualTo(slack));
            Assert.That(high, Is.Not.EqualTo(safe));
            Assert.That(danger, Is.Not.EqualTo(high));
            Assert.That(
                FishingV3HudPresenter.GetTensionZoneColor(FishingV3TensionZone.Low),
                Is.EqualTo(slack));
        }

        [Test]
        public void TensionGuidance_DependsOnZoneRatherThanFishBehaviorState()
        {
            Fixture fixture = CreateFixture(StableTuning(0.5f), 0f);
            fixture.Controller.SetV3FishState(FishingV3FishState.Run);

            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.FishState,
                Is.EqualTo(FishingV3FishState.Run));
            Assert.That(fixture.Presenter.DisplayedTensionZone,
                Is.EqualTo(FishingV3TensionZone.Safe));
            Assert.That(fixture.Presenter.DisplayedTensionZoneLabel, Is.EqualTo("안전"));
            Assert.That(fixture.Presenter.DisplayedTensionHint, Is.EqualTo("계속 감으세요!"));
        }

        [Test]
        public void TimingMode_ShowsPointerZonesAndLastJudgement()
        {
            Fixture fixture = CreateFixture(
                StableTuning(0.5f),
                0f,
                timingMode: true,
                timingPressed: true);

            fixture.Controller.TickRuntime(0.5f / 0.85f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.HasTimingView, Is.True);
            Assert.That(fixture.Presenter.DisplayedTimingPointerNormalized,
                Is.EqualTo(fixture.Controller.V3Snapshot.TimingPointerNormalized)
                    .Within(0.000001f));
            Assert.That(fixture.Presenter.DisplayedTimingGrade,
                Is.EqualTo(FishingV3TimingGrade.Perfect));
            Assert.That(fixture.Controller.V3Snapshot.TimingGoodHalfWidthNormalized,
                Is.GreaterThan(fixture.Controller.V3Snapshot.TimingPerfectHalfWidthNormalized));

            Transform meter = fixture.Presenter.transform.Find("HudRoot/TimingMeter");
            RectTransform rail = meter != null
                ? meter.Find("TimingRail") as RectTransform
                : null;
            RectTransform pointer = meter != null
                ? meter.Find("TimingRail/Pointer") as RectTransform
                : null;
            Text goodLabel = FindText(meter, "GoodLabel");
            Text perfectLabel = FindText(meter, "PerfectLabel");
            RectTransform prompt = meter != null
                ? meter.Find("TimingPrompt") as RectTransform
                : null;
            Assert.That(meter, Is.Not.Null);
            Assert.That(rail, Is.Not.Null);
            Assert.That(rail.sizeDelta.y, Is.GreaterThanOrEqualTo(28f));
            Assert.That(rail.Find("TimingRailShine"), Is.Not.Null);
            Assert.That(pointer, Is.Not.Null);
            Assert.That(pointer.sizeDelta.x, Is.LessThanOrEqualTo(4f));
            Assert.That(pointer.sizeDelta.y, Is.EqualTo(38f));
            Assert.That(pointer.anchorMin.y, Is.EqualTo(0.5f));
            Assert.That(pointer.anchorMax.y, Is.EqualTo(0.5f));
            Assert.That(pointer.Find("PointerCap"), Is.Not.Null);
            Assert.That(goodLabel, Is.Not.Null);
            Assert.That(goodLabel.text, Is.EqualTo("GOOD"));
            Assert.That(perfectLabel, Is.Not.Null);
            Assert.That(perfectLabel.text, Is.EqualTo("PERFECT"));
            Assert.That(prompt, Is.Not.Null);
            Bounds promptBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                meter,
                prompt);
            Bounds goodLabelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                meter,
                goodLabel.rectTransform);
            Bounds perfectLabelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                meter,
                perfectLabel.rectTransform);
            Assert.That(promptBounds.min.y, Is.GreaterThan(goodLabelBounds.max.y));
            Assert.That(promptBounds.min.y, Is.GreaterThan(perfectLabelBounds.max.y));
        }

        [Test]
        public void BiteHookFlow_ShowsWaitThenHookPromptBeforeTimingMeter()
        {
            Fixture fixture = CreateFixture(
                StableTuning(0.5f),
                0f,
                timingMode: true,
                hookPressed: true,
                biteHookMode: true);

            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Presenter.DisplayedGameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.WaitingForBite));
            Assert.That(fixture.Presenter.DisplayedPhaseMessage, Is.EqualTo("입질을 기다리는 중..."));
            Assert.That(fixture.Controller.V3Snapshot.IsTimingReelActive, Is.False);
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.False);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.False);

            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.DisplayedGameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.HookWindow));
            Assert.That(fixture.Presenter.DisplayedPhaseMessage, Is.EqualTo("지금!"));
            Assert.That(fixture.Controller.V3Snapshot.IsTimingReelActive, Is.False);
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.False);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.False);

            fixture.Controller.TickRuntime(0.01f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.DisplayedGameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.Fighting));
            Assert.That(fixture.Presenter.DisplayedPhaseMessage, Is.EqualTo("J를 누르세요"));
            Assert.That(fixture.Controller.V3Snapshot.IsTimingReelActive, Is.True);
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.True);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.True);
        }

        [Test]
        public void HookWindow_ShowsReadableEventTextWithoutEmptyTimingTrack()
        {
            Fixture fixture = CreateFixture(
                StableTuning(0.5f),
                0f,
                timingMode: true,
                biteHookMode: true);

            fixture.Presenter.RefreshNow();
            Transform eventRoot = fixture.Presenter.transform.Find(
                "HudRoot/FishingEventMessage");
            Transform timingMeter = fixture.Presenter.transform.Find(
                "HudRoot/TimingMeter");
            Text eventText = eventRoot != null
                ? eventRoot.GetComponentInChildren<Text>(true)
                : null;

            Assert.That(eventRoot, Is.Not.Null);
            Assert.That(timingMeter, Is.Not.Null);
            Assert.That(eventText, Is.Not.Null);
            Assert.That(eventRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(timingMeter.gameObject.activeSelf, Is.False);
            Assert.That(eventText.text, Is.EqualTo("입질을 기다리는 중..."));
            Assert.That(eventText.color.a, Is.EqualTo(1f));

            fixture.Controller.TickRuntime(10f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.HookWindow));
            Assert.That(eventRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(eventText.gameObject.activeInHierarchy, Is.True);
            Assert.That(timingMeter.gameObject.activeSelf, Is.False);
            Assert.That(eventText.text, Is.EqualTo("지금!"));
            Assert.That(eventText.font, Is.Not.Null);
            Assert.That(eventText.fontSize, Is.EqualTo(24));
            Assert.That(eventText.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(eventText.color.a, Is.EqualTo(1f));
            Assert.That(eventText.color.maxColorComponent, Is.GreaterThan(0.7f));
            Transform eventSurface = eventRoot.Find("Surface");
            Assert.That(eventSurface, Is.Not.Null);
            Assert.That(eventSurface.GetComponent<Image>().color.a, Is.LessThan(0.9f));
            Assert.That(eventRoot.GetSiblingIndex(), Is.GreaterThan(timingMeter.GetSiblingIndex()));

            TextGenerationSettings settings = eventText.GetGenerationSettings(
                eventText.rectTransform.rect.size);
            Assert.That(eventText.cachedTextGenerator.Populate("지금!", settings), Is.True);
            Assert.That(eventText.cachedTextGenerator.vertexCount, Is.GreaterThan(0));
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
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.False);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.False);
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
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
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
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
            Assert.That(fixture.TensionFill.fillAmount, Is.Zero);
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
        }

        [TestCase(FishingV3Result.Caught, "잡았다!", "멋진 낚시였어요!")]
        [TestCase(FishingV3Result.LineBroken, "줄이 끊어졌다!", "장력이 너무 높았어요!")]
        [TestCase(FishingV3Result.FishEscaped, "물고기가 도망쳤다!", "줄이 너무 느슨했어요!")]
        public void TerminalResult_HidesFightingHudAndShowsOneShotResultOverlay(
            FishingV3Result result,
            string expectedTitle,
            string expectedDescription)
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
            if (result == FishingV3Result.Caught)
            {
                Assert.That(fixture.Presenter.IsCaughtResultPending, Is.True);
                Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
                Assert.That(fixture.Presenter.IsHudVisible, Is.False);
                fixture.Presenter.AdvancePresentation(
                    fixture.Presenter.CaughtResultDelaySeconds - 0.01f);
                Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
                fixture.Presenter.AdvancePresentation(0.02f);
            }
            float remaining = fixture.Presenter.ResultTimeRemainingSeconds;

            fixture.Presenter.RefreshNow();
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(result));
            Assert.That(fixture.Controller.V3Snapshot.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Completed));
            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.False);
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.False);
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.True);
            Assert.That(fixture.Presenter.DisplayedResult, Is.EqualTo(result));
            Assert.That(fixture.Presenter.DisplayedResultTitle, Is.EqualTo(expectedTitle));
            Assert.That(fixture.Presenter.DisplayedResultDescription,
                Is.EqualTo(expectedDescription));
            Assert.That(fixture.Presenter.ResultPresentationSequence, Is.EqualTo(1));
            Assert.That(fixture.Presenter.ResultTimeRemainingSeconds,
                Is.EqualTo(remaining).Within(0.000001f));
            Assert.That(fixture.TensionFill.fillAmount, Is.Zero);
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);
        }

        [Test]
        public void ResultOverlay_AutoHidesAfterConfiguredDuration()
        {
            Fixture fixture = CreateFixture(TerminalTuning(0.5f, captureScale: 1f), 1f);
            fixture.Presenter.ConfigureResultDisplayDuration(0.25f);
            fixture.Presenter.ConfigureCaughtResultDelay(0f);
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.True);
            fixture.Presenter.AdvancePresentation(0.24f);
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.True);
            fixture.Presenter.AdvancePresentation(0.02f);

            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
            Assert.That(fixture.Presenter.IsHudVisible, Is.False);
        }

        [Test]
        public void HookWindowFailure_ShowsFishEscapedResultOverlay()
        {
            Fixture fixture = CreateFixture(
                StableTuning(0.5f),
                0f,
                timingMode: true,
                biteHookMode: true);

            fixture.Controller.TickRuntime(10f);
            Assert.That(fixture.Controller.V3Snapshot.GameplayPhase,
                Is.EqualTo(FishingV3GameplayPhase.HookWindow));
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Controller.V3Snapshot.Result,
                Is.EqualTo(FishingV3Result.FishEscaped));
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.True);
            Assert.That(fixture.Presenter.DisplayedResultTitle, Is.EqualTo("물고기가 도망쳤다!"));
        }

        [Test]
        public void NewSession_DuringTerminalFeedbackClearsResultAndShowsFreshHud()
        {
            Fixture fixture = CreateFixture(TerminalTuning(0.5f, captureScale: 1f), 1f);
            fixture.Controller.TickRuntime(1f);
            fixture.Presenter.RefreshNow();
            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(FishingV3Result.Caught));
            Assert.That(fixture.Presenter.IsHudVisible, Is.False);
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
            Assert.That(fixture.Presenter.IsCaughtResultPending, Is.True);
            Assert.That(fixture.CaptureFill.fillAmount, Is.Zero);

            fixture.Controller.BeginRound();
            fixture.Controller.SetV3FishState(FishingV3FishState.Fight);
            fixture.Presenter.RefreshNow();

            Assert.That(fixture.Presenter.IsHudVisible, Is.True);
            Assert.That(fixture.Presenter.IsResultOverlayVisible, Is.False);
            Assert.That(fixture.Presenter.IsCaughtResultPending, Is.False);
            Assert.That(fixture.Presenter.DisplayedResult, Is.EqualTo(FishingV3Result.Active));
            Assert.That(fixture.Presenter.IsTensionStatusVisible, Is.True);
            Assert.That(fixture.Presenter.AreFightGaugesVisible, Is.True);
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
            Assert.That(presenter.HasHudStyleAssets, Is.True);

            Text controls = null;
            Text catchProgress = null;
            foreach (Text label in prefab.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "Controls")
                {
                    controls = label;
                }
                if (label.text == "포획 진행도") catchProgress = label;
            }
            Assert.That(controls, Is.Not.Null);
            Assert.That(controls.gameObject.activeSelf, Is.False);
            Assert.That(controls.text, Is.Empty);
            Assert.That(catchProgress, Is.Not.Null);

            Transform canvas = prefab.transform.Find("Canvas");
            Transform gaugePanel = prefab.transform.Find("Canvas/GaugePanel");
            RectTransform tensionCard = prefab.transform.Find(
                "Canvas/GaugePanel/Tension") as RectTransform;
            RectTransform captureCard = prefab.transform.Find(
                "Canvas/GaugePanel/Capture") as RectTransform;
            Assert.That(canvas, Is.Not.Null);
            Assert.That(gaugePanel, Is.Not.Null);
            Assert.That(tensionCard, Is.Not.Null);
            Assert.That(captureCard, Is.Not.Null);

            Image legacyPanel = gaugePanel.GetComponent<Image>();
            Image tensionPanel = tensionCard.GetComponent<Image>();
            Image capturePanel = captureCard.GetComponent<Image>();
            Assert.That(legacyPanel, Is.Not.Null);
            Assert.That(legacyPanel.enabled, Is.False);
            Assert.That(tensionPanel, Is.Not.Null);
            Assert.That(capturePanel, Is.Not.Null);
            Assert.That(tensionPanel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(capturePanel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(tensionPanel.sprite, Is.Not.Null);
            Assert.That(capturePanel.sprite, Is.EqualTo(tensionPanel.sprite));
            Assert.That(tensionPanel.sprite.border.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(tensionCard.sizeDelta.y, Is.GreaterThanOrEqualTo(180f));
            Assert.That(captureCard.sizeDelta.y, Is.GreaterThanOrEqualTo(158f));
            Assert.That(((RectTransform)gaugePanel).sizeDelta.x,
                Is.GreaterThanOrEqualTo(535f));
            RectTransform tensionTrack = tensionCard.Find("Track") as RectTransform;
            RectTransform captureTrack = captureCard.Find("Track") as RectTransform;
            Assert.That(tensionTrack, Is.Not.Null);
            Assert.That(captureTrack, Is.Not.Null);
            Assert.That(tensionTrack.sizeDelta.y, Is.GreaterThanOrEqualTo(30f));
            Assert.That(captureTrack.sizeDelta.y, Is.GreaterThanOrEqualTo(30f));
            Assert.That(
                tensionCard.anchoredPosition.y - tensionCard.sizeDelta.y,
                Is.GreaterThan(captureCard.anchoredPosition.y));

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
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
            bool begin = true,
            bool timingMode = false,
            bool timingPressed = false,
            bool hookPressed = false,
            bool biteHookMode = false)
        {
            GameObject host = new GameObject("FishingV3HudPresenterTests");
            _objects.Add(host);
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            SerializedObject facadeObject = new SerializedObject(facade);
            facadeObject.FindProperty("controller").objectReferenceValue = controller;
            facadeObject.ApplyModifiedPropertiesWithoutUndo();
            CountingInputSource input = new CountingInputSource(
                reelDelta,
                timingPressed,
                hookPressed);
            controller.SetInputSource(input);
            controller.ConfigureV3Runtime(
                tuning,
                new FishingV3ReelInputTuning
                {
                    VirtualReelSpeedRevolutionsPerSecond = 1f
                },
                reelControlMode: timingMode
                    ? FishingV3ReelControlMode.Timing
                    : FishingV3ReelControlMode.LegacyHold,
                biteHookTuning: biteHookMode
                    ? new FishingV3BiteHookTuning
                    {
                        BiteDelayMinSeconds = 1f,
                        BiteDelayMaxSeconds = 1f,
                        HookWindowMinSeconds = 0.5f,
                        HookWindowMaxSeconds = 0.5f
                    }
                    : null,
                sessionFlowMode: biteHookMode
                    ? FishingV3SessionFlowMode.BiteHook
                    : FishingV3SessionFlowMode.ImmediateFight);
            controller.SetV3FishState(FishingV3FishState.Fight);
            if (begin) controller.BeginRound();

            GameObject viewRoot = new GameObject("HudRoot", typeof(RectTransform));
            viewRoot.transform.SetParent(host.transform, false);
            GameObject gaugePanel = new GameObject("GaugePanel", typeof(RectTransform));
            gaugePanel.transform.SetParent(viewRoot.transform, false);
            GameObject tensionCard = new GameObject("Tension", typeof(RectTransform));
            tensionCard.transform.SetParent(gaugePanel.transform, false);
            CreateText("Label", tensionCard.transform);
            Text tensionLabel = CreateText("TensionValue", tensionCard.transform);
            Image tensionTrack = CreateImage("Track", tensionCard.transform);
            Image tensionFill = CreateImage("Fill", tensionTrack.transform);
            GameObject captureCard = new GameObject("Capture", typeof(RectTransform));
            captureCard.transform.SetParent(gaugePanel.transform, false);
            CreateText("Label", captureCard.transform);
            Text captureLabel = CreateText("CaptureValue", captureCard.transform);
            Image captureTrack = CreateImage("Track", captureCard.transform);
            Image captureFill = CreateImage("Fill", captureTrack.transform);
            FishingV3HudPresenter presenter = host.AddComponent<FishingV3HudPresenter>();
            presenter.Configure(
                facade,
                viewRoot,
                tensionFill,
                captureFill,
                tensionLabel,
                captureLabel);
            return new Fixture(
                controller,
                presenter,
                input,
                tensionFill,
                captureFill,
                tensionLabel,
                captureLabel);
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

        private static Text FindText(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label.name == objectName) return label;
            }

            return null;
        }

        private static Text CreateText(string name, Transform parent)
        {
            GameObject instance = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<Text>();
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
            private readonly bool _timingPressed;
            private readonly bool _hookPressed;
            public bool IsConnected => true;
            public int ReadCount { get; private set; }

            public CountingInputSource(
                float reelDelta,
                bool timingPressed = false,
                bool hookPressed = false)
            {
                _reelDelta = reelDelta;
                _timingPressed = timingPressed;
                _hookPressed = hookPressed;
            }

            public FishingInputFrame ReadFrame()
            {
                ReadCount++;
                return new FishingInputFrame
                {
                    ReelDelta = _reelDelta,
                    HookPressed = _hookPressed,
                    TimingPressed = _timingPressed,
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
            public Text TensionValueLabel { get; }
            public Text CaptureValueLabel { get; }

            public Fixture(
                FishingGameController controller,
                FishingV3HudPresenter presenter,
                CountingInputSource input,
                Image tensionFill,
                Image captureFill,
                Text tensionValueLabel,
                Text captureValueLabel)
            {
                Controller = controller;
                Presenter = presenter;
                Input = input;
                TensionFill = tensionFill;
                CaptureFill = captureFill;
                TensionValueLabel = tensionValueLabel;
                CaptureValueLabel = captureValueLabel;
            }
        }
    }
}

using FishingMiniGame.Core;
using NUnit.Framework;

namespace FishingMiniGame.Tests
{
    public sealed class FishingSingleFishSessionTests
    {
        [Test]
        public void Catch_CompletesSessionExactlyOnce()
        {
            SingleFishSessionTracker session = CreateSession();
            int completionCount = 0;
            session.Completed += _ => completionCount++;
            session.Begin();

            session.CompletePostHook(Cycle(wasCaught: true));
            session.CompletePostHook(Cycle(wasCaught: true));

            Assert.That(session.Current.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(session.Result.Outcome, Is.EqualTo(FishingSessionOutcome.Caught));
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void PostHookEscape_CompletesSession()
        {
            SingleFishSessionTracker session = CreateSession();
            session.Begin();

            session.CompletePostHook(Cycle(false, FishingEscapeReason.LineBroken));

            Assert.That(session.Current.State, Is.EqualTo(FishingSessionState.Completed));
            Assert.That(session.Result.Outcome, Is.EqualTo(FishingSessionOutcome.Escaped));
            Assert.That(session.Result.CycleResult.EscapeReason, Is.EqualTo(FishingEscapeReason.LineBroken));
        }

        [Test]
        public void PreHookFailure_KeepsSameFishAndAllowsRetry()
        {
            SingleFishSessionTracker session = CreateSession();
            session.Begin();
            string selectedFish = session.SelectedFish.FishId;

            session.RecordPreHookFailure(Cycle(false, FishingEscapeReason.MissedBite));

            Assert.That(session.Current.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(session.Result, Is.Null);
            Assert.That(session.Current.PreHookFailureCount, Is.EqualTo(1));
            Assert.That(session.SelectedFish.FishId, Is.EqualTo(selectedFish));
        }

        [Test]
        public void ElapsedTimeBeyondLegacyRoundLimit_DoesNotCompleteSession()
        {
            SingleFishSessionTracker session = CreateSession();
            session.Begin();

            for (int i = 0; i < 800; i++) session.Tick(0.25f);

            Assert.That(session.Current.ElapsedSeconds, Is.EqualTo(200f).Within(0.01f));
            Assert.That(session.Current.State, Is.EqualTo(FishingSessionState.Playing));
            Assert.That(session.Result, Is.Null);
        }

        [Test]
        public void InjectedFishProfile_IsUsedWithoutSessionCodeChanges()
        {
            FishProfile amberjack = Fish("greater_amberjack", "Greater Amberjack");
            SingleFishSessionTracker session = CreateSession(amberjack);
            session.Begin();

            Assert.That(session.Current.FishId, Is.EqualTo("greater_amberjack"));
            Assert.That(session.Current.FishDisplayName, Is.EqualTo("Greater Amberjack"));
            Assert.That(session.SelectedFish.FishId, Is.EqualTo("greater_amberjack"));
        }

        private static SingleFishSessionTracker CreateSession(FishProfile fish = null)
        {
            SingleFishSessionTracker session = new SingleFishSessionTracker();
            session.Initialize(new FishingSessionContext
            {
                SessionId = "test-session",
                ParticipantId = "tester",
                CountdownSeconds = 0f,
                Fish = fish ?? Fish("red_sea_bream", "Red Sea Bream")
            });
            return session;
        }

        private static FishProfile Fish(string id, string displayName)
        {
            return new FishProfile
            {
                FishId = id,
                DisplayName = displayName,
                DifficultyLabel = "Normal"
            };
        }

        private static FishingCycleResult Cycle(
            bool wasCaught,
            FishingEscapeReason reason = FishingEscapeReason.None)
        {
            return new FishingCycleResult
            {
                CycleNumber = 1,
                WasCaught = wasCaught,
                EscapeReason = reason,
                FishId = "red_sea_bream",
                FishDisplayName = "Red Sea Bream",
                DifficultyLabel = "Normal",
                AwardedScore = wasCaught ? 170 : 0,
                RemainingLineDurability = 80f
            };
        }
    }
}

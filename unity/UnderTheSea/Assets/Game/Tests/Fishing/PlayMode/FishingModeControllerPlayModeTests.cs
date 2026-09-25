using System;
using System.Collections;
using System.Reflection;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class FishingModeControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator DisableEnable_CleansSessionAndRestoresOneSubscription()
        {
            Fixture fixture = CreateFixture();
            yield return null;

            Assert.That(SubscriptionCount(fixture.Spot), Is.EqualTo(1));
            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));

            fixture.Mode.enabled = false;
            yield return null;

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Spot.IsBusy, Is.False);
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Aborted));
            Assert.That(SubscriptionCount(fixture.Spot), Is.Zero);

            fixture.Mode.enabled = true;
            yield return null;

            Assert.That(SubscriptionCount(fixture.Spot), Is.EqualTo(1));
            Assert.That(fixture.Spot.TryInteract(fixture.Interactor), Is.True);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));

            UnityEngine.Object.Destroy(fixture.Host);
            UnityEngine.Object.Destroy(fixture.Spot.gameObject);
            UnityEngine.Object.Destroy(fixture.Interactor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Destroy_UnsubscribesAndReleasesActiveSpot()
        {
            Fixture fixture = CreateFixture();
            yield return null;
            fixture.Spot.TryInteract(fixture.Interactor);

            UnityEngine.Object.Destroy(fixture.Mode);
            yield return null;

            Assert.That(fixture.Spot.IsBusy, Is.False);
            Assert.That(SubscriptionCount(fixture.Spot), Is.Zero);
            Assert.That(fixture.Facade.V3Current.RuntimeState,
                Is.EqualTo(FishingV3RuntimeState.Aborted));

            UnityEngine.Object.Destroy(fixture.Host);
            UnityEngine.Object.Destroy(fixture.Spot.gameObject);
            UnityEngine.Object.Destroy(fixture.Interactor);
            yield return null;
        }

        private static Fixture CreateFixture()
        {
            var host = new GameObject("FishingModePlayModeHost");
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            FishingModeController mode = host.AddComponent<FishingModeController>();
            FishingSpot spot = new GameObject("FishingSpot").AddComponent<FishingSpot>();
            var interactor = new GameObject("Interactor");
            mode.Bind(spot);
            return new Fixture(host, controller, facade, mode, spot, interactor);
        }

        private static int SubscriptionCount(FishingSpot spot)
        {
            FieldInfo field = typeof(FishingSpot).GetField(
                "FishingRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Delegate handlers = field?.GetValue(spot) as Delegate;
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private readonly struct Fixture
        {
            public Fixture(
                GameObject host,
                FishingGameController controller,
                FishingMiniGameFacade facade,
                FishingModeController mode,
                FishingSpot spot,
                GameObject interactor)
            {
                Host = host;
                Controller = controller;
                Facade = facade;
                Mode = mode;
                Spot = spot;
                Interactor = interactor;
            }

            public GameObject Host { get; }
            public FishingGameController Controller { get; }
            public FishingMiniGameFacade Facade { get; }
            public FishingModeController Mode { get; }
            public FishingSpot Spot { get; }
            public GameObject Interactor { get; }
        }
    }
}

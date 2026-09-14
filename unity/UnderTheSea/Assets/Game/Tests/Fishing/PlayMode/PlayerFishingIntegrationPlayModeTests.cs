using System;
using System.Collections;
using System.Reflection;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishingMiniGame.Tests
{
    public sealed class PlayerFishingIntegrationPlayModeTests
    {
        private const string AdapterTypeName =
            "FishingMiniGame.Runtime.PlayerFishingAdapter, Assembly-CSharp";
        private const string InputProviderTypeName = "PlayerInputProvider, Assembly-CSharp";

        [UnityTest]
        public IEnumerator OwnSession_LocksThroughPauseAndRestoresOnAbort()
        {
            Fixture fixture = CreateFixture();
            yield return null;

            Refresh(fixture.Adapter);
            Assert.That(TryInteract(fixture.Adapter), Is.True);
            yield return null;

            Assert.That(IsLocked(fixture.Provider), Is.True);
            Assert.That(fixture.Mode.RequestPause(), Is.True);
            yield return null;
            Assert.That(IsLocked(fixture.Provider), Is.True);

            Assert.That(fixture.Mode.Abort(), Is.True);
            yield return null;

            Assert.That(IsLocked(fixture.Provider), Is.False);
            Assert.That(fixture.Spot.IsBusy, Is.False);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));

            DestroyFixture(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableDuringOwnSession_AbortsAndRestoresWithoutStaleState()
        {
            Fixture fixture = CreateFixture();
            yield return null;
            Refresh(fixture.Adapter);
            Assert.That(TryInteract(fixture.Adapter), Is.True);
            Assert.That(IsLocked(fixture.Provider), Is.True);

            ((Behaviour)fixture.Adapter).enabled = false;
            yield return null;

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Spot.IsBusy, Is.False);
            Assert.That(IsLocked(fixture.Provider), Is.False);

            ((Behaviour)fixture.Adapter).enabled = true;
            yield return null;
            SetField(fixture.Adapter, "_localPlayerGameObject", fixture.Player);
            SetField(fixture.Adapter, "_inputProvider", fixture.Provider);
            Refresh(fixture.Adapter);
            Assert.That(ReadProperty<bool>(fixture.Adapter, "OwnsMovementLock"), Is.False);
            Assert.That(IsLocked(fixture.Provider), Is.False);

            DestroyFixture(fixture);
            yield return null;
        }

        private static Fixture CreateFixture()
        {
            var host = new GameObject("FishingPlayerIntegrationPlayModeHost");
            host.SetActive(false);
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            FishingModeController mode = host.AddComponent<FishingModeController>();
            Component adapter = host.AddComponent(RequiredType(AdapterTypeName));
            FishingSpot spot = new GameObject("FishingSpot").AddComponent<FishingSpot>();

            SetField(facade, "controller", controller);
            SetField(mode, "facade", facade);
            SetField(adapter, "fishingModeController", mode);
            SetField(adapter, "fishingSpots", new[] { spot });
            SetField(adapter, "interactionDistance", 2f);
            host.SetActive(true);

            var providerObject = new GameObject("InputProvider");
            Component provider = providerObject.AddComponent(RequiredType(InputProviderTypeName));
            var player = new GameObject("LocalPlayer");
            SetField(adapter, "_localPlayerGameObject", player);
            SetField(adapter, "_inputProvider", provider);

            return new Fixture(host, mode, adapter, providerObject, provider, player, spot);
        }

        private static void DestroyFixture(Fixture fixture)
        {
            UnityEngine.Object.Destroy(fixture.Host);
            UnityEngine.Object.Destroy(fixture.ProviderObject);
            UnityEngine.Object.Destroy(fixture.Player);
            UnityEngine.Object.Destroy(fixture.Spot.gameObject);
        }

        private static bool TryInteract(Component adapter)
        {
            return (bool)InvokeNonPublic(adapter, "TryInteractCurrentSpot");
        }

        private static void Refresh(Component adapter)
        {
            InvokeNonPublic(adapter, "Update");
        }

        private static bool IsLocked(Component provider)
        {
            return ReadProperty<bool>(provider, "IsMovementLocked");
        }

        private static Type RequiredType(string name)
        {
            Type type = Type.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static T ReadProperty<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static object InvokeNonPublic(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
        }

        private readonly struct Fixture
        {
            public Fixture(
                GameObject host,
                FishingModeController mode,
                Component adapter,
                GameObject providerObject,
                Component provider,
                GameObject player,
                FishingSpot spot)
            {
                Host = host;
                Mode = mode;
                Adapter = adapter;
                ProviderObject = providerObject;
                Provider = provider;
                Player = player;
                Spot = spot;
            }

            public GameObject Host { get; }
            public FishingModeController Mode { get; }
            public Component Adapter { get; }
            public GameObject ProviderObject { get; }
            public Component Provider { get; }
            public GameObject Player { get; }
            public FishingSpot Spot { get; }
        }
    }
}

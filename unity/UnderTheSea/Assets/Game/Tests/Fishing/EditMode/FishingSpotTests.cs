using System.Collections.Generic;
using System.Reflection;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingSpotTests
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

        [Test]
        public void NewSpot_IsAvailableForAValidInteractor()
        {
            FishingSpot spot = CreateSpot();
            GameObject interactor = CreateObject("Interactor");

            Assert.That(spot.InteractionEnabled, Is.True);
            Assert.That(spot.IsBusy, Is.False);
            Assert.That(spot.CanInteract, Is.True);
            Assert.That(spot.TryInteract(interactor), Is.True);
        }

        [Test]
        public void DisabledOrInvalidInteraction_DoesNotRaiseRequest()
        {
            FishingSpot spot = CreateSpot();
            GameObject interactor = CreateObject("Interactor");
            int requestCount = 0;
            spot.FishingRequested += _ => requestCount++;

            Assert.That(spot.TryInteract(null), Is.False);

            spot.SetInteractionEnabled(false);
            Assert.That(spot.TryInteract(interactor), Is.False);

            spot.SetInteractionEnabled(true);
            spot.enabled = false;
            Assert.That(spot.TryInteract(interactor), Is.False);
            Assert.That(requestCount, Is.Zero);
        }

        [Test]
        public void TryInteract_RaisesOneRequestWithSpotAndInteractorContext()
        {
            FishingSpot spot = CreateSpot();
            GameObject interactor = CreateObject("Interactor");
            int requestCount = 0;
            FishingSpotInteractionRequest received = default;
            spot.FishingRequested += request =>
            {
                requestCount++;
                received = request;
            };

            bool firstAccepted = spot.TryInteract(interactor);
            bool duplicateAccepted = spot.TryInteract(interactor);

            Assert.That(firstAccepted, Is.True);
            Assert.That(duplicateAccepted, Is.False);
            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(received.Spot, Is.SameAs(spot));
            Assert.That(received.Interactor, Is.SameAs(interactor));
            Assert.That(spot.IsBusy, Is.True);
        }

        [Test]
        public void Release_AllowsANewRequestWithoutRetainingOldContext()
        {
            FishingSpot spot = CreateSpot();
            GameObject first = CreateObject("FirstInteractor");
            GameObject second = CreateObject("SecondInteractor");
            var interactors = new List<GameObject>();
            spot.FishingRequested += request => interactors.Add(request.Interactor);

            Assert.That(spot.TryInteract(first), Is.True);
            spot.Release();
            Assert.That(spot.TryInteract(second), Is.True);

            Assert.That(interactors, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void TriggerMessages_DoNotRaiseFishingRequest()
        {
            FishingSpot spot = CreateSpot();
            BoxCollider other = CreateObject("OtherCollider").AddComponent<BoxCollider>();
            int requestCount = 0;
            spot.FishingRequested += _ => requestCount++;

            spot.gameObject.SendMessage(
                "OnTriggerEnter", other, SendMessageOptions.DontRequireReceiver);
            spot.gameObject.SendMessage(
                "OnTriggerExit", other, SendMessageOptions.DontRequireReceiver);

            Assert.That(requestCount, Is.Zero);
            Assert.That(spot.IsBusy, Is.False);
        }

        [Test]
        public void Spot_DoesNotOwnInputOrFramePollingCallbacks()
        {
            const BindingFlags callbacks = BindingFlags.Instance |
                                           BindingFlags.Public |
                                           BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly;

            Assert.That(typeof(FishingSpot).GetMethod("Update", callbacks), Is.Null);
            Assert.That(typeof(FishingSpot).GetMethod("FixedUpdate", callbacks), Is.Null);
            Assert.That(typeof(FishingSpot).GetMethod("OnTriggerEnter", callbacks), Is.Null);
            Assert.That(typeof(FishingSpot).GetMethod("OnTriggerExit", callbacks), Is.Null);
        }

        [Test]
        public void SpotSource_HasNoDirectInputOrFishingSessionDependencies()
        {
            const string sourcePath =
                "Assets/Game/Scripts/Fishing/Runtime/Integration/FishingSpot.cs";
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(sourcePath);

            Assert.That(script, Is.Not.Null);
            string source = script.text;
            string[] forbiddenTokens =
            {
                "Input.GetKey",
                "Keyboard.current",
                "InputAction",
                "FishingV3Model",
                "FishingV3Runtime",
                "FishingMiniGameFacade",
                "FishingV3HudPresenter",
                "FishingResistance"
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        public void FishingSpotPrefab_IsAStandaloneTriggerEndpoint()
        {
            const string prefabPath = "Assets/Game/Prefabs/Fishing/FishingSpot.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<FishingSpot>(), Is.Not.Null);
            BoxCollider trigger = prefab.GetComponent<BoxCollider>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(prefab.GetComponents<Component>().Length, Is.EqualTo(3));
        }

        private FishingSpot CreateSpot()
        {
            return CreateObject("FishingSpot").AddComponent<FishingSpot>();
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            _objects.Add(instance);
            return instance;
        }
    }
}

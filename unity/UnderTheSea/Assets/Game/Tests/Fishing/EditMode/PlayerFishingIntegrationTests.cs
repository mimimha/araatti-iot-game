using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class PlayerFishingIntegrationTests
    {
        private const string AdapterTypeName =
            "FishingMiniGame.Runtime.PlayerFishingAdapter, Assembly-CSharp";
        private const string InputProviderTypeName = "PlayerInputProvider, Assembly-CSharp";

        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void MovementLock_DefaultsUnlockedFiltersOnlyDirectionAndCanUnlock()
        {
            Component provider = CreateComponent("InputProvider", RequiredType(InputProviderTypeName));

            Assert.That(ReadProperty<bool>(provider, "IsMovementLocked"), Is.False);
            Assert.That(FilterDirection(provider, new Vector2(1f, -0.5f)),
                Is.EqualTo(new Vector2(1f, -0.5f)));

            Invoke(provider, "SetMovementLocked", true);

            Assert.That(ReadProperty<bool>(provider, "IsMovementLocked"), Is.True);
            Assert.That(FilterDirection(provider, new Vector2(1f, -0.5f)), Is.EqualTo(Vector2.zero));

            Invoke(provider, "SetMovementLocked", false);

            Assert.That(ReadProperty<bool>(provider, "IsMovementLocked"), Is.False);
            Assert.That(FilterDirection(provider, Vector2.up), Is.EqualTo(Vector2.up));
        }

        [Test]
        public void MovementLock_SourceKeepsNetworkInputSubmissionPath()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Game/Scripts/Network/PlayerInputProvider.cs");
            Assert.That(script, Is.Not.Null);
            Assert.That(script.text, Does.Contain("input.Set(data);"));
            Assert.That(script.text, Does.Contain("data.Direction = ApplyMovementLock(rawDirection);"));
            Assert.That(script.text, Does.Not.Contain("if (IsMovementLocked) return"));
            Assert.That(script.text, Does.Not.Contain("if (IsMovementLocked)\n            return"));
        }

        [Test]
        public void Adapter_UsesProjectWideInteractAndApprovedBoundariesOnly()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Game/Scripts/Fishing/Integration/PlayerFishingAdapter.cs");
            Assert.That(script, Is.Not.Null);

            Assert.That(script.text, Does.Contain("InputSystem.actions"));
            Assert.That(script.text, Does.Contain("Player/Interact"));
            Assert.That(script.text, Does.Contain(".performed += OnInteractPerformed"));
            Assert.That(script.text, Does.Contain(".performed -= OnInteractPerformed"));
            Assert.That(script.text, Does.Contain("LocalPlayer.Registered"));
            Assert.That(script.text, Does.Contain("LocalPlayer.Unregistered"));
            Assert.That(script.text, Does.Contain("_localPlayer.Runner"));
            Assert.That(script.text, Does.Contain("!player.HasInputAuthority"));
            Assert.That(script.text,
                Does.Contain("fishVisualPresenter?.ConfigureCaughtPresentation"));

            string[] forbiddenTokens =
            {
                "new InputActionAsset",
                "new InputSystem_Actions",
                "Keyboard.current",
                "FindObjectOfType",
                "FindAnyObjectByType",
                "FindFirstObjectByType",
                "GameObject.Find",
                "CompareTag",
                "FishingMiniGameFacade.Abort",
                "Runtime.Abort",
                ".Release()",
                "TickRuntime(",
                "IFishingResistanceOutput",
                "Time.timeScale"
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(script.text, Does.Not.Contain(token), token);
            }
        }

        [Test]
        public void FishingKeyboardBindings_UseCForInteractAndJForSemanticActions()
        {
            string inputActionsPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Assets/InputSystem_Actions.inputactions"));
            string inputActions = File.ReadAllText(inputActionsPath);
            MonoScript keyboardSource = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Game/Scripts/Fishing/Runtime/Infrastructure/KeyboardFishingInputSource.cs");

            Assert.That(Regex.Matches(
                    inputActions,
                    "\\\"path\\\": \\\"<Keyboard>/c\\\"").Count,
                Is.EqualTo(1));
            Assert.That(Regex.IsMatch(
                    inputActions,
                    "\\\"path\\\": \\\"<Keyboard>/c\\\".*?\\\"action\\\": \\\"Interact\\\"",
                    RegexOptions.Singleline),
                Is.True);
            Assert.That(inputActions, Does.Not.Contain("\"path\": \"<Keyboard>/e\""));
            Assert.That(keyboardSource, Is.Not.Null);
            Assert.That(keyboardSource.text, Does.Contain("KeyCode.J"));
            Assert.That(keyboardSource.text, Does.Not.Contain("KeyCode.F"));
            Assert.That(keyboardSource.text, Does.Contain("HookPressed = fishingActionDown"));
            Assert.That(keyboardSource.text, Does.Contain("TimingPressed = fishingActionDown"));
        }

        [Test]
        public void PersonalIntegrationScene_HasRequiredBindingsAndNoMissingScripts()
        {
            const string scenePath =
                "Assets/Game/Scenes/Develop/Yongju/FishingScenes/FishingV3PlayerIntegration.unity";
            Scene scene = default;

            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                Assert.That(scene.IsValid(), Is.True);

                Component adapter = null;
                FishingGameController controller = null;
                FishingMiniGameFacade facade = null;
                FishingModeController mode = null;
                FishingV3HudPresenter hud = null;
                FishingSpot spot = null;
                int missingScriptCount = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    {
                        Component[] components = item.GetComponents<Component>();
                        foreach (Component component in components)
                        {
                            if (component == null)
                            {
                                missingScriptCount++;
                                continue;
                            }

                            if (component.GetType() == RequiredType(AdapterTypeName)) adapter = component;
                            else if (component is FishingGameController foundController) controller = foundController;
                            else if (component is FishingMiniGameFacade foundFacade) facade = foundFacade;
                            else if (component is FishingModeController foundMode) mode = foundMode;
                            else if (component is FishingV3HudPresenter foundHud) hud = foundHud;
                            else if (component is FishingSpot foundSpot) spot = foundSpot;
                        }
                    }
                }

                Assert.That(missingScriptCount, Is.Zero);
                Assert.That(adapter, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(facade, Is.Not.Null);
                Assert.That(mode, Is.Not.Null);
                Assert.That(hud, Is.Not.Null);
                Assert.That(spot, Is.Not.Null);

                var adapterData = new SerializedObject(adapter);
                Assert.That(adapterData.FindProperty("fishingModeController").objectReferenceValue,
                    Is.SameAs(mode));
                SerializedProperty spots = adapterData.FindProperty("fishingSpots");
                Assert.That(spots.arraySize, Is.EqualTo(1));
                Assert.That(spots.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(spot));

                var modeData = new SerializedObject(mode);
                Assert.That(modeData.FindProperty("facade").objectReferenceValue, Is.SameAs(facade));
                Assert.That(modeData.FindProperty("fishingSpot").objectReferenceValue, Is.SameAs(spot));

                var facadeData = new SerializedObject(facade);
                Assert.That(facadeData.FindProperty("controller").objectReferenceValue,
                    Is.SameAs(controller));

                var hudData = new SerializedObject(hud);
                Assert.That(hudData.FindProperty("facade").objectReferenceValue, Is.SameAs(facade));
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NearestAvailableSpot_IsSelectedDeterministically()
        {
            FishingSpot first = CreateObject("FirstSpot").AddComponent<FishingSpot>();
            FishingSpot second = CreateObject("SecondSpot").AddComponent<FishingSpot>();
            first.transform.position = Vector3.left;
            second.transform.position = Vector3.right;
            Fixture fixture = CreateFixture(new[] { first, second }, 2f);

            Refresh(fixture.Adapter);

            Assert.That(ReadProperty<FishingSpot>(fixture.Adapter, "CurrentFishingSpot"),
                Is.SameAs(first));
        }

        [Test]
        public void OutsideProximity_InteractIsIgnored()
        {
            FishingSpot spot = CreateObject("FarSpot").AddComponent<FishingSpot>();
            spot.transform.position = Vector3.right * 10f;
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            Refresh(fixture.Adapter);

            Assert.That(TryInteract(fixture.Adapter), Is.False);
            Assert.That(spot.IsBusy, Is.False);
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(IsLocked(fixture.Provider), Is.False);
        }

        [Test]
        public void OneInteract_StartsOwnSessionOnceAndLocksMovement()
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            int requestCount = 0;
            spot.FishingRequested += _ => requestCount++;
            Refresh(fixture.Adapter);

            Assert.That(TryInteract(fixture.Adapter), Is.True);
            Assert.That(TryInteract(fixture.Adapter), Is.False);

            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Active));
            Assert.That(fixture.Mode.CurrentInteractor, Is.SameAs(fixture.Player));
            Assert.That(IsLocked(fixture.Provider), Is.True);
            Assert.That(ReadProperty<bool>(fixture.Adapter, "OwnsMovementLock"), Is.True);
        }

        [Test]
        public void AnotherInteractorSession_DoesNotLockLocalMovement()
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            GameObject remote = CreateObject("RemoteInteractor");
            fixture.Mode.Bind(spot);

            Assert.That(spot.TryInteract(remote), Is.True);
            Refresh(fixture.Adapter);

            Assert.That(fixture.Mode.CurrentInteractor, Is.SameAs(remote));
            Assert.That(IsLocked(fixture.Provider), Is.False);
            Assert.That(ReadProperty<bool>(fixture.Adapter, "OwnsMovementLock"), Is.False);
        }

        [Test]
        public void PauseKeepsLockAndAbortRestoresPreviousUnlockedState()
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            Refresh(fixture.Adapter);
            Assert.That(TryInteract(fixture.Adapter), Is.True);

            Assert.That(fixture.Mode.RequestPause(), Is.True);
            Refresh(fixture.Adapter);
            Assert.That(IsLocked(fixture.Provider), Is.True);

            Assert.That(fixture.Mode.RequestResume(), Is.True);
            Refresh(fixture.Adapter);
            Assert.That(IsLocked(fixture.Provider), Is.True);

            Assert.That(fixture.Mode.Abort(), Is.True);
            Refresh(fixture.Adapter);
            Assert.That(IsLocked(fixture.Provider), Is.False);
            Assert.That(spot.IsBusy, Is.False);
        }

        [Test]
        public void ExistingLockedState_IsRestoredAfterFishing()
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            SetLocked(fixture.Provider, true);
            Refresh(fixture.Adapter);

            Assert.That(TryInteract(fixture.Adapter), Is.True);
            Assert.That(fixture.Mode.Abort(), Is.True);
            Refresh(fixture.Adapter);

            Assert.That(IsLocked(fixture.Provider), Is.True);
            Assert.That(ReadProperty<bool>(fixture.Adapter, "OwnsMovementLock"), Is.False);
        }

        [TestCase(FishingV3Result.Caught)]
        [TestCase(FishingV3Result.LineBroken)]
        [TestCase(FishingV3Result.FishEscaped)]
        public void TerminalResult_RestoresMovementAndReleasesSpot(FishingV3Result result)
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            Refresh(fixture.Adapter);
            Assert.That(TryInteract(fixture.Adapter), Is.True);

            ConfigureTerminal(fixture, result);
            InvokeNonPublic(fixture.Mode, "Update");
            Refresh(fixture.Adapter);

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Mode.LastResult, Is.EqualTo(result));
            Assert.That(IsLocked(fixture.Provider), Is.False);
            Assert.That(spot.IsBusy, Is.False);
        }

        [Test]
        public void DisableDuringOwnSession_AbortsRestoresAndLeavesNoBusySpot()
        {
            FishingSpot spot = CreateObject("Spot").AddComponent<FishingSpot>();
            Fixture fixture = CreateFixture(new[] { spot }, 2f);
            Refresh(fixture.Adapter);
            Assert.That(TryInteract(fixture.Adapter), Is.True);

            // EditMode does not drive MonoBehaviour disable callbacks consistently;
            // invoke the lifecycle boundary directly. The PlayMode counterpart toggles
            // Behaviour.enabled and verifies Unity's real callback dispatch.
            InvokeNonPublic(fixture.Adapter, "OnDisable");

            Assert.That(fixture.Mode.State, Is.EqualTo(FishingModeLifecycleState.Inactive));
            Assert.That(fixture.Facade.V3Current.RuntimeState, Is.EqualTo(FishingV3RuntimeState.Aborted));
            Assert.That(IsLocked(fixture.Provider), Is.False);
            Assert.That(spot.IsBusy, Is.False);
        }

        private Fixture CreateFixture(FishingSpot[] spots, float range)
        {
            GameObject host = CreateObject("FishingIntegrationHost");
            host.SetActive(false);
            FishingGameController controller = host.AddComponent<FishingGameController>();
            FishingMiniGameFacade facade = host.AddComponent<FishingMiniGameFacade>();
            FishingModeController mode = host.AddComponent<FishingModeController>();
            Component adapter = host.AddComponent(RequiredType(AdapterTypeName));

            var serializedFacade = new SerializedObject(facade);
            serializedFacade.FindProperty("controller").objectReferenceValue = controller;
            serializedFacade.ApplyModifiedPropertiesWithoutUndo();

            var serializedMode = new SerializedObject(mode);
            serializedMode.FindProperty("facade").objectReferenceValue = facade;
            serializedMode.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(adapter);
            serialized.FindProperty("fishingModeController").objectReferenceValue = mode;
            SerializedProperty spotArray = serialized.FindProperty("fishingSpots");
            spotArray.arraySize = spots.Length;
            for (int index = 0; index < spots.Length; index++)
            {
                spotArray.GetArrayElementAtIndex(index).objectReferenceValue = spots[index];
            }

            serialized.FindProperty("interactionDistance").floatValue = range;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            host.SetActive(true);

            Component provider = CreateComponent("InputProvider", RequiredType(InputProviderTypeName));
            GameObject player = CreateObject("LocalPlayer");
            player.transform.position = Vector3.zero;
            SetField(adapter, "_localPlayerGameObject", player);
            SetField(adapter, "_inputProvider", provider);

            return new Fixture(controller, facade, mode, adapter, provider, player);
        }

        private static void ConfigureTerminal(Fixture fixture, FishingV3Result result)
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
            fixture.Controller.SetInputSource(new FixedInputSource(reel));
            fixture.Controller.ConfigureV3Runtime(tuning, new FishingV3ReelInputTuning
            {
                VirtualReelSpeedRevolutionsPerSecond = 1f
            });
            fixture.Controller.SetV3FishState(FishingV3FishState.Fight);
            fixture.Controller.BeginRound();
            fixture.Controller.TickRuntime(1f);
            Assert.That(fixture.Controller.V3Snapshot.Result, Is.EqualTo(result));
        }

        private static Vector2 FilterDirection(Component provider, Vector2 direction)
        {
            return (Vector2)InvokeNonPublic(provider, "ApplyMovementLock", direction);
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

        private static void SetLocked(Component provider, bool locked)
        {
            Invoke(provider, "SetMovementLocked", locked);
        }

        private Component CreateComponent(string name, Type type)
        {
            return CreateObject(name).AddComponent(type);
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            _objects.Add(instance);
            return instance;
        }

        private static Type RequiredType(string qualifiedName)
        {
            Type type = Type.GetType(qualifiedName, false);
            Assert.That(type, Is.Not.Null, qualifiedName);
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

        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(target, arguments);
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
                FishingGameController controller,
                FishingMiniGameFacade facade,
                FishingModeController mode,
                Component adapter,
                Component provider,
                GameObject player)
            {
                Controller = controller;
                Facade = facade;
                Mode = mode;
                Adapter = adapter;
                Provider = provider;
                Player = player;
            }

            public FishingGameController Controller { get; }
            public FishingMiniGameFacade Facade { get; }
            public FishingModeController Mode { get; }
            public Component Adapter { get; }
            public Component Provider { get; }
            public GameObject Player { get; }
        }

        private sealed class FixedInputSource : IFishingInputSource
        {
            private readonly float _reel;

            public FixedInputSource(float reel)
            {
                _reel = reel;
            }

            public bool IsConnected => true;

            public FishingInputFrame ReadFrame()
            {
                return new FishingInputFrame
                {
                    ReelDelta = _reel,
                    IsDeviceConnected = true
                };
            }

            public void ResetState()
            {
            }
        }
    }
}

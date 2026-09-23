using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class NetworkPlayerFishingPresentationIntegrationTests
    {
        private const string ComponentTypeName =
            "FishingMiniGame.Runtime.NetworkPlayerFishingPresentation, Assembly-CSharp";
        private const string PlayerPrefabPath =
            "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";
        private const string BridgeScriptPath =
            "Assets/Game/Scripts/Fishing/Integration/NetworkPlayerFishingPresentation.cs";

        [Test]
        public void NetworkPlayerPrefab_HasExactlyOneFishingPresentationBridge()
        {
            Type type = Type.GetType(ComponentTypeName);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Assert.That(type, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            int count = prefab.GetComponents<Component>()
                .Count(component => component != null && component.GetType() == type);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void Bridge_UsesExistingFusionAuthorityPatternAndSemanticStateOnly()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(BridgeScriptPath);

            Assert.That(script, Is.Not.Null);
            Assert.That(script.text,
                Does.Contain("RpcSources.InputAuthority, RpcTargets.StateAuthority"));
            Assert.That(script.text, Does.Contain("info.Source != Object.InputAuthority"));
            Assert.That(script.text, Does.Contain("HasInputAuthority || Runner == null || Runner.IsServer"));
            Assert.That(script.text, Does.Contain("FishingPhaseCode"));
            Assert.That(script.text, Does.Contain("TerminalResultCode"));
            Assert.That(script.text, Does.Contain("FishVisualId"));
            Assert.That(script.text, Does.Contain("SessionSequence"));
            Assert.That(script.text, Does.Not.Contain("CaptureProgressNormalized"));
            Assert.That(script.text, Does.Not.Contain("NetworkTransform"));
            Assert.That(script.text, Does.Not.Contain("PlayerInputProvider"));
        }

        [Test]
        public void Adapter_PublishesLocalSnapshotWithoutChangingMovementOrInputOwner()
        {
            MonoScript adapter = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Game/Scripts/Fishing/Integration/PlayerFishingAdapter.cs");

            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.text, Does.Contain("PublishLocalSnapshot"));
            Assert.That(adapter.text, Does.Contain("HandleFishingSessionEnded"));
            Assert.That(adapter.text, Does.Contain("RestoreMovementLock"));
            Assert.That(adapter.text, Does.Not.Contain("Rpc_"));
        }

        [Test]
        public void Bridge_UsesRodOnlyRemotePresentationAndCleansOnDespawn()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(BridgeScriptPath);

            Assert.That(script.text, Does.Contain("override void Despawned"));
            Assert.That(script.text, Does.Contain("CleanupRemotePresentation(true)"));
            Assert.That(script.text, Does.Contain("RemoteRodCount"));
            Assert.That(script.text, Does.Contain("public int RemoteFishCount => 0;"));
            Assert.That(script.text, Does.Not.Contain("FishingV3FishVisualPresenter"));
            Assert.That(script.text, Does.Not.Contain("ConfigureRemotePresentation"));
            Assert.That(script.text, Does.Contain("if (HasInputAuthority"));
        }
    }
}

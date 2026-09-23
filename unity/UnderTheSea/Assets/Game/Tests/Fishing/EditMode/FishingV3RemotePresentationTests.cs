using System.Collections.Generic;
using FishingMiniGame.Core;
using FishingMiniGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishingMiniGame.Tests.EditMode
{
    public sealed class FishingV3RemotePresentationTests
    {
        private readonly List<Object> _ownedObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                if (_ownedObjects[index] != null)
                {
                    Object.DestroyImmediate(_ownedObjects[index]);
                }
            }

            _ownedObjects.Clear();
        }

        [Test]
        public void RemoteRod_IsSingleInstanceCleansAtTerminalAndSupportsReentry()
        {
            GameObject player = Own(new GameObject("RemotePlayer"));
            Transform hand = Own(new GameObject("RightHandProp")).transform;
            hand.SetParent(player.transform, false);
            Transform target = Own(new GameObject("RemoteFishAnchor")).transform;
            target.SetParent(player.transform, false);
            target.localPosition = new Vector3(0f, 0.4f, 2f);
            FishingV3PlayerPresentation presenter =
                player.AddComponent<FishingV3PlayerPresentation>();
            presenter.ConfigureRemotePlayer(player.transform, target);

            FishingV3RemotePresentationFrame waiting = ActiveFrame(
                FishingV3GameplayPhase.WaitingForBite);
            presenter.StepRemotePresentation(waiting, 0.1f);
            GameObject firstRod = presenter.RodRoot;
            presenter.StepRemotePresentation(waiting, 0.1f);

            Assert.That(presenter.HasRodVisual, Is.True);
            Assert.That(presenter.RodRoot, Is.SameAs(firstRod));

            presenter.StepRemotePresentation(
                TerminalFrame(FishingV3Result.LineBroken),
                0.1f);
            Assert.That(presenter.HasRodVisual, Is.False);

            presenter.StepRemotePresentation(waiting, 0.1f);
            Assert.That(presenter.HasRodVisual, Is.True);
            Assert.That(presenter.RodRoot, Is.Not.SameAs(firstRod));
        }

        private T Own<T>(T value) where T : Object
        {
            _ownedObjects.Add(value);
            return value;
        }

        private static FishingV3RemotePresentationFrame ActiveFrame(
            FishingV3GameplayPhase phase)
        {
            return new FishingV3RemotePresentationFrame(
                true,
                true,
                false,
                phase,
                FishingV3Result.Active,
                FishingV3FishProfileId.Normal,
                "normal",
                FishingV3FishState.Fight,
                FishingV3TensionZone.High,
                0,
                0f);
        }

        private static FishingV3RemotePresentationFrame TerminalFrame(
            FishingV3Result result)
        {
            return new FishingV3RemotePresentationFrame(
                true,
                false,
                false,
                FishingV3GameplayPhase.Terminal,
                result,
                FishingV3FishProfileId.Normal,
                "normal",
                FishingV3FishState.Fight,
                FishingV3TensionZone.High,
                0,
                0f);
        }
    }
}

using System;
using UnityEngine;

namespace Warriors
{
    public enum WarriorsIoTFeedbackType
    {
        CorrectAttack,
        WrongAttack,
        PlayerDamaged,
        ComboMilestone,
        UltimateReady,
        FinalSwingReady
    }

    [Serializable]
    public readonly struct WarriorsIoTFeedback
    {
        public int PlayerId { get; }
        public WarriorsIoTFeedbackType Type { get; }
        public float Intensity { get; }

        public WarriorsIoTFeedback(int playerId, WarriorsIoTFeedbackType type, float intensity)
        {
            PlayerId = Math.Max(0, playerId);
            Type = type;
            Intensity = Math.Clamp(intensity, 0f, 1f);
        }
    }

    public sealed class WarriorsIoTFeedbackHub : MonoBehaviour
    {
        public event Action<WarriorsIoTFeedback> FeedbackRequested;

        public void Request(int playerId, WarriorsIoTFeedbackType type, float intensity = 1f)
        {
            FeedbackRequested?.Invoke(new WarriorsIoTFeedback(playerId, type, intensity));
        }
    }
}

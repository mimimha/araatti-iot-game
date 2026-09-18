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
        FinalSwingReady,

        /// <summary>동료가 쓰러졌다. 구조를 요청하는 패턴.</summary>
        MateDown,

        /// <summary>협동 창이 열렸다. "지금이다" 준비 신호.</summary>
        CoopWindowOpen,
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

using System;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsTeamGauge : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximum = 100f;
        public float Value { get; private set; }
        public float Normalized => Mathf.Clamp01(Value / maximum);
        public bool IsReady => Value >= maximum;
        public event Action<float> Changed;
        public event Action Ready;

        public void ResetGauge()
        {
            Value = 0f;
            Changed?.Invoke(Normalized);
        }

        public void Add(float amount)
        {
            if (amount <= 0f || IsReady) return;
            bool wasReady = IsReady;
            Value = Mathf.Min(maximum, Value + amount);
            Changed?.Invoke(Normalized);
            if (!wasReady && IsReady) Ready?.Invoke();
        }
    }
}

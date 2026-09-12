using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsIoTInput : MonoBehaviour, IWarriorsInputSource, IWarriorsPlayerInputSource
    {
        [Header("Local routing")]
        [SerializeField, Range(0, 3)] private int localPlayerId;

        [Header("Rule-based IMU thresholds")]
        [SerializeField, Min(.01f)] private float horizontalAngularThreshold = 3.2f;
        [SerializeField, Min(.01f)] private float verticalAngularThreshold = 3.2f;
        [SerializeField, Min(.01f)] private float thrustAccelerationThreshold = 5.5f;
        [SerializeField, Range(0f, 1f)] private float minimumStrength = .35f;
        [SerializeField, Min(.05f)] private float swingCooldownSeconds = .45f;

        private readonly Dictionary<int, double> lastAcceptedTimestamp = new();

        public event Action<WarriorsAttackDirection, float> AttackRequested;
        public event Action DodgeRequested;
        public event Action<WarriorsAttackInput> PlayerAttackRequested;

        public bool OnSwing(int playerId, WarriorsAttackDirection attackType, float strength, double timestamp)
        {
            strength = Mathf.Clamp01(strength);
            if (strength < minimumStrength) return false;
            if (lastAcceptedTimestamp.TryGetValue(playerId, out double previous) &&
                timestamp - previous < swingCooldownSeconds) return false;

            lastAcceptedTimestamp[playerId] = timestamp;
            WarriorsAttackInput input = new(playerId, attackType, strength, timestamp);
            PlayerAttackRequested?.Invoke(input);
            if (playerId == localPlayerId) AttackRequested?.Invoke(attackType, strength);
            return true;
        }

        public bool SubmitImuSample(int playerId, Vector3 linearAcceleration, Vector3 angularVelocity, double timestamp)
        {
            float horizontal = Mathf.Abs(angularVelocity.y);
            float vertical = Mathf.Abs(angularVelocity.x);
            float thrust = Mathf.Max(0f, linearAcceleration.z);

            WarriorsAttackDirection type;
            float strength;
            if (thrust >= thrustAccelerationThreshold && thrust >= horizontal && thrust >= vertical)
            {
                type = WarriorsAttackDirection.Thrust;
                strength = thrust / thrustAccelerationThreshold;
            }
            else if (horizontal >= horizontalAngularThreshold && horizontal >= vertical)
            {
                type = WarriorsAttackDirection.HorizontalSlash;
                strength = horizontal / horizontalAngularThreshold;
            }
            else if (vertical >= verticalAngularThreshold)
            {
                type = WarriorsAttackDirection.VerticalSlash;
                strength = vertical / verticalAngularThreshold;
            }
            else
            {
                return false;
            }

            return OnSwing(playerId, type, Mathf.Clamp01(strength), timestamp);
        }

        public void OnDodge(int playerId)
        {
            if (playerId == localPlayerId) DodgeRequested?.Invoke();
        }
    }
}

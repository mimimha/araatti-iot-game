using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsIoTInput : MonoBehaviour, IWarriorsInputSource, IWarriorsPlayerInputSource
    {
        [Header("Local routing")]
        [Tooltip("이 화면의 주인 번호. 네트워크 판에서는 서버가 정한 번호로 런타임에 덮어쓴다. " +
                 "혼자 하는 검증 씬에서만 인스펙터 값이 쓰인다.")]
        [SerializeField, Range(0, 3)] private int localPlayerId;

        /// <summary>이 화면의 주인 번호. 보내는 쪽이 누구 것으로 보낼지 맞출 때 읽는다.</summary>
        public int LocalPlayerId => localPlayerId;

        /// <summary>
        /// **이 화면의 주인이 몇 번인지 알려 준다.** 네트워크 판에서 반드시 불러야 한다.
        ///
        /// <b>왜 필요한가.</b> <see cref="OnSwing"/> 은 <c>playerId</c> 가 이 값과 같을 때만
        /// <see cref="AttackRequested"/> 를 올린다. 그런데 이 값은 프리팹에 박힌 <b>0</b> 이고
        /// 런타임에 아무도 고쳐 주지 않았다. 네트워크에서 내 번호는 서버가 정하므로,
        /// <b>2P 는 검을 휘둘러도 여기서 걸러져 공격이 나가지 않았다.</b>
        ///
        /// 번호 필터 자체는 남겨 둔다. 한 PC 에서 두 사람이 하는 검증 씬은 이것으로 갈린다.
        /// </summary>
        public void SetLocalPlayerId(int playerId)
        {
            if (localPlayerId == playerId) return;

            localPlayerId = playerId;
            Debug.Log($"[Warriors IoT] 이 화면의 주인을 {playerId + 1}P 로 맞췄습니다.", this);
        }

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

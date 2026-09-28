using System;
using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// 이름은 기존 호환을 위해 KeyboardInput이지만 키보드를 직접 읽지 않는다.
    /// IPlayerController의 양손 입력을 무쌍 공격 방향으로 번역한다.
    /// KeyboardPlayerController를 실제 IoT 구현체로 교체해도 이 코드는 그대로 쓴다.
    /// </summary>
    public sealed class WarriorsKeyboardInput : MonoBehaviour, IWarriorsInputSource, IWarriorsPlayerInputSource
    {
        public event Action<WarriorsAttackDirection, float> AttackRequested;
        public event Action DodgeRequested;
        public event Action<WarriorsAttackInput> PlayerAttackRequested;

        /// <summary>
        /// Who this keyboard speaks for. It used to be hard coded to 0, which meant a second
        /// player's swings were judged against the first player's ROUND 3 lane no matter what
        /// - the lane logic downstream was already per player, the id never was.
        /// </summary>
        [SerializeField, Range(0, 1)] private int playerId;

        [Tooltip("IPlayerController 구현체. 비우면 같은 오브젝트와 부모에서 찾는다.")]
        [SerializeField] private MonoBehaviour playerControllerSource;

        private IPlayerController playerController;

        public int PlayerId => playerId;

        public WarriorsAttackDirection? LastRequestedAttack { get; private set; }

        private void Awake() => ResolvePlayerController();

        public void ConfigurePlayerController(MonoBehaviour source)
        {
            playerControllerSource = source;
            ResolvePlayerController();
        }

        private void Update()
        {
            if (playerController == null) ResolvePlayerController();
            if (playerController == null) return;

            // 어느 손인지가 아니라 IMU가 판정한 실제 동작 종류로 공격을 정한다.
            // 한 기기 모드에서는 Left와 Right가 같은 객체이므로 두 번째 호출은 false다.
            if (TryConsumeAttack(playerController.Left)) return;
            TryConsumeAttack(playerController.Right);
        }

        private bool TryConsumeAttack(IHandDevice hand)
        {
            if (hand == null || !hand.TryConsumeMotion(out HandMotion motion)) return false;

            switch (motion.Type)
            {
                case HandMotionType.HorizontalSwing:
                    RequestAttack(WarriorsAttackDirection.HorizontalSlash, motion.Strength);
                    return true;
                case HandMotionType.VerticalSwing:
                    RequestAttack(WarriorsAttackDirection.VerticalSlash, motion.Strength);
                    return true;
                case HandMotionType.Thrust:
                    RequestAttack(WarriorsAttackDirection.Thrust, motion.Strength);
                    return true;
                default:
                    return false;
            }
        }

        private void ResolvePlayerController()
        {
            playerController = playerControllerSource as IPlayerController
                ?? GetComponent<IPlayerController>()
                ?? GetComponentInParent<IPlayerController>();
        }

        public void RequestAttack(WarriorsAttackDirection direction) => RequestAttack(direction, 1f);

        private void RequestAttack(WarriorsAttackDirection direction, float strength)
        {
            LastRequestedAttack = direction;
            PlayerAttackRequested?.Invoke(new WarriorsAttackInput(
                playerId,
                direction,
                strength,
                Time.realtimeSinceStartupAsDouble));
            AttackRequested?.Invoke(direction, strength);
        }
    }
}

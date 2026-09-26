using UnityEngine;
using Warriors;
using Warriors.Net;

/// <summary>
/// 완드로 무쌍의 <b>네트워크</b> 판을 치게 만드는 다리.
///
///     IPlayerController.TryConsumeMotion  →  WarriorsIoTInput.OnSwing
///
/// **무쌍 폴더의 파일을 하나도 고치지 않습니다.** 무쌍 쪽이 열어 둔 공개 입구만 씁니다.
/// <c>WarriorsInputProvider</c> 주석이 *"장치가 붙는 쪽에서 <c>WarriorsIoTInput.OnSwing</c> 을
/// 부르면 그 순간부터 이 경로로 흘러든다"* 라고 적어 둔 그 자리입니다.
/// (<see cref="IotFishingBridge"/> 와 같은 방식)
///
/// 그 뒤는 무쌍이 이미 다 해 둡니다.
///
///     OnSwing → AttackRequested → WarriorsInputProvider.HandleDeviceSwing → PushSwing
///             → pendingType · pendingStrength · pendingTick 을 다음 OnInput 틱에 싣는다
///
/// "사건 → 틱" 변환까지 <c>PushSwing</c> 이 하므로 여기서는 신경 쓰지 않습니다.
///
/// ⚠ **네트워크 판에서만 돕니다.** <see cref="WarriorsInputProvider"/> 가 있을 때만 값을
///   넘깁니다. 그것이 네트워크 경로에만 있는 부품입니다(러너에 붙습니다).
///
///   혼자 하는 검증 씬(<c>WarriorsTest</c>)에는 <c>WarriorsKeyboardInput</c> 이 살아 있어
///   **이미 같은 <c>TryConsumeMotion</c> 을 읽고 있습니다.** 동작은 읽으면 사라지므로
///   (<c>IPlayerController</c> 주석) 여기서 또 읽으면 **둘 중 하나가 못 받습니다.**
///   네트워크 프리팹에서는 <c>WarriorsSceneSetup.StripLocalOnlyParts</c> 가
///   <c>WarriorsKeyboardInput</c> 을 떼므로 읽는 곳이 여기 하나만 남습니다.
///
/// ⚠ **게이트를 통과하기 전에는 <see cref="IotPlayerController.Persistent"/> 를 건드리지
///   않습니다.** 부르는 순간 COM 포트를 엽니다. <c>WarriorsTest</c> 는 씬에 컨트롤러를
///   직접 두는데, 동글 포트는 한 곳만 열려서 둘 중 하나가 완드를 못 받습니다. (7-7 2번)
///
/// <c>WarriorsIoTInput</c> 과 같은 오브젝트에 붙습니다. 프리팹에 올리지 않습니다 —
/// <see cref="IotWarriorsInstaller"/> 가 씬이 로드될 때 코드로 붙입니다.
/// (<c>WarriorsGameRoot.prefab</c> 은 무쌍 담당 것입니다)
///
/// 서버 빌드에서는 컨트롤러가 <c>null</c> 이라 아무것도 하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WarriorsIoTInput))]
public sealed class IotWarriorsBridge : MonoBehaviour
{
    private WarriorsIoTInput device;

    /// <summary>
    /// 네트워크 경로가 살아 있는지. 없으면 이 다리는 조용히 있는다.
    ///
    /// 매 프레임 찾지 않는다. 러너는 판이 도는 동안 사라지지 않으므로 한 번 찾으면 된다.
    /// 못 찾은 동안에만 다시 찾는다 — 러너는 씬보다 늦게 생길 수 있다.
    /// </summary>
    private WarriorsInputProvider provider;

    /// <summary>러너를 다시 찾아보는 간격(초).</summary>
    private const float ProviderSearchIntervalSeconds = 1f;

    /// <summary>다음에 러너를 찾아볼 시각. 못 찾은 동안 매 프레임 씬을 뒤지지 않으려고 둔다.</summary>
    private float nextProviderSearch;

    private void Awake()
    {
        device = GetComponent<WarriorsIoTInput>();
    }

    private void Update()
    {
        if (provider == null)
        {
            // ⚠ 이따금만 뒤진다. 검증 씬에는 러너가 **영영 없어서**, 그대로 두면 매 프레임
            //   씬 전체를 훑는다. 로비 쪽도 같은 이유로 1초 간격을 썼었다. (7-7)
            if (Time.unscaledTime < nextProviderSearch)
            {
                return;
            }

            nextProviderSearch = Time.unscaledTime + ProviderSearchIntervalSeconds;
            provider = FindAnyObjectByType<WarriorsInputProvider>(FindObjectsInactive.Include);

            // 아직 네트워크 판이 아니다. 여기서 멈춰야 Persistent 를 안 건드린다.
            if (provider == null)
            {
                return;
            }
        }

        IPlayerController controller = IotPlayerController.Persistent;

        if (controller == null)
        {
            return;
        }

        // 양손을 다 훑는다. 어느 손에 IMU 가 있든 공격이 들어와야 한다.
        // 기기가 1대면 Left 와 Right 가 같은 객체라 두 번째는 false 다.
        // WarriorsKeyboardInput 이 로컬에서 하는 것과 같은 모양이다.
        Consume(controller.Left);
        Consume(controller.Right);
    }

    private void Consume(IHandDevice hand)
    {
        if (hand == null || !hand.TryConsumeMotion(out HandMotion motion))
        {
            return;
        }

        WarriorsAttackDirection direction = ToDirection(motion.Type);

        if (direction == WarriorsAttackDirection.None)
        {
            return;
        }

        // 거절되면 false 가 온다 — minimumStrength(0.35) 미만이거나 swingCooldownSeconds(0.45)
        // 안에 또 들어온 경우다. 완드에도 250ms 쿨다운이 있어 **이중**이 된다.
        // 두 값은 무쌍 담당이 정할 자리라 여기서 건드리지 않는다. (7-2)
        device.OnSwing(device.LocalPlayerId, direction, motion.Strength, Time.unscaledTimeAsDouble);
    }

    private static WarriorsAttackDirection ToDirection(HandMotionType type)
    {
        switch (type)
        {
            case HandMotionType.HorizontalSwing: return WarriorsAttackDirection.HorizontalSlash;
            case HandMotionType.VerticalSwing:   return WarriorsAttackDirection.VerticalSlash;
            case HandMotionType.Thrust:          return WarriorsAttackDirection.Thrust;
            default:                             return WarriorsAttackDirection.None;
        }
    }
}

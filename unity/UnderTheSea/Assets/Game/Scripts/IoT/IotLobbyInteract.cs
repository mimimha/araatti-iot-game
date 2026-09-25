using UnderTheSea.Lobby;
using UnityEngine;

/// <summary>
/// 로비에서 **왼손 버튼 1 하나**로 낚시 · 포탈 · 제단을 모두 상호작용하게 만든다.
///
/// 키보드는 셋을 다른 키로 나눠 두었습니다.
///
///     C   낚시 개시   (Player/Interact 액션 → PlayerFishingAdapter)
///     F   미니게임 입장 (MiniGamePortal)
///     E   제단        (AltarInteraction)
///
/// 완드에는 면버튼이 손마다 2개뿐이고 이동 · 카메라 · 점프 · 달리기 · 낚시 챔질이
/// 이미 나머지를 다 쓰고 있어서, 셋을 키처럼 나눌 자리가 없습니다. 그래서
/// **"지금 가까이 있는 것과 상호작용한다"** 하나로 합칩니다.
/// IOT_INPUT.md 2장 "같은 입력이 상황에 따라 달라지는 것" 과 같은 방식입니다.
///
/// 셋이 동시에 사정거리에 들어올 일은 없게 배치되어 있지만, 혹시 겹치면 아래 순서로
/// 하나만 집습니다. 가장 안쪽 동작부터입니다.
///
///     1. 낚시터   (제일 가까이 가야 닿는다)
///     2. 포탈     (4m)
///     3. 제단
///
/// ⚠ **왼손 버튼 1 을 읽는 곳은 여기 하나뿐이어야 합니다.**
///   <c>ConsumeButton1Press</c> 는 한 번만 참을 돌려주고 스스로 지웁니다. 두 곳에서
///   부르면 한쪽이 놓칩니다. (IPlayerController 주석)
///   그래서 <see cref="IotFishingBridge"/> 는 버튼을 읽지 않고 이쪽이 불러 줍니다.
///
/// ⚠ **키보드 경로는 그대로 둡니다.** C · F · E 는 예전처럼 따로 동작합니다.
///   완드가 없으면 이 부품은 스스로 꺼집니다.
///
/// 로비 씬 아무 곳에나 하나 올립니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class IotLobbyInteract : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 씬에서 찾는다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Tooltip("낚시 다리. 비워두면 씬에서 찾는다. 없으면 낚시를 뺀 나머지만 된다.")]
    [SerializeField] private IotFishingBridge fishingBridge;

    private IPlayerController _controller;

    private void Start()
    {
        // ⚠ KeyboardPlayerController 는 찾지 않는다. 그것의 왼손 버튼 1 도 C 라서,
        //   키보드 경로와 여기가 같은 C 를 두 번 먹는다.
        _controller = playerControllerSource as IPlayerController
                      ?? FindAnyObjectByType<IotPlayerController>();

        if (fishingBridge == null) fishingBridge = FindAnyObjectByType<IotFishingBridge>();

        if (_controller == null)
        {
            // 완드가 없는 자리다. 키보드가 예전처럼 다 한다.
            enabled = false;
        }
    }

    private void Update()
    {
        if (_controller == null) return;

        // 낚시 중에는 상호작용 버튼이 놀아야 한다. 낚시터 앞에 선 채로 낚시를 하고 있으므로
        // 그대로 두면 낚시 도중에 같은 낚시터에 또 걸거나 옆의 포탈로 들어가 버린다.
        if (fishingBridge != null && fishingBridge.IsFishing)
        {
            _controller.Left.ConsumeButton1Press();
            return;
        }

        if (!_controller.Left.ConsumeButton1Press()) return;
        if (ChatFocus.Typing) return;

        if (fishingBridge != null && fishingBridge.TryBeginFishing()) return;
        if (TryEnterPortal()) return;

        AltarInteraction.Active?.TryInteractFromDevice();
    }

    /// <summary>
    /// 가까이 있는 포탈로 들어간다.
    ///
    /// 거리는 포탈이 스스로 재고 있으므로(<c>PlayerIsNear</c>) 그 답을 그대로 쓴다.
    /// 여기서 또 재면 F 로는 들어가는데 완드로는 안 되는 자리가 생긴다.
    ///
    /// ⚠ 버튼을 누른 그 프레임에만 씬을 뒤진다. 매 프레임 찾으면 비싸다.
    /// </summary>
    private bool TryEnterPortal()
    {
        MiniGamePortal[] portals = FindObjectsByType<MiniGamePortal>(FindObjectsSortMode.None);

        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i] != null && portals[i].TryEnterFromDevice())
            {
                return true;
            }
        }

        return false;
    }
}

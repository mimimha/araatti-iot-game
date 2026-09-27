using System.Collections.Generic;
using UnderTheSea.Lobby;
using UnderTheSea.Lobby.Dance;
using UnderTheSea.Network;
using UnityEngine;

/// <summary>
/// 로비에서 **왼손 버튼 1 하나**로 낚시 · 포탈 · 이정표 · 제단을 모두 상호작용하고,
/// **왼손 버튼 2** 로 춤 휠을 연다.
///
/// 키보드는 넷을 다른 키로 나눠 두었습니다.
///
///     C   낚시 개시   (Player/Interact 액션 → PlayerFishingAdapter)
///     F   미니게임 입장 (MiniGamePortal) · 이정표 이동 창 (SignpostTeleport)
///     E   제단        (AltarInteraction)
///     Q   춤 휠       (DanceWheelView)
///
/// 완드에는 면버튼이 손마다 2개뿐이고 이동 · 카메라 · 점프 · 달리기 · 낚시 챔질이
/// 이미 나머지를 다 쓰고 있어서, 넷을 키처럼 나눌 자리가 없습니다. 그래서
/// **"지금 가까이 있는 것과 상호작용한다"** 하나로 합칩니다.
/// IOT_INPUT.md 2장 "같은 입력이 상황에 따라 달라지는 것" 과 같은 방식입니다.
///
/// 겹치면 아래 순서로 하나만 집습니다.
///
///     1. 낚시터   (제일 가까이 가야 닿는다)
///     2. 포탈 · 이정표 · 제단 중 **내 캐릭터에서 가장 가까운 것**
///
/// 이정표는 포탈마다 바로 앞에 서 있어서 둘 다 4m 안에 드는 자리가 흔합니다. 고정 순서로
/// 고르면 한쪽이 영영 안 눌리므로 거리로 가릅니다.
///
/// 춤 휠 — 왼손 버튼 2 로 열고, 오른손 스틱으로 칸을 가리키고, 왼손 버튼 2 를 다시 누르면
/// 그 춤을 고릅니다. 아무 칸도 안 가리켰으면 그냥 닫힙니다. 휠이 열린 동안에는 카메라가
/// 오른손 스틱을 쓰지 않습니다(<c>LocalPlayerView</c>). 완드 1대면 오른손 스틱이 없어 열고
/// 닫기만 됩니다.
///
/// 로비 튜토리얼이 떠 있는 동안에는 왼손 버튼 2 가 춤 휠 대신 튜토리얼 [건너뛰기] 입니다.
///
/// ⚠ **왼손 버튼 1 · 2 를 로비에서 읽는 곳은 여기 하나뿐이어야 합니다.**
///   <c>ConsumeButton1Press</c> 는 한 번만 참을 돌려주고 스스로 지웁니다. 두 곳에서
///   부르면 한쪽이 놓칩니다. (IPlayerController 주석)
///   그래서 <see cref="IotFishingBridge"/> 는 버튼을 읽지 않고 이쪽이 불러 줍니다.
///
/// ⚠ **선택지 창이 떠 있으면 비켜 줍니다.** 제단 봉헌 · 이정표 이동 · 인원 선택 창에서는
///   왼손 버튼이 그 창의 버튼을 누르는 데 쓰입니다(<see cref="IotUiNavigator"/>). 여기서 먼저
///   가져가면 창이 영영 안 눌립니다.
///
/// ⚠ **키보드 경로는 그대로 둡니다.** C · F · E · Q 는 예전처럼 따로 동작합니다.
///   완드가 안 붙어 있는 동안에는 이 부품이 아무것도 읽지 않습니다. 그때
///   <c>IotPlayerController</c> 는 왼손 버튼 1 을 C 로 대신 채우는데, 그것을 읽으면
///   C 한 번에 낚시와 포탈이 함께 걸립니다. (<c>IotPlayerController.IsWandLive</c>)
///
/// 씬에 올리지 않습니다. <see cref="IotLobbyInstaller"/> 가 로비에 들어올 때 하나 만듭니다.
/// 씬에 직접 올려 두면 그쪽이 쓰이고 또 만들지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class IotLobbyInteract : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 게임 내내 하나인 완드(IotPlayerController.Persistent)를 쓴다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Tooltip("낚시 다리. 비워두면 씬에서 찾는다. 없으면 낚시를 뺀 나머지만 된다.")]
    [SerializeField] private IotFishingBridge fishingBridge;

    private IPlayerController _controller;

    /// <summary>춤 휠. 로비에만 켜지는 DontDestroyOnLoad 오브젝트라 누를 때 찾아 쥐어 둔다.</summary>
    private DanceWheelView _danceWheel;

    /// <summary>가까운 순으로 늘어놓을 상호작용 대상. 누를 때마다 새로 만들지 않으려고 돌려 쓴다.</summary>
    private readonly List<(float sqrDistance, Component target)> _nearby = new List<(float, Component)>();

    private void Start()
    {
        // ⚠ KeyboardPlayerController 를 꽂지 않는다. 그것의 왼손 버튼 1 도 C 라서,
        //   키보드 경로와 여기가 같은 C 를 두 번 먹는다.
        _controller = playerControllerSource as IPlayerController
                      ?? IotPlayerController.Persistent;

        if (fishingBridge == null) fishingBridge = FindAnyObjectByType<IotFishingBridge>();

        if (_controller == null)
        {
            // 서버 빌드다. 완드가 없다.
            enabled = false;
        }
    }

    private void Update()
    {
        // 완드가 안 붙어 있으면 읽지 않는다. 키보드가 예전처럼 다 한다.
        // 쌓이는 C 는 완드로 넘어가는 순간 IotPlayerController 가 버린다.
        if (!IotPlayerController.IsWandLive(_controller)) return;

        // 선택지 창의 버튼을 누르는 중이다. 눌림은 IotUiNavigator 가 가져간다.
        if (IotUiNavigator.IsScreenOpen()) return;

        IHandDevice left = _controller.Left;

        // 낚시 중에는 상호작용 버튼이 놀아야 한다. 낚시터 앞에 선 채로 낚시를 하고 있으므로
        // 그대로 두면 낚시 도중에 같은 낚시터에 또 걸거나 옆의 포탈로 들어가 버린다.
        if (fishingBridge != null && fishingBridge.IsFishing)
        {
            left.ConsumeButton1Press();
            left.ConsumeButton2Press();
            return;
        }

        bool interact = left.ConsumeButton1Press();
        bool dance = left.ConsumeButton2Press();

        if (ChatFocus.Typing) return;

        // 튜토리얼이 떠 있는 동안 왼손 버튼 2 는 [건너뛰기] 다. 선택지 창의 "버튼 2 = 닫기" 와 같은 뜻이다.
        // 키보드 Q 로 이미 열어 둔 휠이 있으면 그 확정이 먼저다.
        if (dance && !DanceWheelView.IsOpen && LobbyTutorial.TrySkipFromDevice()) dance = false;

        UpdateDanceWheel(dance);

        // 휠이 열린 동안에는 상호작용하지 않는다. 휠 위에 인원 선택 창이 겹쳐 뜬다.
        if (!interact || DanceWheelView.IsOpen) return;

        if (fishingBridge != null && fishingBridge.TryBeginFishing()) return;
        if (TryInteractNearest()) return;

        // 눌렀는데 아무 일도 없었다. 포탈 앞에서 이 줄이 보이면 거리 판정(PlayerIsNear)이 거짓인 것이다.
        Debug.Log("[IotLobbyInteract] 왼손 버튼 1 — 가까이 있는 낚시터 · 포탈 · 이정표 · 제단이 없습니다.", this);
    }

    /// <summary>
    /// 왼손 버튼 2 로 열고 · 확정하고, 열려 있는 동안 오른손 스틱으로 칸을 가리킨다.
    /// </summary>
    private void UpdateDanceWheel(bool pressed)
    {
        if (pressed)
        {
            // 누른 그 프레임에만 찾는다. 매 프레임 찾으면 비싸다.
            if (_danceWheel == null) _danceWheel = FindAnyObjectByType<DanceWheelView>();

            if (_danceWheel != null)
            {
                if (DanceWheelView.IsOpen) _danceWheel.ConfirmFromDevice();
                else _danceWheel.OpenFromDevice();
            }
        }

        if (DanceWheelView.IsOpen && _danceWheel != null)
        {
            // Look 은 오른손 스틱이다. 1대면 0 이라 아무 칸도 안 가리킨다.
            _danceWheel.PointFromDevice(_controller.Look);
        }
    }

    /// <summary>
    /// 포탈 · 이정표 · 제단 가운데 **들어갈 수 있는 것**을 가까운 순으로 눌러 본다.
    ///
    /// 들어갈 수 있는지는 각자가 스스로 재고 있으므로(<c>PlayerIsNear</c>) 그 답을 그대로 쓴다.
    /// 여기서 또 재면 F 로는 들어가는데 완드로는 안 되는 자리가 생긴다. 여기서 재는 거리는
    /// **순서를 정하는 데만** 쓴다.
    ///
    /// 가장 가까운 것이 거절하면(포탈이 이미 여는 중 등) 다음 것을 누른다.
    ///
    /// ⚠ 버튼을 누른 그 프레임에만 씬을 뒤진다. 매 프레임 찾으면 비싸다.
    /// </summary>
    private bool TryInteractNearest()
    {
        Transform me = LocalPlayer.Transform;
        if (me == null) return false;

        _nearby.Clear();

        foreach (MiniGamePortal portal in FindObjectsByType<MiniGamePortal>())
        {
            if (portal != null && portal.PlayerIsNear) Add(me, portal);
        }

        foreach (SignpostTeleport post in SignpostTeleport.Registry)
        {
            if (post != null && post.PlayerIsNear) Add(me, post);
        }

        AltarInteraction altar = AltarInteraction.Active;
        if (altar != null && altar.PlayerIsNear) Add(me, altar);

        _nearby.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

        for (int i = 0; i < _nearby.Count; i++)
        {
            bool done = _nearby[i].target switch
            {
                MiniGamePortal portal => portal.TryEnterFromDevice(),
                SignpostTeleport post => post.TryInteractFromDevice(),
                AltarInteraction near => near.TryInteractFromDevice(),
                _ => false,
            };

            if (done) return true;
        }

        return false;
    }

    /// <summary>높이는 빼고 잰다. 셋 다 높이를 무시하고 거리를 재는 것과 맞춘다.</summary>
    private void Add(Transform me, Component target)
    {
        Vector3 gap = target.transform.position - me.position;
        gap.y = 0f;
        _nearby.Add((gap.sqrMagnitude, target));
    }
}

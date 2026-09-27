using FishingMiniGame.Runtime;
using UnityEngine;

/// <summary>
/// 완드로 낚시를 하게 만드는 다리.
///
///     오른손 버튼 2   hook · 릴링   (키보드 J 자리)
///     낚시 개시       <see cref="IotLobbyInteract"/> 가 왼손 버튼 1 로 여기를 부른다
///
/// **낚시 폴더의 파일을 하나도 고치지 않습니다.** 낚시 쪽이 열어 둔 공개 입구만 씁니다.
/// <c>FishingSpot</c> 주석이 "입력과 세션 시작은 바깥 통합 코드의 몫" 이라고 적어 둔
/// 그 자리입니다.
///
///     FishingSpot.TryInteract        낚시 개시
///     FishingModeController.Bind     어느 낚시터인지 알려주기
///     FishingGameController
///         .SetInputSource            hook · 릴링을 넣을 구멍
///
/// ⚠ **어셈블리 때문에 여기(Assembly-CSharp)에 있어야 합니다.**
///   낚시 게임플레이는 <c>FishingMiniGame.Runtime</c> asmdef 안에 있고, asmdef 는
///   Assembly-CSharp 을 참조할 수 없습니다. 그래서 <c>IPlayerController</c> 를 아는 코드는
///   낚시 폴더의 asmdef 안에 둘 수가 없습니다. 팀원의 <c>PlayerFishingAdapter</c> 가
///   <c>Fishing/Integration/</c>(asmdef 바깥)에 있는 것도 같은 이유입니다.
///
/// ⚠ **<c>Start</c> 에서 입력원을 꽂습니다.** <c>PlayerFishingAdapter.Awake</c> 가
///   키보드 입력원을 먼저 꽂아 두기 때문에, 그보다 늦게 덮어써야 합니다.
///   Awake 는 전부 Start 보다 먼저 도므로 Start 면 확실합니다.
///
/// ⚠ **덮어써도 키보드는 살아 있습니다.** 입력원이 하나만 꽂히는 자리라, 키보드 입력원을
///   <see cref="WandFishingInputSource"/> 안에 새로 품어서 넣습니다. J 와 오른손 버튼 2 가
///   둘 다 됩니다.
///
/// ⚠ **채팅 잠금을 벗기지 않습니다.** 팀원이 씌워 둔 <c>ChatFocusFishingInputSource</c> 로
///   똑같이 감싸서 넣습니다. 안 감싸면 채팅을 치는 동안 완드로 낚시가 됩니다.
///
/// **진동** — 챔질이 걸린 순간과 타이밍 릴링의 GOOD · PERFECT 에 울립니다. 판정을 여기서 다시
/// 하지 않고, 같은 오브젝트의 <c>FishingV3AudioPresenter</c> 가 효과음을 낼 때 올리는
/// <c>CueRequested</c> 를 받습니다. 소리와 떨림이 같은 순간에 나고, 낚시 판정이 바뀌어도 여기는 안 고칩니다.
///
///     챔질 성공   양손    0.8 · 0.25초   물고기가 문 무게
///     PERFECT    오른손  0.7 · 0.12초   릴을 감는 손
///     GOOD       오른손  0.4 · 0.08초
///     MISS       없음    못 맞힌 것에 상을 주지 않는다
///
/// <c>PlayerFishingAdapter</c> 와 같은 오브젝트에 붙습니다. 프리팹에 올리지 않습니다 —
/// <see cref="IotLobbyInstaller"/> 가 씬이 로드될 때 코드로 붙입니다. (낚시 프리팹은 낚시 담당 것)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerFishingAdapter))]
public sealed class IotFishingBridge : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 게임 내내 하나인 완드(IotPlayerController.Persistent)를 쓴다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private PlayerFishingAdapter fishingAdapter;

    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private FishingModeController modeController;

    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private FishingGameController gameController;

    private IPlayerController _controller;
    private WandFishingInputSource _inputSource;

    /// <summary>챔질 · 판정 효과음을 내는 부품. 그 신호에 맞춰 진동한다. 없으면 진동만 빠진다.</summary>
    private FishingV3AudioPresenter _cues;

    /// <summary>지금 낚시 중인가. 낚시 중이면 상호작용 버튼이 낚시 개시로 가면 안 된다.</summary>
    public bool IsFishing =>
        modeController != null && modeController.State != FishingModeLifecycleState.Inactive;

    /// <summary>완드를 찾아 붙었는가. 못 붙었으면 팀원의 키보드 입력원이 그대로 살아 있다.</summary>
    public bool IsActive => _controller != null && _inputSource != null;

    private void Awake()
    {
        if (fishingAdapter == null) fishingAdapter = GetComponent<PlayerFishingAdapter>();
        if (modeController == null) modeController = GetComponent<FishingModeController>();
        if (gameController == null) gameController = GetComponent<FishingGameController>();
    }

    private void Start()
    {
        ResolveController();

        if (_controller == null)
        {
            // 서버 빌드다. 팀원이 꽂아 둔 키보드 입력원을 그대로 둔다.
            enabled = false;
            return;
        }

        if (gameController == null)
        {
            Debug.LogError($"{nameof(IotFishingBridge)}: FishingGameController 를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        // 팀원이 꽂는 것과 같은 키보드 입력원을 바탕으로 품는다. (PlayerFishingAdapter)
        _inputSource = new WandFishingInputSource(
            _controller, new KeyboardFishingInputSource("local-player"));

        // 채팅 잠금을 그대로 씌운다. 팀원이 건 경계를 벗기지 않는다.
        gameController.SetInputSource(new ChatFocusFishingInputSource(_inputSource));

        _cues = GetComponent<FishingV3AudioPresenter>();
        if (_cues != null)
        {
            _cues.CueRequested += Vibrate;
        }
    }

    private void OnDestroy()
    {
        if (_cues != null)
        {
            _cues.CueRequested -= Vibrate;
        }
    }

    /// <summary>
    /// 챔질 · GOOD · PERFECT 에 완드를 울린다.
    ///
    /// ⚠ 완드가 실제로 붙어 있을 때만. 없으면 컨트롤러가 키보드로 대신 받아 로그만 쌓인다.
    /// </summary>
    private void Vibrate(FishingV3AudioCue cue)
    {
        if (!IotPlayerController.IsWandLive(_controller))
        {
            return;
        }

        switch (cue)
        {
            case FishingV3AudioCue.HookSuccess:
                _controller.VibrateBoth(0.8f, 0.25f);
                break;
            case FishingV3AudioCue.Perfect:
                _controller.Right.Vibrate(0.7f, 0.12f);
                break;
            case FishingV3AudioCue.Good:
                _controller.Right.Vibrate(0.4f, 0.08f);
                break;
        }
    }

    private void Update()
    {
        // hook · 릴링. 눌린 순간을 여기서 잡아 두고 ReadFrame 이 가져간다.
        //
        // ⚠ ConsumeButton2Press 를 쓰지 않는 이유는 WandFishingInputSource.PollHook 주석에 있다.
        _inputSource?.PollHook(IsFishing);
    }

    /// <summary>
    /// 가까운 낚시터에 낚시를 건다. <c>PlayerFishingAdapter</c> 가 C 키로 하는 것과 같은 순서다.
    ///
    /// 어느 낚시터가 가까운지는 그쪽이 매 프레임 재고 있으므로 그 결과를 그대로 받아 쓴다.
    /// 거리 판정을 여기서 또 하면 두 곳이 서로 다른 답을 내게 된다.
    ///
    /// ⚠ **버튼을 여기서 읽지 않는다.** 왼손 버튼 1 은 낚시 · 포탈 · 제단이 함께 쓰는
    ///   자리라 받는 곳이 하나여야 한다. <see cref="IotLobbyInteract"/> 가 그 하나다.
    ///   (IPlayerController 주석 — "한 프레임에 두 곳에서 부르면 한쪽이 놓친다")
    /// </summary>
    /// <returns>실제로 낚시를 걸었으면 true.</returns>
    public bool TryBeginFishing()
    {
        if (IsFishing) return false;
        if (modeController == null || fishingAdapter == null) return false;
        if (ChatFocus.Typing) return false;
        if (!modeController.isActiveAndEnabled) return false;

        FishingSpot spot = fishingAdapter.CurrentFishingSpot;
        GameObject player = fishingAdapter.LocalPlayerGameObject;

        if (spot == null || player == null || !spot.CanInteract) return false;

        modeController.Bind(spot);
        return spot.TryInteract(player);
    }

    private void ResolveController()
    {
        // ⚠ **KeyboardPlayerController 를 꽂지 않는다.** 키보드는 안에 품은
        //   KeyboardFishingInputSource 가 이미 J 로 받는다. 둘 다 끼우면 키 한 번에 두 번 챔질한다.
        //   IotPlayerController 가 완드 없이 키보드로 대신 채울 때는 PollHook 이 읽지 않는다.
        _controller = playerControllerSource as IPlayerController
                      ?? IotPlayerController.Persistent;
    }
}

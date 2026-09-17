using MiniGames.Common;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lobby 의 미니게임 입구. **가까이 가서 누르면 그 게임으로 들어간다.**
///
/// <code>
///   가까이 가면   안내가 뜬다
///   누르면        공통 로딩 화면 → MiniGameTransition.Enter(씬 이름)
///   멀어지면      안내가 사라지고 눌러도 아무 일 없다
/// </code>
///
/// <b>어느 미니게임인지는 <see cref="config"/> 하나로 정해진다.</b> 씬 이름도 표시 이름도
/// 거기서 온다. 그래서 이 부품에는 ShipCoop · 광산 · 검 같은 이름이 하나도 없고,
/// 포탈을 하나 더 놓고 설정만 바꾸면 다른 게임의 입구가 된다.
///
/// <b>보이는 것은 이 부품이 만들지 않는다.</b> 같은 오브젝트에 붙은 <c>ProximityPortal</c> 이
/// 거리에 따라 문을 열고 닫는다. 둘은 <b>같은 자리에 있을 뿐 서로를 부르지 않는다.</b>
/// <code>
///   ProximityPortal   6m 에서 문이 열린다      — 보이기만 한다
///   MiniGamePortal    4m 부터 F 로 들어간다    — 입력과 입장만 한다
/// </code>
/// 문이 먼저 열리고 더 다가가야 들어갈 수 있다. 일부러 그렇게 벌려 둔 거리다.
///
/// ⚠ <b>Lobby 에는 공통 상호작용 규약이 없다.</b> 확인해 보니 <c>ProximityPortal</c> 은
///    거리에 따라 연출만 바꾸고 입력을 읽지 않으며, <c>TaskBase</c> 계열은 ShipCoop 안쪽
///    규약이라 <c>IPlayerController</c>(IoT) 에 묶여 있다. 그래서 여기서는 Lobby 이동이
///    이미 쓰고 있는 <b>새 Input System</b> 으로 키 하나만 읽는다. Lobby 이동은 WASD 만
///    쓰므로 <see cref="interactKey"/> 기본값 F 와 겹치지 않는다.
///
/// ⚠ <b>이번 단계에서는 매칭 판을 띄우지 않는다.</b> 인원을 채워 줄 대기열 서비스가 없어
///    "0 / 4명" 이 영원히 멈춰 있게 된다. 진짜 인원은 ShipCoop 서버가 자기 화면에
///    <c>"2명을 기다리는 중 (1/2)"</c> 으로 보여 준다. 그것이 유일한 대기 표시다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MiniGamePortal : MonoBehaviour
{
    [Header("어느 미니게임인가")]
    [Tooltip("씬 이름과 표시 이름이 여기서 온다. 포탈을 하나 더 놓고 이것만 바꾸면 다른 게임이 된다.")]
    [SerializeField] private MiniGameConfig config;

    [Header("가까워야 들어갈 수 있다")]
    [Tooltip("이 거리 안에서만 누를 수 있다(m).")]
    [SerializeField, Min(0.5f)] private float interactDistance = 4f;

    [Tooltip("높이 차이는 보지 않는다. 배 위나 계단에서도 같은 거리로 재려면 켠다.")]
    [SerializeField] private bool ignoreHeight = true;

    [Header("입력")]
    [Tooltip("누를 키. Lobby 이동은 WASD 만 쓰므로 F 와 겹치지 않는다.")]
    [SerializeField] private Key interactKey = Key.F;

    [Header("안내")]
    [Tooltip("가까이 갔을 때 켤 것. 비워 둬도 동작한다.")]
    [SerializeField] private GameObject promptRoot;

    private bool shownNear;
    private bool entering;

    /// <summary>지금 들어갈 수 있는가. 안내를 켜고 끄는 기준이기도 하다.</summary>
    public bool PlayerIsNear { get; private set; }

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError($"[MiniGamePortal] '{name}' 에 MiniGameConfig 가 없습니다. 들어갈 수 없습니다.", this);
        }

        ShowPrompt(false);
    }

    private void Update()
    {
        PlayerIsNear = ResolveDistance(out float distance) && distance <= interactDistance;

        if (PlayerIsNear != shownNear)
        {
            shownNear = PlayerIsNear;
            ShowPrompt(PlayerIsNear);
        }

        if (!PlayerIsNear || entering) return;

        Keyboard keys = Keyboard.current;
        if (keys == null || !keys[interactKey].wasPressedThisFrame) return;

        Enter();
    }

    /// <summary>
    /// 내 캐릭터까지의 거리.
    ///
    /// Lobby 의 내 캐릭터는 네트워크가 스폰하므로 씬에 미리 꽂아 둘 수 없다.
    /// <see cref="LocalPlayer"/> 등록소가 스폰된 순간 그 캐릭터를 들고 있으므로 거기서 받는다.
    /// 아직 접속 중이면 null 이고, 전용 서버에는 아예 없다 — 둘 다 "멀다" 로 친다.
    ///
    /// ⚠ <b>일부러 같은 등록소를 쓴다.</b> 같은 오브젝트에 붙어 있는
    ///    <c>ProximityPortal</c> 도 이 등록소를 본다. 한쪽만 다른 방법으로 캐릭터를 찾으면
    ///    <b>문이 열려 보이는데 F 가 안 먹거나 그 반대</b>가 된다. 재는 대상이 하나여야
    ///    연출 거리(6m)와 입장 거리(4m)의 관계가 화면에서 그대로 보인다.
    /// </summary>
    private bool ResolveDistance(out float distance)
    {
        distance = float.MaxValue;

        Transform local = LocalPlayer.Transform;
        if (local == null) return false;

        Vector3 gap = local.position - transform.position;
        if (ignoreHeight) gap.y = 0f;

        distance = gap.magnitude;
        return true;
    }

    /// <summary>
    /// **미니게임으로 들어간다.**
    ///
    /// 로딩 화면을 먼저 띄우고 전환을 시작한다. 전환은 Lobby Runner 를 끄고 씬을 여는
    /// 순서를 지키며, 실패하면 스스로 Lobby 로 되돌린다.
    /// </summary>
    private void Enter()
    {
        if (config == null) return;

        entering = true;
        ShowPrompt(false);

        Debug.Log($"[MiniGamePortal] '{config.DisplayName}' 으로 들어갑니다. (씬 {config.SceneName})");

        // 매칭 판은 띄우지 않는다. 로딩 화면만 보여 준다.
        if (CommonMatchingUI.Current != null)
        {
            CommonMatchingUI.Current.ShowQueueLoading(config);
        }

        // 세션은 넘기지 않는다 — 비워 두면 미니게임의 기본 세션으로 간다.
        // 진짜 매칭이 붙기 전까지는 고정 세션이고, 그 사실을 감추지 않는다.
        MiniGameTransition.Enter(config.SceneName, null, OnEnterFailed);
    }

    private void OnEnterFailed(string reason)
    {
        Debug.LogError($"[MiniGamePortal] 입장 실패 — {reason}");

        entering = false;

        // 전환이 Lobby 로 되돌린다. 이쪽은 화면만 치운다.
        if (CommonMatchingUI.Current != null) CommonMatchingUI.Current.Hide();
    }

    private void ShowPrompt(bool on)
    {
        if (promptRoot != null && promptRoot.activeSelf != on) promptRoot.SetActive(on);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}

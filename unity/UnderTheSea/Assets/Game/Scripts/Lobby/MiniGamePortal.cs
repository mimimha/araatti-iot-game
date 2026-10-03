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

    [Header("돌아올 때")]
    [Tooltip("이 게임을 마치고 로비로 돌아온 사람이 설 자리. 파란 화살표(앞)가 포탈 반대쪽을 보게 둔다 — " +
             "포탈을 등지고 나온 모습이 된다.\n\n" +
             "비워 두면 처음 로그인할 때와 같은 기본 자리에 선다.\n" +
             "메뉴: Tools > 아라아띠 > 포탈 앞 돌아오는 자리 만들기")]
    [SerializeField] private Transform returnPoint;

    private bool shownNear;
    private bool entering;

    /// <summary>지금 들어갈 수 있는가. 안내를 켜고 끄는 기준이기도 하다.</summary>
    public bool PlayerIsNear { get; private set; }

    /// <summary>이 포탈이 여는 미니게임의 씬 이름. 설정이 없으면 null.</summary>
    public string SceneName => config != null ? config.SceneName : null;

    /// <summary>돌아온 사람이 설 자리. 비어 있을 수 있다.</summary>
    public Transform ReturnPoint => returnPoint;

    /// <summary>
    /// **그 미니게임에서 돌아온 사람을 어디에 세울까.** 로비 서버의 <c>PlayerSpawner</c> 가 부른다.
    ///
    /// 씬에 놓인 포탈 가운데 <paramref name="sceneName"/> 으로 가는 것을 찾아 그 돌아오는 자리를
    /// 준다. 없으면 false — 부르는 쪽이 기본 자리를 쓴다.
    ///
    /// ⚠ <b>회전은 좌우(Y)만 쓴다.</b> 표식을 조금 기울여 놓았어도 캐릭터가 비스듬히 서면 안 된다.
    /// ⚠ <b>꺼진 포탈도 본다.</b> 서버는 화면이 없어 포탈 연출을 꺼 둘 수 있다. 자리 정보는 그대로다.
    /// </summary>
    public static bool TryFindReturnPoint(
        string sceneName, out Vector3 position, out Quaternion rotation, out string portalName)
    {
        position = default;
        rotation = Quaternion.identity;
        portalName = null;

        if (string.IsNullOrEmpty(sceneName)) return false;

        foreach (MiniGamePortal portal in FindObjectsByType<MiniGamePortal>(FindObjectsInactive.Include))
        {
            if (portal == null || portal.returnPoint == null) continue;
            if (!string.Equals(portal.SceneName, sceneName, System.StringComparison.Ordinal)) continue;

            position = portal.returnPoint.position;
            rotation = Quaternion.Euler(0f, portal.returnPoint.eulerAngles.y, 0f);
            portalName = portal.name;
            return true;
        }

        return false;
    }

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

        // 제단 창이 열려 있거나 채팅을 치는 중이면 들어가지 않는다.
        // 그 화면들이 이동을 막을 때 거는 잠금을 그대로 본다.
        if (ChatFocus.Typing) return;

        Keyboard keys = Keyboard.current;
        if (keys == null || !keys[interactKey].wasPressedThisFrame) return;

        Enter();
    }

    /// <summary>
    /// 키가 아니라 <b>밖에서</b> 들어가라고 할 때. 완드가 이 길로 들어온다.
    ///
    /// 위 <c>Update</c> 의 F 와 **같은 조건을 그대로 본다.** 가까이 있어야 하고, 이미
    /// 들어가는 중이면 안 되고, 채팅을 치는 중이면 안 된다. 조건을 여기서 느슨하게 하면
    /// 키로는 못 들어가는 자리에 완드로는 들어가진다.
    ///
    /// 완드가 <c>IPlayerController</c> 로만 들어오고 Input System 을 통하지 않으므로
    /// 키를 흉내내는 대신 이렇게 연다. (IOT_INPUT.md 7장 "키 이벤트를 만들어 보내기" 금지)
    /// </summary>
    /// <returns>실제로 들어갔으면 true.</returns>
    public bool TryEnterFromDevice()
    {
        if (!PlayerIsNear || entering) return false;
        if (ChatFocus.Typing) return false;

        Enter();
        return true;
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

        Debug.Log($"[MiniGamePortal] '{config.DisplayName}' 매칭을 엽니다.");

        // 곧바로 들어가지 않는다. 몇 명이서 할지 먼저 고르고, 로비 서버가 같은 인원을
        // 고른 사람들과 묶어 방을 정해 준 뒤에 떠난다.
        MatchScreenFlow.Begin(config);
    }

    /// <summary>
    /// 매칭 화면이 닫혔으면 다시 누를 수 있게 한다.
    ///
    /// ⚠ <b>없으면 포탈이 한 번 쓰고 죽는다.</b> 매칭을 취소한 사람은 포탈 앞에 그대로
    ///    서 있다. <c>entering</c> 이 켜진 채로 남으면 F 를 눌러도 아무 일이 없다.
    /// </summary>
    private void LateUpdate()
    {
        if (!entering) return;
        if (MiniGameTransition.InMiniGame) return;
        if (CommonMatchingUI.Current != null && CommonMatchingUI.Current.IsShown) return;

        entering = false;
    }

    private void ShowPrompt(bool on)
    {
        if (promptRoot != null && promptRoot.activeSelf != on) promptRoot.SetActive(on);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);

        // 돌아오는 자리와 바라보는 쪽. 화살표가 포탈 반대쪽을 봐야 한다.
        if (returnPoint != null)
        {
            Vector3 at = returnPoint.position;
            Vector3 forward = Quaternion.Euler(0f, returnPoint.eulerAngles.y, 0f) * Vector3.forward;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(at, 0.5f);
            Gizmos.DrawLine(at, at + forward * 2f);
            Gizmos.DrawLine(at + forward * 2f, at + forward * 1.5f + Vector3.Cross(Vector3.up, forward) * 0.4f);
            Gizmos.DrawLine(at + forward * 2f, at + forward * 1.5f - Vector3.Cross(Vector3.up, forward) * 0.4f);
        }
    }
}

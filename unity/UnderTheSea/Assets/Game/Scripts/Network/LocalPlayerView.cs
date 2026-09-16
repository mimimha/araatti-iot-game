using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnderTheSea.Network;
using ithappy.Cute_Characters.Controller;

/// <summary>
/// 내 캐릭터에만 붙는 로컬 연출. **씬에 이미 있는 카메라를 재사용한다.**
///
/// <b>왜 캐릭터 프리팹에 카메라를 넣지 않는가.</b>
/// 프리팹에 카메라를 넣으면 접속자 수만큼 카메라와 AudioListener 가 생긴다.
/// Unity 는 AudioListener 가 둘 이상이면 경고를 내고, 화면도 누구 것을 그릴지 알 수 없다.
/// Lobby 씬에는 이미 <c>MainCamera</c>(ThirdPersonCamera + AudioListener)가 하나 있으므로
/// 그것을 **내 캐릭터로 다시 겨누기만** 한다.
///
/// 이 컴포넌트는 <see cref="NetworkBehaviour.HasInputAuthority"/> 인 피어에서만 동작한다.
/// 서버와 남의 캐릭터에서는 <see cref="Spawned"/> 가 바로 빠져나가므로
/// 카메라 · 마우스 입력이 생기지 않는다.
///
/// 하는 일은 두 가지다.
///   1. 내 캐릭터를 <see cref="LocalPlayer"/> 에 등록한다 (포털 같은 로컬 연출이 이걸 본다)
///   2. 화면 카메라를 내 캐릭터에 붙이고, 화면에 그려지는 카메라를 하나로 만든다
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
public class LocalPlayerView : NetworkBehaviour
{
    [Header("마우스")]
    [SerializeField] private string mouseX = "Mouse X";
    [SerializeField] private string mouseY = "Mouse Y";
    [SerializeField] private string mouseScroll = "Mouse ScrollWheel";

    /// <summary>내가 조작하는 카메라. 남의 캐릭터·서버에서는 null 로 남는다.</summary>
    private PlayerCamera boundCamera;

    /// <summary>
    /// 이 인스턴스가 <b>내 캐릭터</b>로 확정됐는가.
    ///
    /// ⚠ <see cref="Despawned"/> 는 남의 캐릭터가 사라질 때도 그 오브젝트에서 불린다.
    ///    거기서 화면을 가려 버리면 <b>옆 사람이 나갔는데 내 화면이 검게 덮인다.</b>
    ///    실제로 그랬다 — 클라이언트 하나를 닫으니 남은 클라이언트에
    ///    "연결이 끊어졌습니다" 가 떴다.
    ///    Despawned 시점의 HasInputAuthority 를 다시 믿는 대신,
    ///    Spawned 에서 내 것으로 확정한 사실을 그대로 들고 간다.
    /// </summary>
    private bool isLocalPlayer;

    /// <summary>카메라를 내 캐릭터에 붙였는가.</summary>
    private bool cameraReady;

    /// <summary>서버가 복제한 외형을 입혔는가. (PRD 09-2)</summary>
    private bool appearanceReady;

    /// <summary>-logmoves 로 켜는 진단 로그.</summary>
    private static readonly bool LogCamera = FusionLaunchArguments.HasFlag(FusionLaunchArguments.LogMovesKey);

    private float nextCameraLogTime;

    public override void Spawned()
    {
        // 내 캐릭터가 아니면 아무것도 하지 않는다. 서버와 남의 캐릭터가 여기서 걸러진다.
        if (!HasInputAuthority)
        {
            return;
        }

        isLocalPlayer = true;

        // 포털 등 로컬 연출이 "내 캐릭터" 를 찾을 수 있게 먼저 알린다.
        // 카메라보다 먼저 하는 이유: 카메라 쪽에서 문제가 나도 포털은 살아 있게 하려고.
        LocalPlayer.Register(Object);

        // 표식이 붙은 게임플레이 카메라만 쓴다. 씬 이름이나 탐색 순서에 기대지 않는다.
        LobbyGameplayCamera marked = LobbyGameplayCamera.ResolveFor(transform);

        if (marked == null || marked.Follower == null)
        {
            // ResolveFor 가 이미 이유를 남겼다. 아무 카메라나 대신 집지 않는다.
            Debug.LogError(
                "[LocalPlayerView] 쓸 수 있는 게임플레이 카메라가 없어 화면을 붙이지 못했습니다.");

            TransitionStatus.SetFailed("게임플레이 카메라를 찾지 못했습니다.");
            return;
        }

        boundCamera = marked.Follower;

        KeepOnlyThisViewer(marked.Camera);
        boundCamera.BindPlayer(transform);

        // ⚠ 붙이자마자 제자리로 보낸다. 이 한 줄이 빠지면 카메라가 원래 있던 곳에서
        //    초당 90 유닛으로 날아오는 것이 화면에 그대로 보인다.
        //    (ThirdPersonCamera 는 대상이 없는 동안 움직이지 않도록 고쳐 두었다)
        if (boundCamera is ThirdPersonCamera thirdPerson)
        {
            thirdPerson.SnapToPlayer();
        }

        Debug.Log(
            $"[LocalPlayerView] 카메라 '{boundCamera.name}'(씬 '{boundCamera.gameObject.scene.name}')를 " +
            $"내 캐릭터({Object.InputAuthority})에 연결했습니다. " +
            $"카메라 위치 {boundCamera.transform.position.ToString("F2")}, " +
            $"캐릭터까지 {Vector3.Distance(boundCamera.transform.position, transform.position):F2}m");

        // 카메라 쪽 준비가 끝났다.
        cameraReady = true;

        // 외형 복제 컴포넌트가 없으면 기다릴 것이 없다. (09-2 이전 프리팹 호환)
        if (GetComponent<NetworkPlayerAppearance>() == null)
        {
            appearanceReady = true;
        }

        TryFinishLoading();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 남의 캐릭터가 사라진 것은 내 화면·카메라와 아무 상관이 없다.
        // 이 가드가 없으면 옆 사람이 나갈 때 내 화면이 가려진다.
        if (!isLocalPlayer)
        {
            return;
        }

        // 내가 나갈 때 오래된 참조를 남기지 않는다.
        LocalPlayer.Unregister(Object);

        // 내 캐릭터가 사라졌으면 더 이상 보여 줄 화면이 아니다.
        // 이미 실패로 표시돼 있으면(접속 종료 등) 그 사유를 덮어쓰지 않는다.
        if (TransitionStatus.Current == TransitionStatus.Phase.Ready)
        {
            TransitionStatus.SetFailed("연결이 끊어졌습니다.");
        }

        if (boundCamera != null)
        {
            // 사라진 캐릭터를 계속 따라가지 않게 풀어 준다.
            boundCamera.BindPlayer(null);
            boundCamera = null;
        }
    }

    /// <summary>
    /// 서버가 복제한 외형을 입혔다고 <see cref="NetworkPlayerAppearance"/> 가 알려 준다.
    ///
    /// 빈 외형(개발용 직접 진입)이어도 불린다. 그래야 Overlay 가 닫힌다.
    /// </summary>
    public void NotifyAppearanceReady()
    {
        appearanceReady = true;
        TryFinishLoading();
    }

    /// <summary>
    /// "화면을 사용자에게 넘겨도 되는" 순간인지 보고, 맞으면 가림막을 걷는다.
    ///
    /// 세 가지가 다 끝나야 한다. 접속 성공만으로는 부르지 않는다.
    ///   · 내 캐릭터가 있다        (LocalPlayer.Register 완료)
    ///   · 카메라가 자리를 잡았다   (BindPlayer + SnapToPlayer 완료)
    ///   · 외형이 입혀졌다          (서버가 AppearanceReady 를 세운 뒤)
    ///
    /// 외형을 기다리지 않으면 기본 옷을 입은 내 캐릭터가 한순간 보였다가 바뀐다.
    /// </summary>
    private void TryFinishLoading()
    {
        if (!cameraReady || !appearanceReady)
        {
            return;
        }

        TransitionStatus.SetReady();
    }

    /// <summary>
    /// 화면에 그려지는 카메라를 하나로 만든다.
    ///
    /// 위에서 설명한 이유로 카메라가 둘 남으면 둘 다 같은 depth 로 화면에 그려져
    /// 엉뚱한 시점이 덮어써진다. AudioListener 도 둘이 되어 경고가 난다.
    /// RenderTexture 로 그리는 카메라(반사 · 프리뷰)는 화면을 건드리지 않으므로 놔둔다.
    /// </summary>
    private static void KeepOnlyThisViewer(Camera keep)
    {
        if (keep == null)
        {
            return;
        }

        foreach (Camera other in FindObjectsByType<Camera>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == keep || other.targetTexture != null)
            {
                continue;
            }

            other.enabled = false;

            AudioListener listener = other.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }

            Debug.Log(
                $"[LocalPlayerView] 화면에 겹쳐 그려지던 카메라 '{other.name}'" +
                $"(씬 '{other.gameObject.scene.name}')를 껐습니다.");
        }

        // 남길 카메라는 확실히 켜 둔다. 이전 실행에서 꺼 둔 채로 남을 수 있다.
        keep.enabled = true;

        AudioListener keepListener = keep.GetComponent<AudioListener>();
        if (keepListener != null)
        {
            keepListener.enabled = true;
        }
    }

    private void Update()
    {
        if (boundCamera == null)
        {
            return;
        }

        // ThirdPersonCamera 는 SetInput 을 누가 매 프레임 불러 줘야 움직인다.
        // 원래는 MovePlayerInput 이 했지만 NetworkPlayer 에는 그 컴포넌트가 없다.
        //
        // ⚠ **오른쪽 버튼을 누르고 있는 동안에만 돈다.** (IOT_INPUT.md 1장 — 네 게임 공통)
        //    예전에는 마우스를 움직이기만 해도 시점이 따라 돌았다. 그러면 채팅창이나
        //    버튼을 누르려고 커서를 옮기는 동안에도 화면이 돌아가 버린다.
        //    좌클릭은 그 화면 요소들이 써야 하므로 우클릭으로 잡는다.
        bool dragging = Input.GetMouseButton(1);

        Vector2 delta = dragging
            ? new Vector2(Input.GetAxis(mouseX), Input.GetAxis(mouseY))
            : Vector2.zero;

        // 확대·축소는 버튼과 무관하다. 휠은 언제나 그대로 넘긴다.
        boundCamera.SetInput(in delta, Input.GetAxis(mouseScroll));

        if (LogCamera && Time.time >= nextCameraLogTime)
        {
            nextCameraLogTime = Time.time + 1f;

            Transform cam = boundCamera.transform;
            Transform tracked = boundCamera.Player;

            Debug.Log(
                $"[Cam] 카메라 {cam.position.ToString("F2")} / 내 캐릭터 {transform.position.ToString("F2")} / " +
                $"추적대상 {(tracked == null ? "null" : tracked.name + tracked.position.ToString("F2"))} / " +
                $"거리 {Vector3.Distance(cam.position, transform.position):F2}");
        }
    }
}

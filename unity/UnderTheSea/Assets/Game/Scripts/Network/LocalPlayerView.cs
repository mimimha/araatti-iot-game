using System.Collections;
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
    [Header("복귀 직후 안정화")]
    [Tooltip("시뮬레이션 속도를 이 길이만큼씩 재서 정상인지 본다. (초)")]
    [SerializeField, Min(0.1f)] private float settleWindow = 0.5f;

    [Tooltip("몇 번 연속 정상이어야 화면을 넘길지. 한 번만 보면 다시 흔들릴 수 있다.")]
    [SerializeField, Min(1)] private int settleStreak = 2;

    [Tooltip("이 시간을 넘으면 더 기다리지 않고 넘긴다. 무한 로딩을 막는 상한이다. (초)")]
    [SerializeField, Min(1f)] private float settleTimeout = 5f;

    [Tooltip("스폰 뒤 이 시간까지도 조건이 안 갖춰지면 그냥 넘긴다. 마지막 안전장치다. (초)")]
    [SerializeField, Min(3f)] private float giveUpAfter = 15f;

    [Header("마우스")]
    [SerializeField] private string mouseX = "Mouse X";
    [SerializeField] private string mouseY = "Mouse Y";
    [SerializeField] private string mouseScroll = "Mouse ScrollWheel";

    [Header("IoT")]
    [Tooltip("IPlayerController 를 구현한 컴포넌트. 비워두면 게임 내내 하나인 완드(IotPlayerController.Persistent)를 쓴다.")]
    [SerializeField] private MonoBehaviour playerControllerSource;

    [Tooltip("완드 오른손 스틱을 끝까지 밀었을 때 초당 도는 양. 마우스 감도와 따로 맞춰야 한다.")]
    [SerializeField, Min(1f)] private float wandLookSpeed = 120f;

    /// <summary>내가 조작하는 카메라. 남의 캐릭터·서버에서는 null 로 남는다.</summary>
    private PlayerCamera boundCamera;

    /// <summary>완드. 서버 빌드에서는 null 이고, 그때는 예전처럼 마우스만 읽는다.</summary>
    private IPlayerController wand;

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
            // 캐릭터가 보는 방향 뒤에 선다. 미니게임에서 돌아와 포탈을 등지고 섰을 때
            // 포탈에서 걸어 나온 모습이 된다. 처음 로그인 자리는 모두 북쪽을 보므로 예전과 같다.
            thirdPerson.FaceYaw(transform.eulerAngles.y);
            thirdPerson.SnapToPlayer();
        }

        Debug.Log(
            $"[LocalPlayerView] 카메라 '{boundCamera.name}'(씬 '{boundCamera.gameObject.scene.name}')를 " +
            $"내 캐릭터({Object.InputAuthority})에 연결했습니다. " +
            $"카메라 위치 {boundCamera.transform.position.ToString("F2")}, " +
            $"캐릭터까지 {Vector3.Distance(boundCamera.transform.position, transform.position):F2}m");

        // 카메라 쪽 준비가 끝났다.
        cameraReady = true;

        // 여기서부터 시간을 센다. 이 지점이 "내 캐릭터가 확정된" 가장 이른 순간이다.
        StartCoroutine(GiveUpIfNeverReady());

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
    /// **카메라를 그 자리에 얼리거나 다시 풀어 준다.** 순간이동 연출이 쓴다.
    ///
    /// <b>왜 필요한가.</b> 이정표로 옮길 때 캐릭터는 한 틱 만에 도착하지만 카메라는
    /// 초당 90 유닛으로 뒤따라간다(<c>ThirdPersonCamera.m_CameraSpeed</c>). 화면을
    /// 가려도 <b>어두워지는 동안</b> 카메라가 이미 날아가기 시작하므로 그 움직임이
    /// 보인다. 어두워지기 시작할 때 얼려 두면 없어진다.
    ///
    /// <c>enabled</c> 를 끄면 <c>LateUpdate</c> 가 멈춰 카메라가 제자리에 선다.
    /// <c>SetInput</c> 과 <see cref="SnapCameraToMe"/> 는 꺼져 있어도 부를 수 있다.
    /// </summary>
    public void FreezeCamera(bool frozen)
    {
        if (boundCamera != null)
        {
            boundCamera.enabled = !frozen;
        }
    }

    /// <summary>
    /// **카메라를 내 캐릭터 뒤 제자리로 즉시 옮긴다.** 보간하지 않는다.
    ///
    /// 화면이 까만 동안 불러야 뜻이 있다. 밝은 동안 부르면 화면이 툭 끊긴다.
    /// </summary>
    public void SnapCameraToMe()
    {
        if (boundCamera is ThirdPersonCamera thirdPerson)
        {
            thirdPerson.SnapToPlayer();
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

    /// <summary>화면을 이미 넘겼는가. 두 번 넘기지 않기 위한 표시다.</summary>
    private bool handedOver;

    /// <summary>안정화를 기다리는 중인 코루틴. 둘이 같이 돌지 않게 붙잡아 둔다.</summary>
    private Coroutine settling;

    /// <summary>
    /// "화면을 사용자에게 넘겨도 되는" 순간인지 보고, 맞으면 가림막을 걷는다.
    ///
    /// 네 가지가 다 끝나야 한다. 접속 성공만으로는 넘기지 않는다.
    ///   · 내 캐릭터가 있다        (LocalPlayer.Register 완료)
    ///   · 카메라가 자리를 잡았다   (BindPlayer + SnapToPlayer 완료)
    ///   · 외형이 입혀졌다          (서버가 AppearanceReady 를 세운 뒤)
    ///   · 시뮬레이션이 제 속도다   (<see cref="WaitUntilSimulationSettles"/>)
    ///
    /// 외형을 기다리지 않으면 기본 옷을 입은 내 캐릭터가 한순간 보였다가 바뀐다.
    /// 네 번째를 기다리는 이유는 아래에 적었다.
    /// </summary>
    private void TryFinishLoading()
    {
        if (handedOver || settling != null) return;
        if (!cameraReady || !appearanceReady) return;

        settling = StartCoroutine(WaitUntilSimulationSettles());
    }

    /// <summary>
    /// **어떤 조건이 영영 안 와도 결국은 화면을 넘긴다.** 마지막 안전장치다.
    ///
    /// <see cref="WaitUntilSimulationSettles"/> 안에도 상한이 있지만 그것만으로는 모자랐다.
    /// 그 상한은 <b>네 조건이 다 갖춰져 코루틴이 시작된 뒤에만</b> 돈다. 조건 하나가 아예
    /// 안 오면 코루틴이 시작조차 안 하므로 아무도 세지 않는다.
    ///
    /// 실제로 겪었다. 서버가 외형을 기록했는데 클라이언트가 그 변화를 놓쳐
    /// <c>appearanceReady</c> 가 끝내 켜지지 않았고, "서버 1에 접속 중..." 이 2분 넘게 떠
    /// 있었다. 캐릭터는 로비에 멀쩡히 있었고 옆 사람 화면에서는 움직이기까지 했다.
    ///
    /// 놓친 쪽(외형)은 <c>NetworkPlayerAppearance</c> 에서 따로 고쳤다. 다만 <b>원인이 무엇이든
    /// 사람이 갇히는 일은 없어야 하므로</b> 여기에도 시간 상한을 둔다. 원인을 가리기 위해
    /// 무엇이 안 왔는지는 경고에 적는다.
    /// </summary>
    private IEnumerator GiveUpIfNeverReady()
    {
        float waited = 0f;

        while (!handedOver && waited < giveUpAfter)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (handedOver) yield break;

        Debug.LogWarning(
            $"[LocalPlayerView] {giveUpAfter:0}초가 지나도 준비가 끝나지 않아 화면을 넘깁니다. " +
            $"(카메라 {cameraReady}, 외형 {appearanceReady}) " +
            "로딩 화면에 갇히는 것보다 낫습니다. 이 경고가 보이면 원인을 찾아야 합니다.");

        if (settling != null)
        {
            StopCoroutine(settling);
        }

        HandOver();
    }

    /// <summary>
    /// **밀린 틱을 다 따라잡은 뒤에** 화면을 넘긴다.
    ///
    /// <b>왜 필요한가.</b> 미니게임에서 Lobby 로 돌아올 때 씬을 여느라 한 프레임이
    /// 0.8~1.2초 멈춘다. 그동안 서버는 계속 틱을 돌리므로 클라이언트는 그만큼 뒤진 채로
    /// 깨어나고, 다음 1초 동안 밀린 것을 <b>몰아서</b> 돌려 따라잡는다. 실측으로 한 번은
    /// 1초에 167틱(평소 64틱의 2.6배)이었다. 카메라와 외형만 보고 넘기면 사람은 정확히
    /// 그 구간에서 조작하게 되고, 입력이 밀린다고 느낀다. 로그로 순서를 확인했다.
    ///
    /// <code>
    ///     fps 19 | worst 1202ms | tick  +69 | rtt 624ms   ← 씬 로드로 멈춤
    ///     Ready                                            ← 여기서 넘겼다
    ///     fps 45 | worst   81ms | tick +167 | rtt  34ms   ← 넘긴 뒤에 따라잡는다
    ///     fps 60 | worst   23ms | tick  +64 | rtt  33ms   ← 그 다음 초부터 정상
    /// </code>
    ///
    /// 그래서 <b>따라잡기가 끝난 것을 확인하고</b> 넘긴다.
    ///
    /// ⚠ 기준 tick 은 창을 열 때마다 새로 잡는다. Runner 가 교체되면 tick 번호 기준이
    ///    바뀌므로, 예전 값과 빼면 의미 없는 숫자가 나온다.
    ///
    /// ⚠ 64 를 박지 않고 <c>Runner.DeltaTime</c> 에서 실제 설정값을 얻는다. 틱 수를
    ///    바꾸면 이 판정도 같이 따라가야 한다.
    ///
    /// ⚠ <b>어떤 경우에도 로딩 화면에 가두지 않는다.</b> Runner 가 사라지거나 상한 시간을
    ///    넘기면 경고만 남기고 넘긴다. 조작이 조금 굼뜬 것보다 갇히는 쪽이 훨씬 나쁘다.
    /// </summary>
    private IEnumerator WaitUntilSimulationSettles()
    {
        NetworkRunner runner = Runner;

        float rate = runner != null && runner.IsRunning && runner.DeltaTime > 0f
            ? 1f / runner.DeltaTime
            : 0f;

        if (rate <= 0f)
        {
            // 잴 수 없으면 기다리지 않는다.
            HandOver();
            yield break;
        }

        float waited = 0f;
        int inARow = 0;

        while (inARow < settleStreak)
        {
            int from = runner.Tick.Raw;
            float spent = 0f;

            while (spent < settleWindow)
            {
                yield return null;

                spent += Time.unscaledDeltaTime;
                waited += Time.unscaledDeltaTime;

                if (runner == null || !runner.IsRunning)
                {
                    HandOver();
                    yield break;
                }

                if (waited >= settleTimeout)
                {
                    Debug.LogWarning(
                        $"[LocalPlayerView] 시뮬레이션이 {settleTimeout:0}초 안에 제 속도로 " +
                        "돌아오지 않아 그대로 화면을 넘깁니다. 조작이 잠시 밀릴 수 있습니다.");

                    HandOver();
                    yield break;
                }
            }

            float expected = rate * spent;
            int moved = runner.Tick.Raw - from;

            // 아래로 벗어나면 아직 못 따라온 것이고, 위로 벗어나면 몰아서 돌리는 중이다.
            bool normal = moved >= expected * 0.8f && moved <= expected * 1.25f;
            inARow = normal ? inARow + 1 : 0;
        }

        Debug.Log(
            $"[LocalPlayerView] 시뮬레이션이 제 속도({rate:0}틱/초)로 자리 잡았습니다. " +
            $"{waited:0.00}초 기다렸습니다.");

        HandOver();
    }

    private void HandOver()
    {
        settling = null;
        handedOver = true;
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

        // 상하는 부호를 뒤집어 넘긴다.
        //
        // ThirdPersonCamera 는 delta.y 를 그대로 pitch 에 더하는데, pitch 가 커지면
        // 카메라가 주시점 **위로** 올라가 아래를 내려다본다. 그래서 그대로 넘기면
        // 마우스를 위로 끌 때 화면이 아래를 향한다(비행 시뮬 방식).
        // 끈 쪽을 보게 하려면 여기서 뒤집는 수밖에 없다.
        Vector2 delta = dragging
            ? new Vector2(Input.GetAxis(mouseX), -Input.GetAxis(mouseY))
            : Vector2.zero;

        // 완드의 오른손 스틱도 같은 자리로 더한다. 완드가 안 붙어 있으면 건너뛰어 예전과 같다.
        //
        // ⚠ **완드가 실제로 붙어 있을 때만 더한다.** (IsWandLive) 완드가 없으면
        //   IotPlayerController 가 오른손 스틱을 우클릭 드래그로 대신 채워서, 위 마우스 경로와
        //   같은 드래그가 두 번 들어가 화면이 두 배로 돈다.
        //
        // ⚠ **마우스와 단위가 다르다.** 마우스는 이번 프레임에 움직인 양이 그대로 오지만
        //   스틱은 기울인 채로 −1 ~ 1 에 머물러 있는 값이다. 그대로 넘기면 프레임이 빠른
        //   기계에서만 빨리 돈다. 그래서 초당 각도로 바꿔서 넘긴다.
        //
        // ⚠ 상하 부호는 마우스와 같은 이유로 뒤집는다. (바로 위 주석)
        IPlayerController device = ResolveWand();
        if (IotPlayerController.IsWandLive(device) && !ChatFocus.Typing)
        {
            Vector2 look = device.Look;
            delta += new Vector2(look.x, -look.y) * (wandLookSpeed * Time.deltaTime);
        }

        // 확대·축소는 버튼과 무관하다. 휠은 누르지 않고도 늘 먹는다.
        //
        // ⚠ 다만 **화면이 휠을 쓰고 있으면 비켜 준다.** 채팅 기록 위에서 지난 대화를
        //    올려 읽는 동안 화면까지 줌되면 둘 다 제대로 안 된다.
        //    채팅이 있는지는 여기서 몰라도 된다. ChatFocus 한 곳만 본다.
        float zoom = ChatFocus.WheelHeld ? 0f : Input.GetAxis(mouseScroll);

        boundCamera.SetInput(in delta, zoom);

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

    /// <summary>
    /// 완드를 쥐어 둔다. 서버 빌드에서는 null 이고 카메라는 예전처럼 마우스로만 돈다.
    ///
    /// ⚠ <b>KeyboardPlayerController 를 여기 꽂지 않는다.</b> 그것은 오른손 스틱을
    ///   <b>마우스 우클릭 드래그로</b> 채운다. 같은 드래그가 위의 마우스 경로와
    ///   여기로 두 번 들어가 화면이 두 배로 돈다. 완드 경로를 장치 없이 확인해야 할 때만
    ///   인스펙터에 직접 지정한다.
    /// </summary>
    private IPlayerController ResolveWand()
    {
        if (wand == null)
        {
            wand = playerControllerSource as IPlayerController ?? IotPlayerController.Persistent;
        }

        return wand;
    }
}

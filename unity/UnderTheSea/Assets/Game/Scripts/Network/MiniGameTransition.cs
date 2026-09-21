using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// **Lobby 와 미니게임 사이를 오가는 순서를 지킨다.**
///
/// <code>
///   들어갈 때   채널을 기억한다 → Lobby Runner 종료 → 종료 확인 → 미니게임 씬
///   나올 때     미니게임 Runner 종료 → Lobby 씬 → 기억한 채널로 재접속
/// </code>
///
/// <b>왜 별도 부품이 필요한가.</b> 이 프로세스에는 <c>NetworkRunner</c> 가 <b>한 번에
/// 하나만</b> 있어야 한다. <c>ShipCoopLauncher</c> 는 이미 돌고 있는 Runner 를 발견하면
/// <b>세션을 시작하지 않고 조용히 반환</b>한다. 실패도 오류도 아니라서, Lobby Runner 를
/// 끄지 않고 넘어가면 "아무 일도 안 일어나는" 상태가 된다. 그 순서를 지키는 곳이 여기다.
///
/// <b>미니게임을 알지 못한다.</b> 씬 이름과 세션 이름을 받아서 옮길 뿐이라, 배 · 광산 · 검이
/// 같은 부품을 쓴다. ShipCoop 이라는 이름은 이 파일에 한 번도 나오지 않는다.
///
/// ⚠ 허브의 이름은 어디서나 <b>Lobby</b> 다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MiniGameTransition : MonoBehaviour
{
    /// <summary>
    /// Runner 가 사라지기를 기다리는 한도(초).
    ///
    /// ⚠ <b>무기한 기다리지 않는다.</b> 종료 신호를 놓치면 화면이 영영 멈춘다.
    ///
    /// ⚠ <b>한도를 넘기면 들어가지 않는다.</b> 예전 Runner 가 살아 있는 채로 미니게임 씬을
    ///    열면 런처가 "이미 돌고 있는 Runner 가 있다" 며 <b>조용히 세션을 시작하지 않는다.</b>
    ///    실패도 오류도 아니라서 <c>MiniGameEntry.Failed</c> 조차 오지 않는다 — 복구할 신호가
    ///    없는 채로 빈 시작 씬에 갇힌다. 그래서 타임아웃은 "경고 후 진행" 이 아니라
    ///    <b>진입 중단 + 복구</b> 다.
    /// </summary>
    private const float ShutdownTimeoutSeconds = 5f;

    private static MiniGameTransition instance;

    /// <summary>미니게임에 들어가기 직전에 있던 채널. 돌아올 때 이 채널로 재접속한다.</summary>
    private string savedChannelId;

    /// <summary>돌아올 때 쓸 닉네임. 접속에 필요하다.</summary>
    private string savedNickname;

    private bool busy;
    private Action<string> entryFailure;

    /// <summary>지금 미니게임에 들어가 있는가.</summary>
    public static bool InMiniGame { get; private set; }

    /// <summary>
    /// 전환이 어디까지 갔는지. **취소 버튼을 언제 감출지**가 이 값으로 갈린다.
    ///
    /// <c>Preparing</c> 동안에는 아직 아무것도 끊지 않았으므로 되돌릴 수 있다.
    /// <c>Committed</c> 부터는 Lobby Runner 를 이미 껐으므로 되돌릴 수 없다.
    /// </summary>
    public enum Stage
    {
        /// <summary>전환 중이 아니다.</summary>
        Idle,

        /// <summary>들어갈 준비 중. 아직 Lobby 를 끊지 않았다 — 취소할 수 있다.</summary>
        Preparing,

        /// <summary>Lobby 를 끊었다. **여기서부터 취소는 없다.**</summary>
        Committed,
    }

    public static Stage Current { get; private set; } = Stage.Idle;

    /// <summary>전환 단계가 바뀌었다. 로딩 화면이 취소 버튼을 감출 때 쓴다.</summary>
    public static event Action<Stage> StageChanged;

    private static MiniGameTransition Instance
    {
        get
        {
            if (instance != null) return instance;

            GameObject host = new GameObject("MiniGameTransition");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MiniGameTransition>();

            return instance;
        }
    }

    // ------------------------------------------------------------
    // 들어가기
    // ------------------------------------------------------------

    /// <summary>
    /// **미니게임으로 들어간다.**
    ///
    /// <paramref name="sceneName"/> 은 공통 설정에서 오고, <paramref name="sessionName"/> 은
    /// 어느 방으로 갈지다. 이번 단계에서는 늘 고정 세션 하나가 들어온다.
    /// </summary>
    /// <param name="sessionName">
    /// 들어갈 방. <b>비워 두면 미니게임의 기본 세션</b>을 쓴다. 이번 단계에서는 늘 비운다 —
    /// 진짜 매칭이 붙기 전까지는 고정 세션이고, 그 사실을 감추지 않는다.
    /// </param>
    public static void Enter(string sceneName, string sessionName = null, Action<string> onFailed = null)
    {
        Instance.StartCoroutine(Instance.EnterRoutine(sceneName, sessionName, onFailed));
    }

    private IEnumerator EnterRoutine(string sceneName, string sessionName, Action<string> onFailed)
    {
        if (busy)
        {
            Debug.LogWarning("[MiniGameTransition] 이미 전환 중입니다. 두 번째 요청은 무시합니다.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Fail(onFailed, "미니게임 씬 이름이 비어 있습니다.");
            yield break;
        }

        busy = true;
        SetStage(Stage.Preparing);

        // 돌아올 곳을 먼저 기억한다. Disconnect 가 채널 정보를 지우므로 순서가 중요하다.
        RememberLobby();

        TransitionStatus.SetLoading("게임에 입장 중...");

        // 여기서부터는 되돌릴 수 없다. 화면의 취소 버튼도 이 신호로 사라진다.
        SetStage(Stage.Committed);

        yield return ShutdownRunner();

        if (!shutdownDone)
        {
            // ⚠ 예전 Runner 가 살아 있다. 이대로 씬을 열면 런처가 조용히 포기하고
            //    실패 신호조차 오지 않는다. **들어가지 않고 되돌린다.**
            const string reason = "기존 네트워크 연결을 종료하지 못했습니다.";

            Debug.LogError($"[MiniGameTransition] {reason} 미니게임에 들어가지 않고 되돌립니다.");

            busy = false;
            onFailed?.Invoke(reason);

            yield return RecoverToLobby();
            yield break;
        }

        // 어느 방으로 갈지 알려 둔다. 비워 두면 런처가 기본 세션을 쓴다.
        MiniGameSessionRequest.Pending = sessionName;

        // 접속에 실패하면 로딩 화면에 갇히지 않게 Lobby 로 되돌린다.
        entryFailure = onFailed;
        MiniGameEntry.Failed += HandleEntryFailed;

        InMiniGame = true;
        busy = false;
        SetStage(Stage.Idle);

        SceneFlow.ToMiniGame(sceneName);
    }

    /// <summary>
    /// 미니게임 접속이 실패했다. **Lobby 로 되돌린다.**
    ///
    /// 서버가 안 떠 있거나 연결이 거부된 경우다. 그대로 두면 사람이 빈 시작 씬에 남는다.
    /// </summary>
    private void HandleEntryFailed(string reason)
    {
        MiniGameEntry.Failed -= HandleEntryFailed;

        Debug.LogError($"[MiniGameTransition] 미니게임 입장 실패 — {reason}. Lobby 로 돌아갑니다.");

        Action<string> tell = entryFailure;
        entryFailure = null;

        tell?.Invoke(reason);

        ReturnToLobby();
    }

    /// <summary>
    /// 지금 붙어 있는 채널을 기억한다.
    ///
    /// ⚠ <c>Disconnect()</c> 와 <c>OnShutdown</c> 이 <c>connectedChannelId</c> 를 지운다.
    ///    **끊기 전에** 읽어 두지 않으면 돌아올 곳을 잃는다.
    /// </summary>
    private void RememberLobby()
    {
        savedChannelId = LobbyReturnInfo.ChannelId;
        savedNickname = LobbyReturnInfo.Nickname;

        if (string.IsNullOrEmpty(savedChannelId))
        {
            Debug.LogWarning(
                "[MiniGameTransition] 돌아올 채널을 모릅니다. 미니게임이 끝나면 채널 선택으로 보냅니다.");
        }
        else
        {
            Debug.Log($"[MiniGameTransition] 돌아올 채널을 기억했습니다 — {savedChannelId}");
        }
    }

    /// <summary>
    /// Lobby Runner 를 끄고 **정말 사라질 때까지** 기다린다.
    ///
    /// <c>NetworkRunner.Instances</c> 에 돌고 있는 것이 하나도 없어야 미니게임 런처가
    /// 세션을 시작한다. <c>Destroy</c> 는 프레임 끝에 처리되므로 한 프레임으로는 부족하다.
    /// </summary>
    private IEnumerator ShutdownRunner()
    {
        shutdownDone = false;

        if (NetworkServiceLocator.IsReady)
        {
            NetworkServiceLocator.Current.Disconnect();
        }

        // ⚠ **위 한 줄로는 미니게임 Runner 가 꺼지지 않는다.**
        //
        //    FusionNetworkService.Disconnect() 는 자기가 만든 Lobby Runner 만 끈다. 미니게임
        //    Runner 는 각 런처(ShipCoopLauncher 등)가 StartGame 으로 직접 띄운 것이라 서비스가
        //    아예 모른다. 그래서 미니게임 안에서 나올 때는 Disconnect 가 곧바로 반환하고,
        //    Runner 는 그대로 살아남아 아래 대기 루프가 타임아웃까지 돈다.
        //
        //    들어갈 때는 이 문제가 안 보였다. 그때 돌고 있던 것이 Lobby Runner 라
        //    Disconnect 로 꺼졌기 때문이다. 나올 때만 드러난다.
        ShutdownStrayRunners();

        float waited = 0f;

        while (AnyRunnerAlive())
        {
            if (waited >= ShutdownTimeoutSeconds)
            {
                Debug.LogError(
                    $"[MiniGameTransition] Runner 가 {ShutdownTimeoutSeconds:0}초 안에 사라지지 않았습니다.");
                yield break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        shutdownDone = true;
        Debug.Log($"[MiniGameTransition] Runner 가 종료됐습니다. ({waited:0.00}초)");
    }

    /// <summary>직전 <see cref="ShutdownRunner"/> 가 실제로 끝냈는가. 코루틴은 값을 못 돌려준다.</summary>
    private bool shutdownDone;

    /// <summary>
    /// <see cref="NetworkServiceLocator"/> 가 모르는 Runner 를 끈다. **미니게임 Runner 가 여기 해당한다.**
    ///
    /// 끄는 주인을 찾아다니지 않고 <c>NetworkRunner.Instances</c> 를 훑는다. 미니게임마다 런처가
    /// 다르고 앞으로 더 늘어나는데, 그때마다 이곳이 그 이름을 알아야 한다면 같은 사고가 반복된다.
    /// "이 프로세스에 Runner 는 하나만 있어야 한다" 는 규칙만 알면 충분하다.
    /// </summary>
    private static void ShutdownStrayRunners()
    {
        // ⚠ Shutdown 이 Instances 를 건드릴 수 있으므로 먼저 훑어 담고 나서 끈다.
        List<NetworkRunner> alive = new List<NetworkRunner>();

        foreach (NetworkRunner candidate in NetworkRunner.Instances)
        {
            if (candidate != null && candidate.IsRunning) alive.Add(candidate);
        }

        foreach (NetworkRunner stray in alive)
        {
            Debug.Log($"[MiniGameTransition] 서비스가 모르는 Runner 를 끕니다 — {stray.name}");
            stray.Shutdown();
        }
    }

    private static bool AnyRunnerAlive()
    {
        foreach (NetworkRunner candidate in NetworkRunner.Instances)
        {
            if (candidate != null && candidate.IsRunning) return true;
        }

        return false;
    }

    // ------------------------------------------------------------
    // 나오기
    // ------------------------------------------------------------

    /// <summary>
    /// **Lobby 로 돌아간다.** 미니게임 결과 화면과 접속 실패 복구가 함께 쓴다.
    ///
    /// 들어갈 때 기억해 둔 채널로 자동 재접속한다. 채널을 모르거나 재접속에 실패하면
    /// 기존 실패 흐름을 따라 채널 선택 화면으로 간다.
    /// </summary>
    public static void ReturnToLobby()
    {
        Instance.StartCoroutine(Instance.ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        if (busy)
        {
            Debug.LogWarning("[MiniGameTransition] 이미 전환 중입니다. 두 번째 요청은 무시합니다.");
            yield break;
        }

        busy = true;
        SetStage(Stage.Committed);

        MiniGameEntry.Failed -= HandleEntryFailed;
        entryFailure = null;

        TransitionStatus.SetLoading("Lobby로 돌아가는 중...");

        // 미니게임 Runner 를 끈다. 켜진 채로 Lobby 에 접속하면 런처가 그렇듯
        // 채널 접속도 조용히 막힌다.
        yield return ShutdownRunner();

        // ⚠ 인원도 함께 지운다. 안 지우면 다음에 매칭 없이 들어가는 판이
        //    앞 판의 인원을 물려받아 "1 / 2" 처럼 틀린 수를 기다린다.
        MiniGameSessionRequest.Pending = null;
        MiniGameSessionRequest.Crew = 0;
        InMiniGame = false;

        busy = false;
        yield return RecoverToLobby();
    }

    /// <summary>
    /// **Lobby 로 되돌린다.** 기억해 둔 채널로 재접속하고, 그럴 수 없으면 채널 선택으로 간다.
    ///
    /// 두 곳이 함께 쓴다 — 미니게임을 정상적으로 마치고 나올 때, 그리고 들어가다 실패해
    /// 되돌아갈 때. 어느 쪽이든 사람이 어딘가에 갇히지 않게 하는 마지막 길이다.
    ///
    /// <code>
    ///   Runner 가 없다 + 채널을 안다   → 그 채널로 재접속
    ///   Runner 가 없다 + 채널을 모른다 → 채널 선택 화면으로
    ///   Runner 가 남아 있다            → **아무것도 하지 않는다.** 로딩만 걷는다
    /// </code>
    ///
    /// ⚠ Runner 를 끄는 일은 여기서 하지 않는다. 부르는 쪽이 이미 했거나(정상 종료),
    ///    끄지 못해서 온 것(복구)이기 때문이다.
    /// </summary>
    private IEnumerator RecoverToLobby()
    {
        // ⚠ **Runner 가 아직 살아 있으면 새로 접속하지 않는다.**
        //
        //    이 프로세스에는 Runner 가 하나만 있어야 한다. 남아 있는 채로 Connect 를 부르면
        //    두 번째 Runner 를 만들거나, 만들다 실패해 원래 붙어 있던 Lobby 세션까지 잃는다.
        //    종료 타임아웃으로 여기 온 경우가 정확히 그 상황이다 — 끊으라고 했는데 안 끊겼다.
        //
        //    그때는 **아무것도 하지 않는 것이 옳다.** 기존 Lobby 는 아직 살아 있으므로
        //    로딩 화면만 걷어 주면 사람은 그 자리에서 다시 조작할 수 있다.
        if (AnyRunnerAlive())
        {
            Debug.LogWarning(
                "[MiniGameTransition] Runner 가 아직 살아 있어 재접속하지 않습니다. " +
                "기존 Lobby 를 그대로 두고 로딩 상태만 풉니다.");

            SetStage(Stage.Idle);
            TransitionStatus.SetReady();
            yield break;
        }

        // 여기부터는 Runner 가 하나도 없다. 새로 붙어도 안전하다.
        if (string.IsNullOrEmpty(savedChannelId) || !NetworkServiceLocator.IsReady)
        {
            Debug.LogWarning("[MiniGameTransition] 돌아갈 채널을 몰라 채널 선택 화면으로 보냅니다.");

            LobbyReturnInfo.Forget();
            SetStage(Stage.Idle);
            TransitionStatus.SetReady();
            SceneFlow.BackToChannelSelect();
            yield break;
        }

        // 채널에 붙으면 Fusion 이 Lobby 를 올린다. 여기서 씬을 따로 열지 않는다.
        Debug.Log($"[MiniGameTransition] 채널 {savedChannelId} 로 돌아갑니다.");
        TransitionStatus.SetLoading("Lobby로 돌아가는 중...");

        NetworkServiceLocator.Current.Connect(savedNickname, savedChannelId);

        SetStage(Stage.Idle);
    }

    // ------------------------------------------------------------

    private void Fail(Action<string> onFailed, string reason)
    {
        Debug.LogError($"[MiniGameTransition] {reason}");

        busy = false;
        SetStage(Stage.Idle);
        TransitionStatus.SetReady();

        onFailed?.Invoke(reason);
    }

    private static void SetStage(Stage next)
    {
        if (Current == next) return;

        Current = next;
        StageChanged?.Invoke(next);
    }

    /// <summary>
    /// 플레이 모드를 새로 시작할 때 비운다.
    /// 에디터에서 "Reload Domain" 을 꺼 두면 static 이 이전 플레이의 값을 들고 있다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        instance = null;
        InMiniGame = false;
        Current = Stage.Idle;
        StageChanged = null;
    }
}

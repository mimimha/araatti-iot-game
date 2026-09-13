using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <see cref="INetworkService"/> 의 실제 Fusion 구현.
///
/// ChannelSelect 에서 [입장] 을 누르면 여기가 <b>진짜 Dedicated Server 세션에 붙는다.</b>
/// 붙는 데 성공해야만 성공 이벤트를 쏜다. 실패하면 화면은 ChannelSelect 에 그대로 남는다.
///
/// <b>Lobby 씬은 누가 로드하는가 — Fusion 이다.</b>
/// <see cref="StartGameArgs.Scene"/> 에 Lobby 를 넘기므로 접속이 끝나면 Fusion 이 그 씬을
/// 네트워크 씬으로 올린다. 그래서 <see cref="SceneFlow.LobbyLoadedByNetwork"/> 를 켜서
/// SceneFlow 가 일반 <c>SceneManager.LoadScene("Lobby")</c> 를 또 하지 않게 한다.
/// 두 곳이 같은 씬을 로드하면 네트워크 오브젝트가 붙지 않은 Lobby 가 덮어써진다.
///
/// <b>포트는 다루지 않는다.</b> 클라이언트는 세션 <b>이름</b>만 알면 Photon Cloud 가 찾아 준다.
/// 포트는 서버 exe 의 <c>-port</c> 인자에서만 쓴다.
///
/// 화면 코드(ChannelSelectController · ChannelRowView)는 이 클래스를 모른다.
/// <see cref="NetworkServiceLocator"/> 를 통해 <see cref="INetworkService"/> 로만 만난다.
/// Fake 로 되돌리려면 <see cref="NetworkServiceBootstrap"/> 한 줄만 바꾸면 된다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-3)
/// </summary>
public class FusionNetworkService : MonoBehaviour, INetworkService, INetworkRunnerCallbacks
{
    /// <summary>Fusion 이 네트워크 씬으로 올릴 Lobby. Build Settings 의 경로와 같아야 한다.</summary>
    private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

    private NetworkRunner runner;
    private string connectedChannelId;
    private bool isConnecting;

    // ------------------------------------------------------------
    // INetworkService — 알림
    // ------------------------------------------------------------

    public event Action<ServerInfo[]> OnServerListUpdated;
    public event Action<bool, string> OnConnectResult;
    public event Action<int> OnLobbyPlayerCountChanged;
    public event Action<string, int, int> OnQueueUpdated;
    public event Action<string> OnMiniGameStarting;
    public event Action<string> OnDisconnected;

    private void Awake()
    {
        // Boot → ChannelSelect → Lobby 를 넘어 살아남아야 한다.
        // 접속은 ChannelSelect 에서 시작하고 세션은 Lobby 에서도 유지되기 때문이다.
        DontDestroyOnLoad(gameObject);
        NetworkServiceLocator.Register(this);

        Debug.Log("[FusionNetworkService] 실제 Fusion 세션을 사용합니다.", this);
    }

    private void OnDestroy()
    {
        NetworkServiceLocator.Unregister(this);
    }

    // ------------------------------------------------------------
    // 채널 목록
    // ------------------------------------------------------------

    /// <summary>
    /// 채널 목록을 돌려준다.
    ///
    /// 이번 단계에서는 <see cref="ChannelCatalog"/> 의 고정 목록이다.
    /// Photon 의 공개 세션 목록을 읽어 오는 것은 별도 PRD 범위다.
    /// 목록에 있어도 그 세션의 서버가 떠 있지 않으면 입장에서 실패한다.
    /// </summary>
    public void RequestServerList()
    {
        OnServerListUpdated?.Invoke(ChannelCatalog.ToServerInfos());
    }

    // ------------------------------------------------------------
    // 접속
    // ------------------------------------------------------------

    public async void Connect(string nickname, string serverId)
    {
        if (isConnecting)
        {
            Debug.Log("[FusionNetworkService] 이미 접속 중입니다.");
            return;
        }

        if (!ChannelCatalog.TryGetSessionName(serverId, out string sessionName))
        {
            OnConnectResult?.Invoke(false, "존재하지 않는 채널입니다.");
            return;
        }

        // Fusion 이 Lobby 를 additive 로 올린 뒤 내려야 할 "지금 보이는 화면" 을 기억해 둔다.
        // 접속이 끝난 뒤에 물어보면 이미 활성 씬이 바뀌어 있을 수 있어 지금 잡아 둔다.
        Scene screenBeforeJoin = SceneManager.GetActiveScene();

        isConnecting = true;

        // 접속하는 동안 화면을 가린다. 실제로 플레이할 수 있게 되는 시점은
        // 내 NetworkPlayer 가 스폰되고 카메라가 붙은 뒤다. (LocalPlayerView 가 Ready 로 바꾼다)
        TransitionStatus.SetLoading($"{ChannelCatalog.GetDisplayName(serverId)}에 접속 중...");

        runner = CreateRunner();

        Debug.Log($"[FusionNetworkService] \"{sessionName}\" 세션에 접속합니다. (채널 {serverId}, 닉네임 {nickname})");

        // ⚠ 이 프로젝트는 NetworkProjectConfig 의 PeerMode 가 **Multiple** 이다.
        //    그 모드에서는 클라이언트도 Scene 을 반드시 지정해야 Fusion 이 씬을 올린다.
        //    지정하지 않으면 이런 오류가 나고 **세션에는 붙지만 Lobby 가 로드되지 않는다.**
        //      "PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene."
        //    실제로 그 상태였다 — 서버에는 플레이어가 스폰되고 로그는 전부 정상인데
        //    화면은 ChannelSelect 에 그대로 남아 있었다.
        //
        //    공식 샘플(FusionBootstrap.StartClient)이 Scene 을 비워 두는 것은
        //    Single peer mode 기준이라 여기에 그대로 적용할 수 없다.
        //
        //    여기서 이중 로드는 생기지 않는다. 이 경로를 타는 클라이언트는 ChannelSelect 에 있고
        //    Lobby 를 아직 들고 있지 않기 때문이다.
        //    (개발용 직접 실행은 이미 Lobby 에서 시작하므로 이 경로를 타지 않는다.
        //     그쪽은 FusionLauncher 가 자기 씬을 지정한다)
        SceneRef lobby = SceneRef.FromPath(LobbyScenePath);

        if (!lobby.IsValid)
        {
            Debug.LogError($"[FusionNetworkService] Lobby 씬 경로를 해석하지 못했습니다: {LobbyScenePath}");
            isConnecting = false;
            TransitionStatus.SetReady();
            OnConnectResult?.Invoke(false, "Lobby 씬을 찾지 못했습니다.");
            return;
        }

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            Scene = lobby,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
        });

        isConnecting = false;

        if (!result.Ok)
        {
            Debug.LogWarning(
                $"[FusionNetworkService] 접속 실패: {result.ShutdownReason} — {result.ErrorMessage}");

            // 세션에 붙지 못했으므로 러너를 정리한다. 다음 시도에 걸리적거리지 않게.
            CleanUpRunner();

            // 화면을 다시 사용자에게 돌려준다. ChannelSelect 가 실패 문구를 띄우고 씬은 그대로 남는다.
            TransitionStatus.SetReady();

            OnConnectResult?.Invoke(false, DescribeFailure(result.ShutdownReason, serverId));
            return;
        }

        connectedChannelId = serverId;

        // ⚠ 여기서부터 Lobby 는 Fusion 이 로드했다. SceneFlow 가 또 로드하면 안 된다.
        SceneFlow.LobbyLoadedByNetwork = true;

        Debug.Log($"[FusionNetworkService] \"{sessionName}\" 세션 접속 성공. Lobby 는 Fusion 이 로드합니다.");

        // 화면 쪽에 먼저 알린다. ChannelSelectController 가 이 안에서 SceneFlow 를 부르는데,
        // 그 전에 씬을 내려 버리면 그 컴포넌트가 사라진 뒤에 콜백이 도는 꼴이 된다.
        OnConnectResult?.Invoke(true, string.Empty);

        // 그 다음 이전 화면을 내린다. Fusion 은 additive 로 올리므로 이걸 하지 않으면
        // ChannelSelect 의 ScreenSpaceOverlay Canvas 가 Lobby 위를 계속 덮는다.
        SceneFlow.UnloadScreenScene(screenBeforeJoin);
    }

    public void Disconnect()
    {
        if (runner == null)
        {
            return;
        }

        Debug.Log("[FusionNetworkService] 세션을 종료합니다.");

        CleanUpRunner();
        connectedChannelId = null;

        SceneFlow.LobbyLoadedByNetwork = false;
        TransitionStatus.SetReady();

        OnDisconnected?.Invoke("접속을 종료했습니다.");
    }

    // ------------------------------------------------------------
    // 러너 수명
    // ------------------------------------------------------------

    /// <summary>
    /// 접속할 때마다 새 러너를 만든다.
    ///
    /// 미리 만들어 두지 않는 이유: 접속하지 않는 화면(Title · Login)에서까지
    /// 러너가 떠 있으면 Lobby 의 <see cref="FusionLauncher"/> 가
    /// "이미 러너가 있다" 고 판단해 개발용 직접 실행이 막힌다.
    /// </summary>
    private NetworkRunner CreateRunner()
    {
        GameObject host = new GameObject("FusionRunner (Client)");
        host.transform.SetParent(transform, worldPositionStays: false);

        NetworkRunner created = host.AddComponent<NetworkRunner>();
        host.AddComponent<NetworkSceneManagerDefault>();

        // 입력 제공자는 러너와 같은 오브젝트에 있어야 Fusion 이 수집한다.
        PlayerInputProvider input = host.AddComponent<PlayerInputProvider>();

        created.ProvideInput = true;
        created.AddCallbacks(this);
        created.AddCallbacks(input);

        return created;
    }

    private void CleanUpRunner()
    {
        if (runner == null)
        {
            return;
        }

        if (runner.IsRunning)
        {
            runner.Shutdown();
        }

        Destroy(runner.gameObject);
        runner = null;
    }

    /// <summary>사용자가 읽을 수 있는 실패 사유로 바꾼다.</summary>
    private static string DescribeFailure(ShutdownReason reason, string channelId)
    {
        string channel = ChannelCatalog.GetDisplayName(channelId);

        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return $"{channel} 이(가) 열려 있지 않습니다.";

            case ShutdownReason.GameIsFull:
                return $"{channel} 이(가) 가득 찼습니다.";

            case ShutdownReason.ConnectionTimeout:
            case ShutdownReason.ConnectionRefused:
                return $"{channel} 에 연결하지 못했습니다.";

            default:
                return $"{channel} 입장에 실패했습니다. ({reason})";
        }
    }

    // ------------------------------------------------------------
    // 미니게임 — 이번 단계 범위 밖
    // ------------------------------------------------------------

    public void JoinMiniGameQueue(string miniGameName)
    {
        Debug.LogWarning($"[FusionNetworkService] 미니게임 대기열은 아직 구현되지 않았습니다. ({miniGameName})");
    }

    public void LeaveMiniGameQueue()
    {
        Debug.LogWarning("[FusionNetworkService] 미니게임 대기열은 아직 구현되지 않았습니다.");
    }

    public void ReportMiniGameResult(bool success, int score)
    {
        Debug.LogWarning($"[FusionNetworkService] 미니게임 결과 보고는 아직 구현되지 않았습니다. ({success}, {score})");
    }

    // ------------------------------------------------------------
    // Fusion 콜백
    // ------------------------------------------------------------

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        OnLobbyPlayerCountChanged?.Invoke(CountPlayers(r));
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        OnLobbyPlayerCountChanged?.Invoke(CountPlayers(r));
    }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[FusionNetworkService] 서버와의 연결이 끊어졌습니다: {reason}");

        connectedChannelId = null;
        SceneFlow.LobbyLoadedByNetwork = false;

        OnDisconnected?.Invoke("서버와의 연결이 끊어졌습니다.");
    }

    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner r, ShutdownReason shutdownReason)
    {
        connectedChannelId = null;
        SceneFlow.LobbyLoadedByNetwork = false;
    }

    private static int CountPlayers(NetworkRunner r)
    {
        int count = 0;
        foreach (PlayerRef _ in r.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    #region Unused Fusion callbacks

    void INetworkRunnerCallbacks.OnInput(NetworkRunner r, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner r) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner r) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner r) { }

    #endregion
}

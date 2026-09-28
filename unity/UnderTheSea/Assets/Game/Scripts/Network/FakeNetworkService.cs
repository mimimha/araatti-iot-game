using System;
using System.Collections;
using System.Collections.Generic;
using MiniGames.Common;
using UnityEngine;

/// <summary>
/// 서버 없이 화면을 만들고 테스트하기 위한 가짜 네트워크 서비스.
///
/// ⚠ 임시 구현입니다. 서버 담당자의 진짜 구현이 나오면 이 컴포넌트만 빼면 됩니다.
///    화면 코드는 INetworkService 만 쓰기 때문에 고칠 필요가 없습니다.
///
/// 사용법
///   1. Boot 씬에 빈 오브젝트를 만든다. (이름: NetworkService)
///   2. 이 스크립트를 붙인다.
///   3. 끝. 씬이 바뀌어도 살아남는다.
///
/// Inspector 에서 서버 목록, 지연 시간, 실패 시뮬레이션을 조절할 수 있다.
/// </summary>
public class FakeNetworkService : MonoBehaviour, INetworkService
{
    [Header("가짜 서버 목록")]
    [SerializeField]
    private List<ServerInfo> servers = new List<ServerInfo>
    {
        new ServerInfo("srv-1", "서버 1", 12, 100),
        new ServerInfo("srv-2", "서버 2", 3, 100),
    };

    [Header("응답 지연 (초)")]
    [Tooltip("서버 목록을 돌려주기까지 걸리는 시간")]
    [SerializeField, Range(0f, 3f)] private float serverListDelay = 0.3f;

    [Tooltip("접속 결과를 돌려주기까지 걸리는 시간")]
    [SerializeField, Range(0f, 3f)] private float connectDelay = 0.6f;

    [Header("실패 시뮬레이션 (테스트용)")]
    [Tooltip("켜면 접속이 항상 실패한다. 실패 화면을 확인할 때 사용한다.")]
    [SerializeField] private bool alwaysFailConnect = false;

    [SerializeField] private string failReason = "서버에 연결할 수 없습니다.";

    [Header("미니게임 대기열")]
    [Tooltip("대기열 요청 뒤 첫 서버 패킷을 흉내 내기까지 기다리는 시간")]
    [SerializeField, Range(0f, 15f)] private float queueResponseDelay = 5f;

    [Tooltip("필요 인원")]
    [SerializeField] private int requiredPlayers = 4;

    [Tooltip("가짜 플레이어가 몇 초마다 한 명씩 들어오는지")]
    [SerializeField, Range(0.2f, 5f)] private float fakeJoinInterval = 1.2f;

    public event Action<ServerInfo[]> OnServerListUpdated;
    public event Action<bool, string> OnConnectResult;
    public event Action<int> OnLobbyPlayerCountChanged;
    public event Action<string, int, int> OnQueueUpdated;
    public event Action<string> OnMiniGameStarting;
    public event Action<string> OnDisconnected;

    private string _connectedServerId;
    private string _nickname;
    private Coroutine _queueRoutine;

    /// <summary>지금 접속되어 있는지</summary>
    public bool IsConnected => !string.IsNullOrEmpty(_connectedServerId);

    private void Awake()
    {
        // 씬이 바뀌어도 살아남아야 한다. 접속 상태를 유지해야 하기 때문.
        DontDestroyOnLoad(gameObject);
        NetworkServiceLocator.Register(this);
        Debug.Log("[FakeNetworkService] 가짜 네트워크 서비스가 등록되었습니다. (임시 구현)", this);
    }

    private void OnDestroy()
    {
        NetworkServiceLocator.Unregister(this);
    }

    // ------------------------------------------------------------
    // 요청
    // ------------------------------------------------------------

    public void RequestServerList()
    {
        StartCoroutine(RespondServerList());
    }

    private IEnumerator RespondServerList()
    {
        yield return WaitUnscaled(serverListDelay);
        OnServerListUpdated?.Invoke(servers.ToArray());
    }

    public void Connect(string nickname, string serverId)
    {
        StartCoroutine(RespondConnect(nickname, serverId));
    }

    private IEnumerator RespondConnect(string nickname, string serverId)
    {
        yield return WaitUnscaled(connectDelay);

        if (alwaysFailConnect)
        {
            OnConnectResult?.Invoke(false, failReason);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(nickname))
        {
            OnConnectResult?.Invoke(false, "닉네임을 입력해 주세요.");
            yield break;
        }

        int index = servers.FindIndex(s => s.Id == serverId);
        if (index < 0)
        {
            OnConnectResult?.Invoke(false, "존재하지 않는 서버입니다.");
            yield break;
        }

        if (servers[index].IsFull)
        {
            OnConnectResult?.Invoke(false, "서버가 가득 찼습니다.");
            yield break;
        }

        _nickname = nickname;
        _connectedServerId = serverId;

        // 내가 들어갔으니 인원이 하나 늘어난다.
        ServerInfo joined = servers[index];
        joined.CurrentPlayers++;
        servers[index] = joined;

        OnConnectResult?.Invoke(true, string.Empty);
        OnLobbyPlayerCountChanged?.Invoke(joined.CurrentPlayers);
    }

    public void Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        int index = servers.FindIndex(s => s.Id == _connectedServerId);
        if (index >= 0)
        {
            ServerInfo left = servers[index];
            left.CurrentPlayers = Mathf.Max(0, left.CurrentPlayers - 1);
            servers[index] = left;
        }

        StopQueue();
        _connectedServerId = null;
        _nickname = null;

        OnDisconnected?.Invoke("접속을 종료했습니다.");
    }

    public void JoinMiniGameQueue(string miniGameName)
    {
        StopQueue();
        PlayerRoster.ClearPlayers();
        _queueRoutine = StartCoroutine(FillQueue(miniGameName));
    }

    private IEnumerator FillQueue(string miniGameName)
    {
        // 실제 서버의 최초 대기열 패킷을 기다리는 상황을 그대로 흉내 낸다.
        yield return WaitUnscaled(queueResponseDelay);

        // 나 한 명으로 시작해서 가짜 플레이어가 한 명씩 들어온다.
        int targetPlayers = ResolveRequiredPlayers(miniGameName);
        int current = 1;
        PlayerRoster.RegisterNextTestPlayer(isLocal: true);
        OnQueueUpdated?.Invoke(miniGameName, current, targetPlayers);

        while (current < targetPlayers)
        {
            yield return WaitUnscaled(fakeJoinInterval);
            current++;
            PlayerRoster.RegisterNextTestPlayer();
            OnQueueUpdated?.Invoke(miniGameName, current, targetPlayers);
        }

        // Fake는 명단까지만 채운다. 게임 시작은 실제 화면 규칙과 동일하게
        // 30초 자동 시작 또는 [게임 시작] 버튼이 결정한다.
        _queueRoutine = null;
    }

    public void LeaveMiniGameQueue()
    {
        StopQueue();
        PlayerRoster.ClearPlayers();
    }

    private int ResolveRequiredPlayers(string miniGameName)
    {
        if (string.Equals(miniGameName, MiniGameId.Sword.ToString(), StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(miniGameName, MiniGameId.Mining.ToString(), StringComparison.OrdinalIgnoreCase)) return 4;
        if (string.Equals(miniGameName, MiniGameId.Ship.ToString(), StringComparison.OrdinalIgnoreCase)) return 4;
        return Mathf.Max(1, requiredPlayers);
    }

    private void StopQueue()
    {
        if (_queueRoutine != null)
        {
            StopCoroutine(_queueRoutine);
            _queueRoutine = null;
        }
    }

    public void ReportMiniGameResult(bool success, int score)
    {
        MiniGameConfig config = PlayerRoster.CurrentGame;
        ReportMiniGameResult(new MiniGameResult(
            config != null ? config.GameId : MiniGameId.Sword,
            success,
            score,
            0f,
            config != null ? config.ExtraStatLabel : null,
            string.Empty,
            config != null ? config.FragmentId : null,
            fragmentObtained: false,
            playerCount: PlayerRoster.ActivePlayerCount));
    }

    public void ReportMiniGameResult(MiniGameResult result)
    {
        string rewardId = !string.IsNullOrEmpty(result.RewardId)
            ? result.RewardId
            : PlayerRoster.CurrentGame != null ? PlayerRoster.CurrentGame.FragmentId : null;

        bool obtained = result.IsClear && !RewardService.Has(rewardId);
        MiniGameResult settled = result.WithReward(rewardId, obtained);
        Debug.Log($"[FakeNetworkService] 미니게임 결과 확정 — 성공: {settled.IsClear}, 점수: {settled.Score}", this);
        MiniGameResultGateway.SubmitAuthoritative(settled);
    }

    // ------------------------------------------------------------

    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}

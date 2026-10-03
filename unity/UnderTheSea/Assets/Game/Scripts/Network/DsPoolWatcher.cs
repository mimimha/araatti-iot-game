using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using MiniGames.Common;
using UnityEngine;

/// <summary>
/// **미니게임 서버들이 지금 몇 명을 데리고 있는지 지켜본다.** 로비 DS 에서만 돈다.
///
/// <b>어떻게 아는가.</b> Photon 은 <i>세션 로비</i> 에 붙어 있는 피어에게 열려 있는 세션
/// 목록을 통째로 보내 준다(<c>OnSessionListUpdated</c>). 미니게임 DS 들은 아무것도 알리지
/// 않아도 거기에 나타난다 — 런처가 <c>IsVisible</c> 을 건드리지 않고, 기본값이 참이기 때문이다.
///
/// <b>그래서 서버끼리 통신할 필요가 없다.</b> 사람이 튕겨서 인원이 줄어드는 것도,
/// 미니게임 DS 가 죽어서 목록에서 사라지는 것도 Photon 이 알아서 반영한다. 미니게임 쪽에
/// "나 비었어요" 를 보내는 코드를 넣지 않아도 된다.
///
/// <code>
///   로비 DS ─┬─ 게임용 러너      lobby-ch1 을 호스팅. 사람들이 붙어 있다.
///            └─ 감시용 러너      세션 로비에 붙어 목록만 본다.  ← 이 부품
/// </code>
///
/// ⚠ <b>러너를 하나 더 쓰는 이유.</b> Photon 피어는 <b>로비에 있거나 방에 있거나</b> 둘 중
///    하나다. 게임을 호스팅하는 러너는 이미 방 안에 있으므로 목록을 받지 못한다.
///    <c>PeerMode.Multiple</c> 이라 한 프로세스에 러너를 여러 개 둘 수 있다.
///
/// ⚠ <b>이 부품은 방을 배정하지 않는다.</b> 목록은 몇 초 늦게 갱신되므로, 방금 사람을
///    보낸 방은 아직 "0명" 으로 보인다. 그것만 믿고 배정하면 같은 방에 두 팀이 들어간다.
///    배정은 자기가 보낸 곳을 기억하는 쪽이 하고, 여기는 <b>관측된 사실</b>만 말한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DsPoolWatcher : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string HostName = "[DS Pool 감시]";

    /// <summary>끊겼을 때 다시 붙기까지 기다리는 시간(초).</summary>
    private const float RetrySeconds = 5f;

    /// <summary>돌고 있는 감시기. 로비 DS 가 아니면 null 이다.</summary>
    public static DsPoolWatcher Current { get; private set; }

    /// <summary>한 방에서 관측된 것.</summary>
    public readonly struct Room
    {
        /// <summary>방 이름. 예: <c>mine-2</c></summary>
        public readonly string Session;

        /// <summary>지금 들어가 있는 사람 수. <b>서버 자신은 뺀 값이다.</b></summary>
        public readonly int Occupants;

        /// <summary>이 방이 받을 수 있는 사람 수. <b>서버 자신은 뺀 값이다.</b></summary>
        public readonly int Capacity;

        /// <summary>Photon 이 새 접속을 받는 상태인가.</summary>
        public readonly bool Open;

        public Room(string session, int occupants, int capacity, bool open)
        {
            Session = session;
            Occupants = occupants;
            Capacity = capacity;
            Open = open;
        }

        /// <summary>아무도 없는가.</summary>
        public bool Empty => Occupants <= 0;
    }

    private readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();

    private NetworkRunner runner;
    private bool stopping;

    /// <summary>세션 목록을 한 번이라도 받았는가. 받기 전에는 "빈 방이 없다" 와 구별해야 한다.</summary>
    public bool Ready { get; private set; }

    /// <summary>목록이 갱신될 때마다 부른다. 매칭이 밀린 사람을 다시 살펴보는 신호로 쓴다.</summary>
    public event Action Updated;

    /// <summary>
    /// **감시를 시작한다.** 로비 DS 가 기동을 마친 뒤 한 번 부른다.
    ///
    /// 두 번 불러도 안전하다 — 이미 돌고 있으면 그대로 둔다.
    /// </summary>
    public static void Begin()
    {
        if (Current != null) return;

        var host = new GameObject(HostName);
        DontDestroyOnLoad(host);
        Current = host.AddComponent<DsPoolWatcher>();
    }

    /// <summary>감시를 멈추고 치운다.</summary>
    public static void End()
    {
        if (Current == null) return;

        Current.stopping = true;
        Destroy(Current.gameObject);
        Current = null;
    }

    private void Start() => StartCoroutine(WatchForever());

    private IEnumerator WatchForever()
    {
        while (!stopping)
        {
            yield return Connect();

            if (stopping) yield break;

            // 여기 왔다는 것은 붙지 못했거나 끊겼다는 뜻이다. 목록을 믿을 수 없게 됐으므로
            // 지우고 다시 시도한다. 남겨 두면 죽은 서버를 비어 있다고 말하게 된다.
            rooms.Clear();
            Ready = false;
            Updated?.Invoke();

            yield return new WaitForSeconds(RetrySeconds);
        }
    }

    private IEnumerator Connect()
    {
        runner = gameObject.AddComponent<NetworkRunner>();

        // 조작하는 사람이 없다. 입력을 만들지 않는다.
        runner.ProvideInput = false;
        runner.AddCallbacks(this);

        FusionAppSettings settings = FusionSessionIsolation.PhotonSettings;

        Debug.Log($"{HostName} 세션 로비에 붙는 중… ({FusionSessionIsolation.Describe()})");

        // ⚠ await 를 코루틴에서 기다리려면 Task 를 직접 들고 있어야 한다.
        System.Threading.Tasks.Task<StartGameResult> join =
            runner.JoinSessionLobby(SessionLobby.ClientServer, customAppSettings: settings);

        while (!join.IsCompleted) yield return null;

        if (join.IsFaulted || join.Result == null || !join.Result.Ok)
        {
            string why = join.IsFaulted
                ? join.Exception?.GetBaseException().Message
                : $"{join.Result?.ShutdownReason} — {join.Result?.ErrorMessage}";

            Debug.LogWarning($"{HostName} 세션 로비 접속 실패: {why}. {RetrySeconds}초 뒤 다시 시도합니다.");
            Cleanup();
            yield break;
        }

        Debug.Log($"{HostName} 붙었습니다. 미니게임 서버 목록을 지켜봅니다.");

        // 러너가 살아 있는 동안은 여기서 기다린다. 끊기면 아래로 떨어져 재시도로 간다.
        //
        // ⚠ **IsRunning 으로 보면 안 된다.** 그것은 시뮬레이션이 도는지를 말하는데,
        //    세션 로비에만 붙은 러너는 방에 들어가 있지 않으므로 늘 거짓이다. 그렇게 짰더니
        //    붙자마자 "연결이 끊겼습니다" 가 뜨며 5초마다 다시 붙기를 반복했다.
        //    IsCloudReady 가 "로비 갱신을 받을 수 있는 상태" 를 가리키는 값이다.
        while (!stopping && runner != null && !runner.IsShutdown && runner.IsCloudReady)
        {
            yield return null;
        }

        if (!stopping) Debug.LogWarning($"{HostName} 연결이 끊겼습니다. 다시 붙습니다.");
        Cleanup();
    }

    private void Cleanup()
    {
        if (runner == null) return;

        runner.RemoveCallbacks(this);
        Destroy(runner);
        runner = null;
    }

    private void OnDestroy()
    {
        stopping = true;
        if (Current == this) Current = null;
    }

    // ───────────────────────────── 조회 ─────────────────────────────

    /// <summary>
    /// 이 게임의 방들을 번호 순서대로 돌려준다. 서버가 안 떠 있는 방은 빠진다.
    /// </summary>
    public List<Room> RoomsOf(MiniGameId game)
    {
        var found = new List<Room>(DsPool.SizePerGame);

        for (int number = 1; number <= DsPool.SizePerGame; number++)
        {
            string session = DsPool.SessionName(game, number);
            if (session.Length != 0 && rooms.TryGetValue(session, out Room room)) found.Add(room);
        }

        return found;
    }

    /// <summary>이 방이 관측되고 있는가. 없으면 서버가 안 떠 있거나 아직 목록을 못 받았다.</summary>
    public bool TryGet(string session, out Room room) => rooms.TryGetValue(session, out room);

    /// <summary>
    /// **지금 이 게임에 들어와 있는 사람 수.** 로비와 미니게임을 모두 더한다.
    ///
    /// 채널 선택 화면에서 "몇 명이 놀고 있는지" 를 보여 주는 데 쓴다. 로비 인원만 세면
    /// 사람들이 미니게임에 들어간 순간 숫자가 뚝 떨어져 <b>아무도 없는 것처럼 보인다.</b>
    /// 광산에 있는 사람도 이 게임을 하고 있는 사람이다.
    ///
    /// 각 방의 <see cref="Room.Occupants"/> 는 이미 서버 자신을 뺀 값이라 그대로 더하면 된다.
    ///
    /// ⚠ <b>채널이 하나라는 전제다.</b> 미니게임 세션은 채널을 구분하지 않으므로,
    ///    채널을 늘리면 누가 어느 채널에서 왔는지 알 수 없어 이 합계가 뭉개진다.
    ///    그때는 세션 이름에 채널을 넣거나 방마다 속성을 붙여야 한다.
    /// </summary>
    public int TotalOccupants()
    {
        int sum = 0;

        foreach (Room room in rooms.Values)
        {
            sum += room.Occupants;
        }

        return sum;
    }

    /// <summary>
    /// <paramref name="crew"/> 명이 통째로 들어갈 수 있는 <b>빈 방</b>들. 번호 순서다.
    ///
    /// 우리 매칭은 방 하나에 한 팀만 넣는다. 그래서 "자리가 남은 방" 이 아니라
    /// <b>아무도 없는 방</b>만 고른다. 남은 자리에 끼워 넣으면 3인 팀이 진행 중인 방에
    /// 2인 팀이 들어가 버린다.
    ///
    /// ⚠ <b>하나만 돌려주지 않는다.</b> 받는 쪽은 "이미 다른 일행에게 준 방" 을 걸러 내야
    ///    하는데, 후보가 하나뿐이면 그 방이 걸렸을 때 다음으로 넘어갈 수가 없다.
    ///
    /// ⚠ 목록을 아직 한 번도 못 받았으면 <b>빈 목록</b>이다. "빈 방이 없다" 와 같은 모양이라
    ///    보이지만, 어느 쪽이든 지금 배정하면 안 되는 것은 같다.
    /// </summary>
    public List<string> EmptyRooms(MiniGameId game, int crew)
    {
        var free = new List<string>(DsPool.SizePerGame);
        if (!Ready) return free;

        foreach (Room room in RoomsOf(game))
        {
            if (!room.Open || !room.Empty || room.Capacity < crew) continue;

            free.Add(room.Session);
        }

        return free;
    }

    // ───────────────────────────── 콜백 ─────────────────────────────

    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList)
    {
        rooms.Clear();

        foreach (SessionInfo info in sessionList)
        {
            if (info == null || string.IsNullOrEmpty(info.Name)) continue;

            // ⚠ **서버 자신이 한 자리를 차지한다.** 아무도 접속하지 않은 미니게임 DS 가
            //    PlayerCount 1 · MaxPlayers 5 로 보인다(실측). 정원 4명짜리 광산인데도 그렇다.
            //    그대로 쓰면 빈 방을 영영 못 찾고, 정원도 한 명씩 많게 센다.
            int occupants = Mathf.Max(0, info.PlayerCount - 1);
            int capacity = Mathf.Max(0, info.MaxPlayers - 1);

            rooms[info.Name] = new Room(info.Name, occupants, capacity, info.IsOpen);
        }

        Ready = true;
        Updated?.Invoke();
    }

    // 아래는 인터페이스를 채우기 위한 빈 구현이다. 이 부품은 세션 목록만 본다.
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner r, PlayerRef p) { }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner r, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner r, ShutdownReason reason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner r) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner r, NetAddress addr, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ReadOnlySpan<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner r) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner r) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p) { }
}

using System;
using MiniGames.Common;

/// <summary>
/// 서버 하나의 정보. Title 화면의 서버 목록에 표시된다.
/// </summary>
[Serializable]
public struct ServerInfo
{
    /// <summary>내부 식별용 ID. 접속할 때 이 값을 보낸다.</summary>
    public string Id;

    /// <summary>화면에 보일 이름. 예: "서버 1"</summary>
    public string DisplayName;

    /// <summary>현재 접속 인원</summary>
    public int CurrentPlayers;

    /// <summary>최대 인원</summary>
    public int MaxPlayers;

    public ServerInfo(string id, string displayName, int currentPlayers, int maxPlayers)
    {
        Id = id;
        DisplayName = displayName;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
    }

    /// <summary>인원이 가득 찼는지</summary>
    public bool IsFull => CurrentPlayers >= MaxPlayers;
}

/// <summary>
/// 클라이언트와 서버 사이의 경계.
///
/// 규칙
///   - 클라이언트(화면)는 네트워크 라이브러리를 직접 부르지 않고 이 인터페이스만 사용한다.
///   - 서버 담당자는 화면(UI)을 직접 건드리지 않고 아래 event 로만 알린다.
///
/// 이 규칙을 지키면 서버 담당자가 나중에 라이브러리를 바꾸거나
/// Dedicated Server 로 옮겨도 클라이언트 코드는 고치지 않아도 된다.
///
/// 자세한 내용은 GAME_STRUCTURE.md 4장 참고.
/// 수정할 때는 클라이언트 담당자와 서버 담당자가 함께 정하고, 문서에도 반영한다.
/// </summary>
public interface INetworkService
{
    // ------------------------------------------------------------
    // 클라이언트 → 서버 (요청)
    // ------------------------------------------------------------

    /// <summary>서버 목록을 요청한다. 결과는 OnServerListUpdated 로 온다.</summary>
    void RequestServerList();

    /// <summary>고른 서버의 로비에 접속한다. 결과는 OnConnectResult 로 온다.</summary>
    void Connect(string nickname, string serverId);

    /// <summary>접속을 끊는다.</summary>
    void Disconnect();

    /// <summary>미니게임 대기열에 등록한다. 4명이 모이면 OnMiniGameStarting 이 온다.</summary>
    void JoinMiniGameQueue(string miniGameName);

    /// <summary>대기열 등록을 취소한다.</summary>
    void LeaveMiniGameQueue();

    /// <summary>미니게임이 끝났을 때 결과를 보고한다.</summary>
    void ReportMiniGameResult(bool success, int score);

    /// <summary>
    /// 점수·시간·게임별 기록을 포함한 전체 결과를 서버에 보고한다.
    /// 새 미니게임은 이 오버로드를 사용하고, 위의 짧은 함수는 기존 코드 호환용으로 둔다.
    /// </summary>
    void ReportMiniGameResult(MiniGameResult result);

    // ------------------------------------------------------------
    // 서버 → 클라이언트 (알림)
    // ------------------------------------------------------------

    /// <summary>서버 목록을 받았다.</summary>
    event Action<ServerInfo[]> OnServerListUpdated;

    /// <summary>접속 시도 결과. (성공 여부, 실패했다면 그 이유)</summary>
    event Action<bool, string> OnConnectResult;

    /// <summary>로비 인원이 바뀌었다. (현재 인원)</summary>
    event Action<int> OnLobbyPlayerCountChanged;

    /// <summary>
    /// 최초 대기열 응답 또는 대기열 인원 변경을 받았다. (미니게임 이름, 현재 인원, 필요 인원)
    /// 실제 구현은 이 이벤트를 올리기 전에 PlayerRoster를 서버 스냅샷으로 갱신한다.
    /// </summary>
    event Action<string, int, int> OnQueueUpdated;

    /// <summary>매칭이 완료되었다. (이동할 미니게임 이름)</summary>
    event Action<string> OnMiniGameStarting;

    /// <summary>연결이 끊겼다. (끊긴 이유)</summary>
    event Action<string> OnDisconnected;
}

using UnityEngine;

/// <summary>
/// **어느 방으로 들어갈 것인가.** Lobby 가 채우고 미니게임 런처가 읽는다.
///
/// <b>왜 필요한가.</b> 세션 이름은 지금까지 실행 인자 <c>-session</c> 으로만 정할 수 있었고
/// 그것은 <b>프로세스가 뜰 때 고정</b>된다. 게임 안에서 방을 정해 주려면 씬을 넘어 전달할
/// 자리가 하나 필요하다.
///
/// <b>미니게임을 알지 못한다.</b> 문자열 하나라서 배 · 광산 · 검이 같은 자리를 쓴다.
///
/// ⚠ 비어 있으면 실행 인자를 따른다. 그래서 지금까지의 단독 실행 방법이 그대로 살아 있다.
/// </summary>
public static class MiniGameSessionRequest
{
    /// <summary>다음에 들어갈 세션. 비어 있으면 실행 인자를 쓴다.</summary>
    public static string Pending { get; set; }

    /// <summary>
    /// **이번 판의 인원.** 매칭이 정해 준 수다. 0 이면 정해진 것이 없다.
    ///
    /// 접속할 때 <c>StartGameArgs.ConnectionToken</c> 에 실려 미니게임 서버로 간다.
    /// 그것이 유일한 길이다 — 로비 서버와 미니게임 서버는 서로 말을 걸지 않는다.
    /// </summary>
    public static int Crew { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Pending = null;
        Crew = 0;
    }
}

/// <summary>
/// **미니게임 서버가 기억하는 "몇 명짜리 판인가".** 서버에서만 쓴다.
///
/// <b>왜 필요한가.</b> 광산은 2 · 3 · 4명 중에 고를 수 있다. 서버는 그 수를 알아야
/// 몇 명이 모였을 때 출발시킬지 정할 수 있는데, <b>로비 서버가 알려 줄 길이 없다</b> —
/// 둘은 서로 접속하지 않는다. 그래서 들어오는 사람이 들고 온다.
///
/// <code>
///   클라이언트  ConnectionToken 에 인원을 실어 접속
///   서버        MiniGameAdmission 이 읽어서 여기 적어 둔다
///   게임        MineNet.ResolveCrewToStart 가 여기를 먼저 본다
/// </code>
///
/// ⚠ 판이 끝나 아무도 없게 되면 <see cref="Forget"/> 로 지운다. 안 지우면 다음 팀이
///    <b>앞 팀의 인원</b>으로 시작한다 — 2인 판 다음에 온 4인 팀이 둘만 모여도 출발한다.
///
/// ⚠ 먼저 들어온 사람의 값을 쓴다. 뒤에 온 사람이 다른 수를 들고 오면 무시하고 남긴다.
///    같은 일행이면 같은 수여야 하고, 다르다면 그쪽이 잘못 온 것이다.
/// </summary>
public static class MatchCrew
{
    /// <summary>서버가 쪽지를 읽어 적어 둔 값. 클라이언트에서는 늘 0 이다.</summary>
    private static int remembered;

    /// <summary>
    /// 이번 판의 인원. 0 이면 아직 모른다.
    ///
    /// <b>서버와 클라이언트가 같은 값을 봐야 한다.</b> 화면에 "1 / 2" 처럼 띄우는 곳이
    /// 클라이언트에도 있어서, 한쪽만 알면 서버는 1명에 시작하는데 화면은 2명을 기다린다고
    /// 나온다. 실제로 그렇게 보였다.
    ///
    /// <code>
    ///   서버        MiniGameAdmission 이 접속 쪽지를 읽어 Remember 로 적는다
    ///   클라이언트   로비에서 떠날 때 MiniGameSessionRequest.Crew 에 적어 두고 들고 온다
    /// </code>
    ///
    /// 둘 중 있는 쪽을 쓴다. 새 네트워크 변수를 만들지 않아도 같은 수가 나온다.
    /// </summary>
    public static int Assigned => remembered > 0 ? remembered : MiniGameSessionRequest.Crew;

    /// <summary>처음 들어온 사람이 들고 온 수를 적어 둔다.</summary>
    public static void Remember(int crew)
    {
        if (crew <= 0 || remembered == crew) return;

        if (remembered != 0)
        {
            Debug.LogWarning(
                $"[판 인원] 이미 {remembered}명짜리 판인데 {crew}명을 들고 온 사람이 있습니다. " +
                "먼저 정해진 값을 그대로 씁니다.");
            return;
        }

        remembered = crew;
        Debug.Log($"[판 인원] 이번 판은 {crew}명입니다.");
    }

    /// <summary>판이 끝났다. 다음 팀을 위해 지운다.</summary>
    public static void Forget()
    {
        if (remembered == 0) return;

        Debug.Log($"[판 인원] {remembered}명짜리 판이 끝났습니다. 잊습니다.");
        remembered = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => remembered = 0;
}

/// <summary>
/// **미니게임이 끝나면 어디로 돌아가는가.** 채널 접속이 채우고 전환이 읽는다.
///
/// <b>왜 따로 두는가.</b> <c>FusionNetworkService</c> 는 <c>Disconnect()</c> 와
/// <c>OnShutdown</c> 에서 자기가 들고 있던 채널 정보를 지운다. 그것이 맞는 동작이지만,
/// 미니게임에 들어가려면 <b>반드시 끊어야</b> 하므로 끊는 순간 돌아올 곳도 함께 사라진다.
/// 그래서 끊기와 무관한 자리에 따로 적어 둔다.
///
/// ⚠ 허브의 이름은 어디서나 <b>Lobby</b> 다.
/// </summary>
public static class LobbyReturnInfo
{
    /// <summary>마지막으로 접속한 채널. 미니게임에서 돌아올 때 이 채널로 재접속한다.</summary>
    public static string ChannelId { get; private set; }

    /// <summary>그때 쓴 닉네임. 재접속에 필요하다.</summary>
    public static string Nickname { get; private set; }

    /// <summary>
    /// **방금 다녀온 미니게임의 씬 이름.** 로비에 다시 들어갈 때 그 포탈 앞에 서려고 적어 둔다.
    ///
    /// 미니게임에 들어갈 때 적고(<c>MiniGameTransition</c>), 로비에 접속하면서 서버에 쪽지로
    /// 보낸 뒤 지운다(<c>FusionNetworkService.Connect</c>). 처음 로그인할 때는 비어 있으므로
    /// 지금처럼 기본 자리에 선다. 쪽지 모양은 <see cref="LobbyReturnToken"/>.
    /// </summary>
    public static string CameFrom { get; private set; }

    /// <summary>채널에 접속할 때 적어 둔다.</summary>
    public static void Remember(string nickname, string channelId)
    {
        Nickname = nickname;
        ChannelId = channelId;
    }

    /// <summary>미니게임에 들어갈 때 적는다. 돌아오면 그 포탈 앞에 선다.</summary>
    public static void RememberCameFrom(string sceneName)
    {
        CameFrom = sceneName;
    }

    /// <summary>
    /// 로비 접속에 쪽지를 실어 보냈으면 지운다. 남겨 두면 나중에 채널을 옮겨 들어갈 때도
    /// 엉뚱하게 그 포탈 앞에 선다.
    /// </summary>
    public static void ForgetCameFrom()
    {
        CameFrom = null;
    }

    /// <summary>채널 선택으로 되돌아갈 때처럼, 돌아갈 곳이 사라졌을 때 비운다.</summary>
    public static void Forget()
    {
        Nickname = null;
        ChannelId = null;
        CameFrom = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Forget();
}

/// <summary>
/// **미니게임 입장이 실패했다.** 게임별 런처가 알리고, 전환이 듣고 복구한다.
///
/// 게임마다 런처가 다르지만 실패했다는 사실은 같다. 그래서 여기 한 자리로 모은다 —
/// 전환 코드가 특정 미니게임의 타입을 알 필요가 없어진다.
///
/// 듣는 사람이 없으면 아무 일도 일어나지 않는다. 실행 인자로 바로 켜는 단독 실행은
/// 예전 그대로 로그만 남는다.
/// </summary>
public static class MiniGameEntry
{
    /// <summary>입장 실패. 인자는 사람이 읽을 사유다.</summary>
    public static event System.Action<string> Failed;

    public static void ReportFailed(string reason) => Failed?.Invoke(reason);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Failed = null;
}

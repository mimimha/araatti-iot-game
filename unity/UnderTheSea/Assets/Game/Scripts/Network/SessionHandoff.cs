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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Pending = null;
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

    /// <summary>채널에 접속할 때 적어 둔다.</summary>
    public static void Remember(string nickname, string channelId)
    {
        Nickname = nickname;
        ChannelId = channelId;
    }

    /// <summary>채널 선택으로 되돌아갈 때처럼, 돌아갈 곳이 사라졌을 때 비운다.</summary>
    public static void Forget()
    {
        Nickname = null;
        ChannelId = null;
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

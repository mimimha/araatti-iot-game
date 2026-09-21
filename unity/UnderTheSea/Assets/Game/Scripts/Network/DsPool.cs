using MiniGames.Common;

/// <summary>
/// **미니게임 하나가 쓰는 전용 서버 묶음.** 방 이름을 정하는 유일한 자리다.
///
/// <code>
///   광산   mine-1      mine-2
///   검     warriors-1  warriors-2
///   배     shipcoop-1  shipcoop-2
/// </code>
///
/// <b>왜 따로 두는가.</b> 매칭은 "빈 서버가 있나" 를 Photon 세션 목록으로 판단한다.
/// 그러려면 목록에서 <b>이 게임의 방만</b> 골라내야 하는데, 그 규칙이 매칭 · 감시기 ·
/// 런처에 흩어지면 방을 한 대 늘리는 날 한 군데를 빠뜨린다.
///
/// <b>이름은 여기서 만들지 않는다.</b> 앞부분은 각 게임의 <c>SessionPrefix</c> 에서
/// 그대로 가져온다. 런처가 실제로 여는 방 이름과 어긋날 수 없다.
///
/// ⚠ <see cref="SizePerGame"/> 은 <b>실제로 띄워 둔 서버 대수와 같아야 한다.</b>
///    이 값만 올리고 서버를 안 띄우면 매칭이 없는 방을 배정한다.
/// </summary>
public static class DsPool
{
    /// <summary>게임 하나에 서버 몇 대를 띄워 두는가.</summary>
    public const int SizePerGame = 2;

    /// <summary>이 게임의 방 이름 앞부분. 모르는 게임이면 빈 문자열.</summary>
    public static string PrefixOf(MiniGameId id)
    {
        switch (id)
        {
            case MiniGameId.Mining: return Mine.Net.MineNet.SessionPrefix;
            case MiniGameId.Sword:  return Warriors.Net.WarriorsNet.SessionPrefix;
            case MiniGameId.Ship:   return UnderTheSea.MiniGames.ShipCoop.Net.ShipCoopNet.SessionPrefix;
            default:                return string.Empty;
        }
    }

    /// <summary><paramref name="roomNumber"/> 번 방의 이름. 번호는 1부터 센다.</summary>
    public static string SessionName(MiniGameId id, int roomNumber)
    {
        string prefix = PrefixOf(id);
        return prefix.Length == 0 ? string.Empty : $"{prefix}-{roomNumber}";
    }

    /// <summary>
    /// 이 방이 그 게임의 것인가.
    ///
    /// ⚠ 앞부분만 보고 판단하면 <c>mine</c> 과 <c>mine-boss</c> 같은 이름이 생겼을 때
    ///    서로를 자기 방으로 오해한다. 그래서 <b>번호까지 맞는지</b> 본다.
    /// </summary>
    public static bool Owns(MiniGameId id, string sessionName)
    {
        if (string.IsNullOrEmpty(sessionName)) return false;

        for (int room = 1; room <= SizePerGame; room++)
        {
            if (sessionName == SessionName(id, room)) return true;
        }

        return false;
    }
}

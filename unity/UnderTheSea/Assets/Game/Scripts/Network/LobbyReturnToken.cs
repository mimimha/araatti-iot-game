using System.Text;

/// <summary>
/// **로비에 다시 들어갈 때 들고 가는 쪽지.** "어느 미니게임에서 돌아왔나" 한 가지가 적혀 있다.
///
/// <code>
///   클라이언트  미니게임 씬 이름을 StartGameArgs.ConnectionToken 에 싣는다   (FusionNetworkService)
///   로비 서버   GetPlayerConnectionToken 으로 읽고 그 포탈 앞에 세운다        (PlayerSpawner)
/// </code>
///
/// <b>왜 필요한가.</b> 미니게임에서 돌아오는 것은 로비에 <b>새로 접속</b>하는 것이다. 로비 서버는
/// 그 사람이 어디서 왔는지 모르니 처음 로그인한 사람과 똑같이 기본 자리에 세웠다. 게임을 마친
/// 사람이 섬 반대편 시작 지점에서 다시 걸어와야 했다.
///
/// 미니게임 서버에 인원을 넘기는 <c>MatchCrewToken</c> 과 같은 방식이다. 읽고 쓰는 규칙을 한 곳에 둔다.
///
/// ⚠ <b>이 값을 그대로 믿지 않는다.</b> 클라이언트가 보내는 것이라 고쳐 보낼 수 있다. 그래서 좌표가
///    아니라 <b>씬 이름</b>만 싣고, 서버는 자기 씬에 그 이름의 포탈이 있을 때만 그 앞에 세운다.
///    모르는 이름이면 기본 자리다. 아무 데나 서게 할 수는 없다.
///
/// ⚠ 쪽지가 없거나 모양이 다르면 null 이다. 처음 로그인, 예전 클라이언트가 그렇다.
/// </summary>
public static class LobbyReturnToken
{
    /// <summary>씬 이름이 이보다 길면 버린다. 우리 씬 이름은 20자도 안 된다.</summary>
    private const int MaxBytes = 64;

    /// <summary>쪽지에 씬 이름을 적는다. 비어 있으면 빈 쪽지(null)다.</summary>
    public static byte[] Write(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return null;

        byte[] bytes = Encoding.UTF8.GetBytes(sceneName);
        return bytes.Length <= MaxBytes ? bytes : null;
    }

    /// <summary>쪽지에서 씬 이름을 읽는다. 없거나 모양이 다르면 null.</summary>
    public static string Read(byte[] token)
    {
        if (token == null || token.Length == 0 || token.Length > MaxBytes) return null;

        string name = Encoding.UTF8.GetString(token);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}

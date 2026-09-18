using Fusion.Photon.Realtime;
using UnityEngine;

/// <summary>
/// <b>사람마다 Photon 방을 갈라놓는다.</b>
///
/// Photon 의 방은 <c>AppId + AppVersion + 지역</c> 안에서 <b>전 세계가 공유한다.</b>
/// 우리 팀은 AppId 하나를 같이 쓰므로, 세션 이름이 같으면 옆자리 사람의 서버와
/// 내 클라이언트가 그냥 만난다. 실제로 이런 일이 있었다.
///
///   - 서연님이 본인 PC 에서 <c>warriors-1</c> 로 DS 를 먼저 띄웠다.
///   - 내 DS 는 <c>GameIdAlreadyExists</c> 로 방을 못 열고, 아무도 안 들어왔다.
///   - 내 클라이언트는 조용히 <b>서연님 서버</b>에 붙었다. 빌드가 서로 달라서
///     네트워크 값 배치가 어긋났고, 클라이언트에서 이렇게 터졌다.
///
///         AssertException: meta.WordCount == NetworkObject.GetWordCount(instance)
///
///     화면에서는 몬스터가 투명하고, 처치 수가 안 오르고, 한 명인데 판이 시작했다.
///     원인을 찾는 데 반나절이 걸렸다. <b>어디에도 "남의 서버에 붙었다" 는 말은 없다.</b>
///
/// <c>-appver</c> 를 주면 그 값이 Photon 의 AppVersion 이 된다. Photon 문서의 표현대로
/// AppId 하나가 값마다 <b>서로 다른 가상 AppId</b> 로 갈라지므로, 값이 다른 사람끼리는
/// 방 목록조차 보이지 않는다. 세션 이름이 같아도 부딪히지 않는다.
///
///     AraAtti-Server.exe -batchmode -nographics -session warriors-1 -port 27017 -appver geonhee
///     AraAtti-Client.exe -mode client -session lobby-ch1 -appver geonhee
///
/// ⚠ <b>서버와 클라이언트에 같은 값을 줘야 만난다.</b> 한쪽만 주면 서로 못 본다.
///    이것은 고장이 아니라 이 인자가 하는 일 그대로다.
///
/// ⚠ <b>인자를 주지 않으면 지금까지와 똑같다.</b> 값이 비면 <see cref="PhotonSettings"/> 가
///    <c>null</c> 이고, Fusion 은 <c>null</c> 을 받으면 공용 설정을 쓴다.
///    빌드·배포 경로는 이 인자를 넘기지 않으므로 제품 동작이 달라지지 않는다.
///
/// ⚠ <b>공용 <c>PhotonAppSettings.asset</c> 은 건드리지 않는다.</b> 전역 설정의 AppVersion 을
///    직접 고치면 에디터가 그 에셋을 수정된 것으로 보고, 실수로 커밋되면 팀 전체가
///    갈라진다. 그래서 복사본을 만들어 이 판에만 넘긴다.
/// </summary>
public static class FusionSessionIsolation
{
    /// <summary>Photon AppVersion 으로 쓸 값. 없으면 지금까지처럼 팀 전체가 같은 방을 본다.</summary>
    public const string AppVersionKey = "-appver";

    /// <summary>
    /// <b>붙을 Photon 지역을 못 박는다.</b> (예: <c>kr</c> · <c>jp</c> · <c>asia</c>)
    ///
    /// ⚠ <b>Photon 의 방 목록은 지역마다 완전히 따로다.</b> 서버가 A 지역에 방을 열었는데
    ///    클라이언트가 B 지역을 고르면, 방은 멀쩡히 있는데 없다고 나온다.
    ///
    ///        Photon Cloud Operation failed [32758]: 'Game does not exist'
    ///        ShutdownReason: GameNotFound
    ///
    ///    이 값을 주지 않으면 Photon 이 <b>접속할 때마다 전 세계에 핑을 쏴서</b> 가장 가까운
    ///    지역을 고른다. 핑이 비슷하면 알파벳 순으로 고르므로(RegionHandler 참고)
    ///    <b>몇 ms 차이로 지역이 갈린다.</b> 고른 결과는 저장되지도 않아 띄울 때마다 다시 뽑는다.
    ///    서버와 클라이언트가 같은 PC 일 때는 같은 핑이 나와 잘 맞았고, 다른 컴퓨터가
    ///    들어오자 누구는 되고 누구는 "방이 없다" 가 나왔다.
    ///
    /// <b>게임 지연에는 영향이 없다.</b> Fusion Server 모드에서 지역은 <b>서로를 찾는 데만</b>
    /// 쓰이고, 실제 게임 트래픽은 클라이언트와 DS 가 직통으로 주고받는다. 그래서 나중에
    /// 서버가 EC2 로 가도 이 값은 배포 설정 한 줄로 맞추면 된다.
    /// </summary>
    public const string RegionKey = "-region";

    /// <summary>한 번만 읽는다. 커맨드라인은 프로세스가 사는 동안 바뀌지 않는다.</summary>
    private static bool resolved;

    private static string version;

    private static string region;

    private static FusionAppSettings settings;

    /// <summary>이 프로세스가 쓸 AppVersion. 인자가 없으면 빈 문자열.</summary>
    public static string AppVersion
    {
        get
        {
            Resolve();
            return version;
        }
    }

    /// <summary>못 박은 Photon 지역. 인자가 없으면 빈 문자열(= 핑으로 자동 선택).</summary>
    public static string Region
    {
        get
        {
            Resolve();
            return region;
        }
    }

    /// <summary>남들과 갈라져 있는가.</summary>
    public static bool Isolated => !string.IsNullOrEmpty(AppVersion);

    /// <summary>
    /// <c>StartGameArgs.CustomPhotonAppSettings</c> 에 그대로 넣을 값.
    ///
    /// <b><c>null</c> 이 정상이다.</b> Fusion 은 이 값이 비어 있으면 공용 설정을 쓴다.
    /// 그래서 호출하는 쪽은 인자가 있는지 없는지 따질 필요 없이 한 줄만 쓰면 된다.
    ///
    ///     CustomPhotonAppSettings = FusionSessionIsolation.PhotonSettings,
    /// </summary>
    public static FusionAppSettings PhotonSettings
    {
        get
        {
            Resolve();
            return settings;
        }
    }

    /// <summary>서버 로그 맨 앞에 남길 한 줄.</summary>
    public static string Describe()
    {
        string room = Isolated
            ? $"AppVersion \"{AppVersion}\" — 같은 값을 준 사람하고만 만납니다."
            : "AppVersion 없음 — 팀 공용입니다. 세션 이름이 겹치면 남의 서버에 붙을 수 있습니다.";

        string where = string.IsNullOrEmpty(Region)
            ? "지역 자동 — 띄울 때마다 핑으로 다시 고릅니다. 서버와 다른 지역을 고르면 방을 못 찾습니다."
            : $"지역 \"{Region}\" 고정 — 서버와 클라이언트가 같은 값이어야 만납니다.";

        return $"{room} / {where}";
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;

        version = (FusionLaunchArguments.GetString(AppVersionKey, string.Empty) ?? string.Empty).Trim();
        region = (FusionLaunchArguments.GetString(RegionKey, string.Empty) ?? string.Empty).Trim();

        // 둘 다 없으면 손댈 것이 없다. null 을 넘기면 Fusion 이 공용 설정을 쓴다.
        if (version.Length == 0 && region.Length == 0)
        {
            // ⚠ 아무것도 안 박은 쪽도 반드시 남긴다. 사고가 나는 것은 이쪽이다.
            //    "왜 남의 서버에 붙었지" · "왜 방이 없다고 하지" 를 로그에서 찾을 수 있어야 한다.
            Debug.Log($"[FusionSessionIsolation] {Describe()}");
            return;
        }

        if (!PhotonAppSettings.TryGetGlobal(out PhotonAppSettings global) || global.AppSettings == null)
        {
            // 여기까지 오면 Photon 설정 자체가 없다. 방을 가르기는커녕 접속도 안 된다.
            // 이 인자 때문에 못 떴다고 오해하지 않도록 이유를 남기고 공용으로 둔다.
            Debug.LogError(
                "[FusionSessionIsolation] 공용 Photon 설정을 찾지 못해 " +
                $"{AppVersionKey} · {RegionKey} 를 적용하지 못합니다. " +
                "Assets/Photon/Fusion/Resources/PhotonAppSettings.asset 을 확인해 주세요.");
            version = string.Empty;
            region = string.Empty;
            return;
        }

        // ⚠ 공용 설정을 고치지 않고 복사본에만 값을 넣는다. 위 주석 참고.
        FusionAppSettings copy = new FusionAppSettings();
        global.AppSettings.CopyTo(copy);

        if (version.Length > 0)
        {
            copy.AppVersion = version;
        }

        if (region.Length > 0)
        {
            // FixedRegion 을 주면 Photon 이 핑 단계를 건너뛰고 이 지역으로 바로 간다.
            copy.FixedRegion = region;
        }

        settings = copy;

        Debug.Log($"[FusionSessionIsolation] {Describe()}");
    }
}

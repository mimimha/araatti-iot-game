using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채널 ID ↔ Fusion 세션 이름을 잇는 고정 표.
///
/// <b>이번 단계에서는 고정 목록이다.</b> 자동 증설도, Photon 세션 목록 조회도 하지 않는다.
/// 서버 exe 를 몇 개 띄우느냐가 곧 채널 수다.
///
///     채널 ID "srv-1"  →  세션 이름 "lobby-ch1"   ←  AraAtti-Server.exe -session lobby-ch1
///     채널 ID "srv-2"  →  세션 이름 "lobby-ch2"   ←  AraAtti-Server.exe -session lobby-ch2
///
/// ⚠ 채널 ID 는 <b>"srv-N"</b> 이다. Boot 씬의 FakeNetworkService 가 쓰던 값과 같게 맞췄다.
///    (ChannelSelectController 의 sampleChannels 는 "ch-N" 을 쓰지만 그건 씬 단독 실행용
///     화면 확인 목록이고 실제 흐름을 타지 않는다)
///
/// <b>채널 = 별도 Lobby 인스턴스.</b>
/// 나중에 서버를 더 띄워도 기존 채널의 정원이 늘어나는 것이 아니라
/// `lobby-ch3` 같은 <b>독립된 Lobby</b> 가 하나 더 생기는 구조다. (샤드)
/// 정원이 차면 서버를 자동으로 띄우는 일은 매치메이커/서버 디렉터리를 다루는 별도 PRD 범위다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-3)
/// </summary>
public static class ChannelCatalog
{
    /// <summary>화면에 보여 줄 채널 하나. <see cref="ServerInfo"/> 로 바꿔서 UI 에 넘긴다.</summary>
    public readonly struct Channel
    {
        /// <summary>INetworkService.Connect 에 실리는 값.</summary>
        public readonly string Id;

        /// <summary>화면에 보이는 이름.</summary>
        public readonly string DisplayName;

        /// <summary>Fusion 세션 이름. 서버 exe 의 -session 값과 같아야 한다.</summary>
        public readonly string SessionName;

        /// <summary>표시용 정원. 실제 인원 제한은 이번 단계에서 걸지 않는다.</summary>
        public readonly int MaxPlayers;

        public Channel(string id, string displayName, string sessionName, int maxPlayers)
        {
            Id = id;
            DisplayName = displayName;
            SessionName = sessionName;
            MaxPlayers = maxPlayers;
        }
    }

    /// <summary>
    /// 지금 열려 있는 채널들.
    ///
    /// 여기를 늘리려면 그 세션 이름으로 서버 exe 를 하나 더 띄워야 한다.
    /// 띄우지 않은 채널을 고르면 접속이 실패하고 그 사유가 화면에 뜬다.
    /// </summary>
    private static readonly Channel[] Channels =
    {
        new Channel("srv-1", "서버 1", "lobby-ch1", 100),
        new Channel("srv-2", "서버 2", "lobby-ch2", 100),
    };

    /// <summary>채널 목록을 화면이 아는 형태로 돌려준다.</summary>
    public static ServerInfo[] ToServerInfos(IReadOnlyDictionary<string, int> playerCounts = null)
    {
        ServerInfo[] result = new ServerInfo[Channels.Length];

        for (int i = 0; i < Channels.Length; i++)
        {
            Channel c = Channels[i];

            // 실제 인원수는 이번 단계에서 채우지 않는다. 접속해 봐야 알 수 있다.
            int current = 0;
            if (playerCounts != null && playerCounts.TryGetValue(c.Id, out int known))
            {
                current = known;
            }

            result[i] = new ServerInfo(c.Id, c.DisplayName, current, c.MaxPlayers);
        }

        return result;
    }

    /// <summary>채널 ID 로 세션 이름을 찾는다. 모르는 ID 면 false.</summary>
    public static bool TryGetSessionName(string channelId, out string sessionName)
    {
        foreach (Channel c in Channels)
        {
            if (string.Equals(c.Id, channelId, System.StringComparison.Ordinal))
            {
                sessionName = c.SessionName;
                return true;
            }
        }

        Debug.LogWarning($"[ChannelCatalog] 모르는 채널 ID 입니다: \"{channelId}\"");
        sessionName = null;
        return false;
    }

    /// <summary>채널 ID 로 표시 이름을 찾는다. 실패 문구에 쓴다.</summary>
    public static string GetDisplayName(string channelId)
    {
        foreach (Channel c in Channels)
        {
            if (string.Equals(c.Id, channelId, System.StringComparison.Ordinal))
            {
                return c.DisplayName;
            }
        }

        return channelId;
    }
}

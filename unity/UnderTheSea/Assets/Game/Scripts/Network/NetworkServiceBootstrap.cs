using UnityEngine;

/// <summary>
/// 네트워크 서비스를 게임 시작 시 하나 만들어 둔다.
///
/// **여기가 Fake ↔ Fusion 을 갈아끼우는 유일한 지점이다.**
/// 아래 <see cref="Active"/> 한 줄만 바꾸면 전환된다. 화면 코드도 씬도 고치지 않는다.
///
///     Active = Implementation.Fusion   실제 Dedicated Server 세션에 접속  ← 지금 설정
///     Active = Implementation.Fake     서버 없이 화면 흐름만 확인
///
/// 계정 쪽의 <c>AccountServiceBootstrap</c> 과 같은 구조다. (PRD 06 에서 이 방식으로
/// 계정 서비스를 Fake→HTTP 로 바꿨을 때 화면 코드 diff 가 0 줄이었다)
///
/// <b>씬에 컴포넌트를 올리지 않는 이유</b>
///   · 씬 파일은 병합 충돌이 가장 심하다. 전환할 때마다 Boot 씬을 고치고 싶지 않다
///   · Boot 을 거치지 않고 ChannelSelect 만 단독 실행해도 서비스가 준비된다
///
/// ⚠ Boot 씬의 <c>NetworkService</c> 오브젝트에 있던 <c>FakeNetworkService</c> 컴포넌트는
///    <b>제거해야 한다.</b> 그대로 두면 씬의 것과 여기서 만든 것이 둘 다 등록을 시도해
///    어느 쪽이 쓰일지가 실행 순서에 좌우된다.
///    (<see cref="NetworkServiceLocator"/> 가 덮어쓰기 경고를 내지만, 경고로 끝날 일이 아니다)
///    <c>FakeNetworkService.cs</c> 파일 자체는 남겨 둔다. 아래 Active 로 언제든 다시 쓴다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-3)
/// </summary>
public static class NetworkServiceBootstrap
{
    public enum Implementation
    {
        /// <summary>실제 Fusion Dedicated Server 세션에 접속한다.</summary>
        Fusion,

        /// <summary>서버 없이 이 PC 안에서 흉내낸다. 화면 흐름만 볼 때.</summary>
        Fake
    }

    /// <summary>
    /// ★ 지금 쓰는 구현체. **이 한 줄이 전환 스위치다.**
    ///
    /// const 가 아니라 static readonly 인 이유: const 로 두면 아래 분기 중 한쪽이
    /// "도달할 수 없는 코드" 경고를 낸다.
    /// </summary>
    private static readonly Implementation Active = Implementation.Fusion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateIfMissing()
    {
        // Dedicated Server 는 채널을 고르지도, 접속하지도 않는다. 서비스가 필요 없다.
        // 서버는 Lobby 씬의 FusionLauncher 가 직접 세션을 연다.
        if (FusionLaunchArguments.IsDedicatedServerProcess())
        {
            return;
        }

        if (NetworkServiceLocator.IsReady)
        {
            // 씬에 직접 올려둔 것이 이미 등록했다. 둘을 만들지 않는다.
            Debug.LogWarning(
                "[NetworkServiceBootstrap] 네트워크 서비스가 이미 등록돼 있어 새로 만들지 않습니다. " +
                "Boot 씬에 FakeNetworkService 컴포넌트가 남아 있는지 확인해 주세요.");
            return;
        }

        GameObject host = new GameObject($"NetworkService ({Active})");

        // AddComponent 가 곧바로 Awake 를 부르고, 그 안에서 Locator 에 등록된다.
        if (Active == Implementation.Fake)
        {
            host.AddComponent<FakeNetworkService>();
        }
        else
        {
            host.AddComponent<FusionNetworkService>();
        }
    }
}

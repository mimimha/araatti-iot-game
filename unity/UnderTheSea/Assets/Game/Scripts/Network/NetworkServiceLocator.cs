using UnityEngine;

/// <summary>
/// 현재 사용 중인 네트워크 서비스를 담아두는 곳.
///
/// 화면 쪽 코드는 여기서 서비스를 꺼내 쓴다.
///
///     if (NetworkServiceLocator.IsReady)
///     {
///         NetworkServiceLocator.Current.RequestServerList();
///     }
///
/// 가짜(FakeNetworkService)를 진짜로 바꾸고 싶으면
/// Boot 씬의 컴포넌트만 교체하면 된다. 화면 코드는 고치지 않는다.
/// </summary>
public static class NetworkServiceLocator
{
    /// <summary>현재 등록된 네트워크 서비스. 없으면 null.</summary>
    public static INetworkService Current { get; private set; }

    /// <summary>서비스가 준비되었는지</summary>
    public static bool IsReady => Current != null;

    /// <summary>서비스를 등록한다. 구현체가 Awake 에서 호출한다.</summary>
    public static void Register(INetworkService service)
    {
        if (service == null)
        {
            Debug.LogWarning("[NetworkServiceLocator] null 은 등록할 수 없습니다.");
            return;
        }

        if (Current != null && !ReferenceEquals(Current, service))
        {
            Debug.LogWarning(
                $"[NetworkServiceLocator] 이미 {Current.GetType().Name} 이 등록되어 있는데 " +
                $"{service.GetType().Name} 이 덮어씁니다. 씬에 네트워크 서비스가 두 개 있는지 확인하세요.");
        }

        Current = service;
    }

    /// <summary>등록을 해제한다. 구현체가 OnDestroy 에서 호출한다.</summary>
    public static void Unregister(INetworkService service)
    {
        if (ReferenceEquals(Current, service))
        {
            Current = null;
        }
    }
}

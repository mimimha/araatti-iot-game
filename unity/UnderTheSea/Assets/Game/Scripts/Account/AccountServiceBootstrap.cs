using UnityEngine;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 계정 · 캐릭터 서비스를 게임 시작 시 하나 만들어 둔다.
    ///
    /// **여기가 가짜 ↔ 진짜를 갈아끼우는 유일한 지점이다.**
    /// PRD 06 에서는 아래 두 줄의 컴포넌트 타입만 HTTP 구현으로 바꾸면 된다.
    ///
    ///     host.AddComponent&lt;FakeAuthService&gt;();       →  HttpAuthService
    ///     host.AddComponent&lt;FakeCharacterService&gt;();  →  HttpCharacterService
    ///
    /// 씬에 컴포넌트를 올리지 않고 코드로 만드는 이유
    ///   - 씬 파일을 고치지 않아도 된다. 씬은 병합 충돌이 가장 심한 파일이다.
    ///   - Boot 을 거치지 않고 Login 씬만 단독 실행해도 서비스가 준비된다.
    ///
    /// BeforeSceneLoad 로 도는 이유
    ///   첫 씬의 어떤 Awake · OnEnable 보다 먼저 실행되어야
    ///   화면 스크립트가 OnEnable 에서 서비스 이벤트를 구독할 수 있다.
    /// </summary>
    public static class AccountServiceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfMissing()
        {
            if (AccountServiceLocator.IsReady)
            {
                // 누군가 이미 등록했다. (씬에 직접 올려둔 경우)
                return;
            }

            GameObject host = new GameObject("AccountService (Fake)");

            // AddComponent 가 곧바로 Awake 를 부르고, 그 안에서 Locator 에 등록된다.
            host.AddComponent<FakeAuthService>();
            host.AddComponent<FakeCharacterService>();
        }
    }
}

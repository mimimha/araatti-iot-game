using UnityEngine;

namespace UnderTheSea.Account
{
    /// <summary>
    /// 계정 · 캐릭터 서비스를 게임 시작 시 하나 만들어 둔다.
    ///
    /// **여기가 가짜 ↔ 진짜를 갈아끼우는 유일한 지점이다.**
    /// 아래 <see cref="Active"/> 한 줄만 바꾸면 전환된다. 화면 코드는 고치지 않는다.
    ///
    ///     Active = Implementation.Http   실제 서버(ASP.NET Core + MySQL) 사용  ← 지금 설정
    ///     Active = Implementation.Fake   서버 없이 이 PC 안에서만 동작
    ///
    /// **Fake 로 되돌리는 방법**
    ///   1. 아래 Active 를 <c>Implementation.Fake</c> 로 바꾼다.
    ///   2. 저장하면 Unity 가 다시 컴파일한다. 그것으로 끝이다.
    ///      씬 · 프리팹을 고칠 필요가 없고, Fake 클래스는 그대로 남아 있다.
    ///   서버를 띄우지 않고 화면 흐름만 볼 때 쓴다. (server/README.md 를 참고해 서버를 띄우는 편이 정확하다)
    ///
    /// 씬에 컴포넌트를 올리지 않고 코드로 만드는 이유
    ///   - 씬 파일을 고치지 않아도 된다. 씬은 병합 충돌이 가장 심한 파일이다.
    ///   - Boot 을 거치지 않고 Login 씬만 단독 실행해도 서비스가 준비된다.
    ///
    /// BeforeSceneLoad 로 도는 이유
    ///   첫 씬의 어떤 Awake · OnEnable 보다 먼저 실행되어야
    ///   화면 스크립트가 OnEnable 에서 서비스 이벤트를 구독할 수 있다.
    ///
    /// 서버 주소를 바꾸려면 <see cref="HttpApiConfig.DefaultBaseUrl"/> 을 본다.
    /// </summary>
    public static class AccountServiceBootstrap
    {
        public enum Implementation
        {
            /// <summary>실제 서버와 HTTP 로 통신한다.</summary>
            Http,

            /// <summary>서버 없이 PlayerPrefs 로 흉내낸다. 화면 흐름만 확인할 때.</summary>
            Fake
        }

        /// <summary>
        /// ★ 지금 쓰는 구현체. **이 한 줄이 전환 스위치다.**
        ///
        /// const 가 아니라 static readonly 인 이유: const 로 두면 아래 분기 중 한쪽이
        /// "도달할 수 없는 코드" 경고를 낸다.
        /// </summary>
        private static readonly Implementation Active = Implementation.Http;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfMissing()
        {
            if (AccountServiceLocator.IsReady)
            {
                // 누군가 이미 등록했다. (씬에 직접 올려둔 경우)
                return;
            }

            GameObject host = new GameObject($"AccountService ({Active})");

            // AddComponent 가 곧바로 Awake 를 부르고, 그 안에서 Locator 에 등록된다.
            if (Active == Implementation.Fake)
            {
                host.AddComponent<FakeAuthService>();
                host.AddComponent<FakeCharacterService>();
            }
            else
            {
                host.AddComponent<HttpAuthService>();
                host.AddComponent<HttpCharacterService>();
            }
        }
    }
}

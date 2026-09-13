/// <summary>
/// 개발용 직접 Lobby 실행 경로.
///
/// 로그인 · REST · DB 를 거치지 않고 멀티플레이만 빠르게 보고 싶을 때 쓴다.
/// <b>로컬 씬만 여는 방식이 아니다.</b> 이 경로로 들어가도 반드시
/// Fusion <c>Client</c> 로 Dedicated Server 세션에 접속하고, 캐릭터는 서버가 스폰한다.
/// 즉 일반 사용자 경로와 네트워크 구조가 완전히 같고, 앞단의 UI 만 건너뛴다.
///
/// <b>어디에서만 켜지는가.</b>
/// <c>DEVELOPMENT_BUILD</c> 로 만든 실행 파일에서 <c>-devjoin</c> 을 줬을 때만 <c>true</c> 다.
/// Release 빌드에는 로그인 우회 수단이 아예 들어가지 않는다.
///
/// <b>에디터는 여기에 해당하지 않는다.</b> 에디터에서 Lobby 를 Play 하면
/// <see cref="FusionLauncher"/> 가 언제나 <c>GameMode.Client</c> 로 떠서 Dedicated Server 에 붙는다.
/// 토글로 켜고 끄던 예전 방식은 없앴다 — 켜는 것을 잊고 혼자 Host 로 놀게 되는 일이 실제로 있었다.
/// 즉 <b>플레이 모드 테스트에는 Dedicated Server 가 떠 있어야 한다.</b>
///
/// <b>신원 · 외형.</b>
/// 이번 단계(PRD 08-2)는 모든 플레이어가 기본 외형이므로 가짜 신원을 만들 필요가 없다.
/// PlayerPrefs · REST · MySQL 에 아무것도 쓰지 않고 읽지도 않는다.
///
/// 세션 이름 · 포트는 <see cref="FusionLaunchArguments"/> 규칙을 그대로 따른다.
/// <c>-session</c> · <c>-port</c> 가 있으면 그 값이 이기고, 없으면 FusionLauncher 의 Inspector 값을 쓴다.
///
/// 사용법: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
public static class FusionDevEntry
{
    /// <summary>
    /// 지금 실행이 "개발용 직접 접속" 인가.
    ///
    /// true 면 앞단 UI(Login · ChannelSelect) 없이 Lobby 씬만 열어도
    /// <see cref="FusionLauncher"/> 가 세션을 시작하도록 허용한다.
    ///
    /// ⚠ 이 값은 더 이상 <c>GameMode</c> 를 고르지 않는다. 모드는 언제나 Client 다.
    ///    이 값이 정하는 것은 **"Lobby 만 직접 열어도 되는가"** 하나뿐이다.
    /// </summary>
    public static bool WantsClientJoin
    {
        get
        {
#if DEVELOPMENT_BUILD
            // Development Build 에서는 실행 인자로만 켠다. 메뉴가 없기 때문이다.
            //   AraAtti-Client.exe -devjoin -session lobby-ch1
            return System.Array.Exists(
                System.Environment.GetCommandLineArgs(),
                arg => string.Equals(arg, "-devjoin", System.StringComparison.OrdinalIgnoreCase));
#else
            // Release 빌드에는 개발용 우회 경로가 없다.
            return false;
#endif
        }
    }
}

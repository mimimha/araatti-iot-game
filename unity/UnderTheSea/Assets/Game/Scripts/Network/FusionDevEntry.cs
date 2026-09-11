using UnityEngine;

/// <summary>
/// 개발용 직접 Lobby 실행 경로.
///
/// 로그인 · REST · DB 를 거치지 않고 멀티플레이만 빠르게 보고 싶을 때 쓴다.
/// <b>로컬 씬만 여는 방식이 아니다.</b> 이 경로로 들어가도 반드시
/// Fusion <c>Client</c> 로 Dedicated Server 세션에 접속하고, 캐릭터는 서버가 스폰한다.
/// 즉 일반 사용자 경로와 네트워크 구조가 완전히 같고, 앞단의 UI 만 건너뛴다.
///
/// <b>어디에서만 켜지는가.</b>
/// 아래 조건부 컴파일 밖에서는 이 클래스가 늘 <c>false</c> 를 돌려준다.
/// 그래서 Release 빌드에는 로그인 우회 수단이 아예 들어가지 않는다.
///   UNITY_EDITOR      에디터에서 Lobby 씬을 직접 Play
///   DEVELOPMENT_BUILD "Development Build" 로 만든 실행 파일
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
#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 개발용 접속을 켤지 여부. 메뉴로 토글한다.
    /// EditorPrefs 라 이 PC 의 이 에디터에만 남고 저장소에는 들어가지 않는다.
    /// </summary>
    public const string EditorPrefsKey = "AraAtti.Fusion.DevJoinAsClient";
#endif

    /// <summary>
    /// 지금 실행이 "개발용 직접 접속" 인가.
    ///
    /// true 면 <see cref="FusionLauncher"/> 가 AutoHostOrClient 대신 Client 로 뜬다.
    /// 즉 혼자 호스트가 되어 버리지 않고, 이미 떠 있는 Dedicated Server 를 찾아 붙는다.
    /// </summary>
    public static bool WantsClientJoin
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false);
#elif DEVELOPMENT_BUILD
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

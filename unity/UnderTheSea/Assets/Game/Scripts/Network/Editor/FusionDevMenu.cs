using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// 개발용 직접 Lobby 실행 경로를 에디터에서 켜고 끈다. (PRD 08-2)
    ///
    /// 켜면 에디터에서 Play 했을 때 <see cref="FusionLauncher"/> 가
    /// AutoHostOrClient 가 아니라 <b>Client</b> 로 떠서 이미 실행 중인
    /// Dedicated Server 세션에 붙는다. 로컬 씬만 여는 것이 아니라
    /// 서버가 스폰한 NetworkPlayer 를 받는 진짜 네트워크 경로다.
    ///
    /// 설정은 EditorPrefs 에 저장되어 이 PC 의 이 에디터에만 남는다. 저장소에 들어가지 않는다.
    /// </summary>
    public static class FusionDevMenu
    {
        private const string MenuPath = "Tools/아라아띠/개발용 Lobby 접속 (Fusion Client)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(FusionDevEntry.EditorPrefsKey, false);
            EditorPrefs.SetBool(FusionDevEntry.EditorPrefsKey, next);

            Debug.Log(next
                ? "[FusionDevMenu] 개발용 Lobby 접속을 켰습니다. " +
                  "이제 에디터에서 Play 하면 Dedicated Server 에 Client 로 붙습니다. " +
                  "서버 exe 를 먼저 띄워 두세요."
                : "[FusionDevMenu] 개발용 Lobby 접속을 껐습니다. 에디터는 다시 AutoHostOrClient 로 뜹니다.");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(FusionDevEntry.EditorPrefsKey, false));
            return true;
        }
    }
}

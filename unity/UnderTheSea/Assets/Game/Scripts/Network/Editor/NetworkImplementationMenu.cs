using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Network.EditorTools
{
    /// <summary>
    /// 에디터에서 네트워크 구현체를 Fake 로 바꾸는 스위치.
    ///
    /// 기본은 Fusion(실제 서버)이다. 서버 없이 대기열·매칭 화면 흐름만 보고 싶을 때 이 메뉴를 켠다.
    /// 값은 EditorPrefs — 이 PC 의 이 사람에게만 적용되고 저장소에는 올라가지 않는다.
    /// 바꾼 뒤에는 Play 를 다시 시작해야 한다 (서비스는 Play 시작 때 한 번 만들어진다).
    ///
    /// 매칭 테스트 씬(CommonMatchResultTest)은 이 스위치와 무관하게 스스로 Fake 를 세운다.
    /// </summary>
    public static class NetworkImplementationMenu
    {
        private const string MenuPath = "Tools/아라아띠/네트워크/에디터에서 Fake 네트워크 사용";

        [MenuItem(MenuPath, priority = 300)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(NetworkServiceBootstrap.EditorUseFakePrefKey, false);
            EditorPrefs.SetBool(NetworkServiceBootstrap.EditorUseFakePrefKey, next);
            Menu.SetChecked(MenuPath, next);

            Debug.Log(next
                ? "[Network] 에디터 Play 에서 FakeNetworkService 를 씁니다. (서버 없이 흐름 확인) — Play 를 다시 시작하세요."
                : "[Network] 에디터 Play 에서 FusionNetworkService 를 씁니다. (실제 서버) — Play 를 다시 시작하세요.");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(NetworkServiceBootstrap.EditorUseFakePrefKey, false));
            return !EditorApplication.isPlaying;
        }
    }
}

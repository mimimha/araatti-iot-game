using UnityEditor;
using UnityEngine;
using UnderTheSea.Character;

namespace UnderTheSea.Account.Editor
{
    /// <summary>
    /// 서버 외형 복원을 확인할 때 쓰는 에디터 메뉴.
    ///
    /// 기존 <c>Tools > 아라아띠 > 캐릭터 이름 지우기</c> 는 <c>PlayerNickname</c> **하나만** 지운다.
    /// 외형 캐시(<c>CharacterAppearanceSnapshotV1</c>)는 남아 있어서,
    /// "서버에서 외형이 복원되는지" 를 확인하려면 두 키를 **함께** 지워야 한다.
    ///
    /// ⚠ 지우는 것은 이 PC 의 캐시뿐이다.
    ///    서버 DB(MySQL)와 가짜 구현의 Fake.Account.* 데이터는 건드리지 않는다.
    /// </summary>
    public static class AccountDevMenu
    {
        private const string MenuRoot = "Tools/아라아띠/";

        [MenuItem(MenuRoot + "로컬 캐릭터 캐시 지우기 (서버 복원 확인용)")]
        private static void ClearLocalCache()
        {
            bool hadNickname = !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(SceneFlow.NicknameKey, string.Empty));
            bool hadAppearance = CharacterAppearanceStore.HasSaved;

            if (!hadNickname && !hadAppearance)
            {
                Debug.Log("[AccountDevMenu] 지울 로컬 캐시가 없습니다. 이미 비어 있습니다.");
                return;
            }

            CharacterSessionCache.ClearLocalCache("에디터 메뉴에서 직접 지웠습니다");

            Debug.Log(
                "[AccountDevMenu] 로컬 캐시를 지웠습니다. " +
                $"(이름 {(hadNickname ? "있었음" : "없었음")} / 외형 {(hadAppearance ? "있었음" : "없었음")})\n" +
                "이제 같은 계정으로 다시 로그인하면 서버 값으로 캐시가 다시 만들어집니다.\n" +
                "서버 DB 와 Fake.Account.* 데이터는 지우지 않았습니다.");
        }

        [MenuItem(MenuRoot + "로컬 캐릭터 캐시 보기")]
        private static void ShowLocalCache()
        {
            string nickname = PlayerPrefs.GetString(SceneFlow.NicknameKey, string.Empty);

            if (!CharacterAppearanceStore.TryLoad(out CharacterAppearanceSnapshot snapshot))
            {
                Debug.Log(
                    $"[AccountDevMenu] 저장된 외형 캐시가 없습니다. " +
                    $"({SceneFlow.NicknameKey} = \"{nickname}\")");
                return;
            }

            string parts = snapshot.parts.Length == 0
                ? "(없음)"
                : string.Join(", ", System.Array.ConvertAll(
                    snapshot.parts, part => part.slot + "=" + part.prefabName));

            Debug.Log(
                $"[AccountDevMenu] 로컬 캐시 상태\n" +
                $"  {SceneFlow.NicknameKey} : \"{nickname}\"\n" +
                $"  스냅샷 이름            : \"{snapshot.nickname}\"\n" +
                $"  피부색                 : {snapshot.bodyColorHex}\n" +
                $"  파츠 {snapshot.parts.Length}개            : {parts}");
        }
    }
}

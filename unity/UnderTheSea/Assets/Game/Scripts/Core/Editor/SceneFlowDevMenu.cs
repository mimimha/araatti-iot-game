using UnityEditor;
using UnityEngine;

/// <summary>
/// 개발 중에 화면 흐름을 테스트할 때 쓰는 에디터 메뉴.
///
/// 캐릭터를 한 번 만들면 SceneFlow 가 CharacterCreate 를 건너뛰기 때문에
/// (GAME_STRUCTURE.md 6장 "두 번째 실행부터") 생성 화면을 다시 보려면 저장된 이름을 지워야 한다.
/// </summary>
public static class SceneFlowDevMenu
{
    private const string MenuRoot = "Tools/아라아띠/";

    [MenuItem(MenuRoot + "캐릭터 이름 지우기 (CharacterCreate 다시 보기)")]
    private static void ClearCharacter()
    {
        string before = SceneFlow.Nickname;

        if (string.IsNullOrWhiteSpace(before))
        {
            Debug.Log("[SceneFlowDevMenu] 저장된 캐릭터 이름이 없습니다. 이미 CharacterCreate 가 나옵니다.");
            return;
        }

        SceneFlow.ClearCharacter();
        Debug.Log($"[SceneFlowDevMenu] \"{before}\" 를 지웠습니다. 다음 로그인부터 CharacterCreate 가 나옵니다.");
    }

    [MenuItem(MenuRoot + "튜토리얼 다시 보기")]
    private static void ClearTutorial()
    {
        // ⚠ "이미 나올 예정이면 아무것도 안 한다" 는 가드를 두지 않는다.
        //    그 판단이 틀렸을 때 눌러도 아무 일이 안 일어나 원인을 못 찾는다.
        //    실제로 그것 때문에 표시가 한 번도 안 세워졌다. 누르면 늘 세운다.
        LobbyTutorial.ClearSeen();
        Debug.Log("[SceneFlowDevMenu] 다음 로비 입장에서 튜토리얼이 나옵니다.");
    }

    [MenuItem(MenuRoot + "오프닝 영상 다시 보기")]
    private static void ClearOpeningVideo()
    {
        // 튜토리얼 다시 보기와 같은 이유로 가드를 두지 않는다. 누르면 늘 세운다.
        UnderTheSea.Lobby.OpeningVideo.ClearSeen();
        Debug.Log("[SceneFlowDevMenu] 다음 로비 입장에서 오프닝 영상이 나옵니다. (Resources/OpeningVideo 영상이 있을 때)");
    }

    [MenuItem(MenuRoot + "저장된 캐릭터 이름 보기")]
    private static void ShowCharacter()
    {
        Debug.Log(SceneFlow.HasCharacter
            ? $"[SceneFlowDevMenu] 저장된 캐릭터 이름: \"{SceneFlow.Nickname}\" → 로그인하면 ChannelSelect 로 바로 갑니다."
            : "[SceneFlowDevMenu] 저장된 캐릭터 이름이 없습니다. → 로그인하면 CharacterCreate 로 갑니다.");
    }
}

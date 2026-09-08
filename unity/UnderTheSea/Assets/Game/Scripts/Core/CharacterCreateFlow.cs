using UnityEngine;
using UnderTheSea.Character;

/// <summary>
/// CharacterCreate 씬에서 [생성 완료] 이후 다음 화면으로 넘겨주는 연결 담당.
///
/// 캐릭터 생성 UI(CharacterCustomizationController)는 이름 검증과 저장까지만 하고
/// 씬 전환은 하지 않는다. (GAME_STRUCTURE.md 3장 · 6장)
/// 그 "끝났다" 알림을 여기서 받아 SceneFlow 로 넘긴다.
///
/// 사용법
///   1. CharacterCreate 씬에 빈 오브젝트를 만든다. (이름: CharacterCreateFlow)
///   2. 이 스크립트를 붙인다.
///   3. 끝. 씬에 캐릭터 생성 UI 가 하나뿐이면 자동으로 찾는다.
/// </summary>
public class CharacterCreateFlow : MonoBehaviour
{
    [Header("캐릭터 생성 UI")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다. 씬에 두 개 이상 있을 때만 직접 지정한다.")]
    [SerializeField] private CharacterCustomizationController controller;

    private void Awake()
    {
        if (controller == null)
        {
            controller = FindAnyObjectByType<CharacterCustomizationController>(FindObjectsInactive.Include);
        }

        if (controller == null)
        {
            Debug.LogError(
                "[CharacterCreateFlow] 씬에서 캐릭터 생성 UI 를 찾지 못했습니다. " +
                "CharacterCustomization 프리팹이 씬에 올려져 있는지 확인해 주세요.", this);
        }
    }

    private void OnEnable()
    {
        if (controller != null) controller.Completed += HandleCompleted;
    }

    private void OnDisable()
    {
        if (controller != null) controller.Completed -= HandleCompleted;
    }

    /// <summary>이름 검증을 통과하고 저장까지 끝났을 때 호출된다.</summary>
    private void HandleCompleted(string nickname)
    {
        Debug.Log($"[CharacterCreateFlow] 캐릭터 생성 완료: \"{nickname}\"");
        SceneFlow.FromCharacterCreate();
    }
}

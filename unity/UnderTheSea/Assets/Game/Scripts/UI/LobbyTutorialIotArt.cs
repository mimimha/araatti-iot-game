using UnityEngine;

/// <summary>
/// 로비 튜토리얼의 **완드용 그림**. <c>Resources/LobbyTutorial.prefab</c> 루트에 붙어 있다.
///
/// <see cref="LobbyTutorial"/> 은 실행 중에 <c>AddComponent</c> 로 생겨서 인스펙터로 그림을 꽂을
/// 수가 없다. 그래서 그림은 그것이 띄우는 프리팹 쪽이 들고 있고, 튜토리얼이 이것을 읽어
/// 완드가 붙어 있을 때 각 단계의 <c>Art</c> 그림을 바꿔 끼운다.
///
/// 비어 있는 칸은 키보드 그림을 그대로 둔다. 그림은 작가 문서가 최종본으로 정한
/// <c>Art/UI/Tutorial/icon-iot-*-fit-v2.png</c> 이다. (art/ui-assets/iot-tutorial-generation.md)
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyTutorialIotArt : MonoBehaviour
{
    [Tooltip("이동 단계. 왼손 스틱을 강조한 그림.")]
    [SerializeField] private Sprite move;

    [Tooltip("둘러보기 단계. 오른손 스틱을 강조한 그림.")]
    [SerializeField] private Sprite look;

    [Tooltip("점프 단계. 오른손 A(버튼 1)를 강조한 그림.")]
    [SerializeField] private Sprite jump;

    public Sprite Move => move;
    public Sprite Look => look;
    public Sprite Jump => jump;
}

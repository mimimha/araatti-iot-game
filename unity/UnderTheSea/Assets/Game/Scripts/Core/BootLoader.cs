using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boot 씬에 하나만 놓아두면, 잠깐 기다렸다가 다음 씬으로 자동으로 넘어간다.
///
/// 사용법
///   1. Boot 씬에 빈 오브젝트를 만든다. (이름: BootLoader)
///   2. 이 스크립트를 붙인다.
///   3. 끝. 버튼은 필요 없다.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [Header("다음에 열 씬 이름")]
    [SerializeField] private string nextScene = "Title";

    [Header("몇 초 뒤에 넘어갈지")]
    [SerializeField] private float delaySeconds = 1f;

    private void Start()
    {
        Debug.Log("Boot: 초기화 시작");

        // TODO: 나중에 여기에서 게임 설정 불러오기, 매니저 생성 등을 한다.

        // delaySeconds 초 뒤에 GoToNextScene() 을 실행한다.
        Invoke(nameof(GoToNextScene), delaySeconds);
    }

    private void GoToNextScene()
    {
        if (string.IsNullOrEmpty(nextScene))
        {
            Debug.LogError("[BootLoader] 다음 씬 이름이 비어 있습니다.", this);
            return;
        }

        Debug.Log($"Boot: 초기화 완료 → {nextScene}");
        SceneManager.LoadScene(nextScene);
    }
}

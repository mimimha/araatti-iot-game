using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 버튼에 붙여두면, 눌렀을 때 정해둔 씬으로 넘어간다.
///
/// 사용법
///   1. 버튼 오브젝트에 이 스크립트를 붙인다.
///   2. Inspector 의 "이동할 씬 이름" 칸에 씬 이름을 적는다.
///   3. 버튼의 On Click () 에 이 스크립트의 OnClick 을 연결한다.
/// </summary>
public class SceneChangeButton : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    [Tooltip("Build Profiles 의 Scene List 에 등록된 이름과 똑같이 적는다.")]
    [SerializeField] private string sceneName;

    /// <summary>
    /// 버튼을 눌렀을 때 호출된다.
    /// </summary>
    public void OnClick()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[{name}] 이동할 씬 이름이 비어 있습니다.", this);
            return;
        }

        Debug.Log($"씬 이동 요청: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}

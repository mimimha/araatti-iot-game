using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionLauncher : MonoBehaviour
{
    [SerializeField] private string roomName = "AraAtti-Test";

    private async void Start()
    {
        NetworkRunner runner = GetComponent<NetworkRunner>();

        runner.ProvideInput = true;

        // 로컬 입력 제공자를 등록한다. 컴포넌트가 없으면 자동으로 붙여서 Inspector 작업을 줄인다.
        PlayerInputProvider inputProvider = GetComponent<PlayerInputProvider>();
        if (inputProvider == null)
            inputProvider = gameObject.AddComponent<PlayerInputProvider>();

        runner.AddCallbacks(inputProvider);

        // buildIndex가 -1이면 이 씬이 Build Settings에 없다는 뜻이라 Fusion이 씬을 넘겨줄 수 없다.
        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            Debug.LogError($"[Fusion] '{SceneManager.GetActiveScene().name}' 씬이 Build Settings에 등록되어 있지 않아 접속을 중단한다.");
            return;
        }

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = roomName,
            Scene = SceneRef.FromIndex(buildIndex),
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log($"[Fusion] '{roomName}' 방 접속 성공");
        }
        else
        {
            Debug.LogError($"Fusion 접속 실패: {result.ShutdownReason}");
        }
    }
}
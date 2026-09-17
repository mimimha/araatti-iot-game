using System.Linq;
using MiniGames.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// Lobby 에 **미니게임 입구와 공통 로딩 화면**을 놓는다. 한 번 눌러 만드는 도구다.
    ///
    /// 손으로 놓으면 "내 유니티에서는 되는데" 가 생긴다. 무엇을 어디에 놓았는지 코드로 남긴다.
    ///
    /// <code>
    ///   Tools/아라아띠/Lobby 에 미니게임 입구 놓기
    ///     1. CommonMatchCanvas   공통 UI 프리팹. 이번 단계에서는 로딩 화면만 쓴다
    ///     2. ShipCoopPortal      MiniGame_Ship 설정을 든 입구. 바닥 위에 올라간다
    /// </code>
    ///
    /// 여러 번 눌러도 같은 결과가 나온다.
    /// </summary>
    public static class LobbyPortalSetup
    {
        private const string MenuRoot = "Tools/아라아띠/";

        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";
        private const string CanvasPrefabPath = "Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab";
        private const string ShipConfigPath = "Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_Ship.asset";

        /// <summary>
        /// 이미 있는 포탈 연출. <c>ProximityPortal</c> 과 파랑 이펙트 3종이 배선돼 있다.
        /// **새 아트를 만들지 않는다.**
        /// </summary>
        private const string PortalVisualPath = "Assets/Game/Prefabs/Portal/P_Portal_Blue_01.prefab";

        private const string CanvasName = "CommonMatchCanvas";
        private const string PortalName = "ShipCoopPortal";

        /// <summary>
        /// 입구를 놓을 자리. <b>높이는 적지 않는다</b> — 바닥을 찾아 그 위에 올린다.
        ///
        /// Lobby 의 스폰 지점은 원점이 아니라 <c>x 17~25 / z 48~53</c> 부근이고 바닥도
        /// <c>y ≈ 1~3</c> 으로 기울어 있다. 그래서 원점 근처에 두면 접속하자마자
        /// <b>45m 밖에 있어 보이지도 않는다.</b> 실제로 그렇게 되어 있었다.
        ///
        /// 여기 적은 자리는 스폰 줄과 같은 <c>z</c> 선상에서 <c>x</c> 로만 떨어진 곳이다.
        /// 지형을 재 보니 평평한 <c>Terrain</c> 이고 가장 가까운 스폰에서 4m 남짓이라
        /// 나오자마자 눈에 들어온다.
        /// </summary>
        private static readonly Vector2 PortalSpot = new Vector2(12.8f, 49f);

        /// <summary>바닥을 못 찾았을 때 쓸 높이. 스폰 지점들의 높이대다.</summary>
        private const float FallbackHeight = 1f;

        [MenuItem(MenuRoot + "Lobby 에 미니게임 입구 놓기")]
        public static void BuildAll()
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            if (!PlaceCommonCanvas(scene)) return;
            if (!PlacePortal(scene)) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[LobbyPortalSetup] Lobby 에 미니게임 입구를 놓았습니다.");
        }

        /// <summary>배치 빌드에서 부르는 입구. 컴파일 확인에도 쓴다.</summary>
        public static void BuildAllFromCommandLine()
        {
            BuildAll();
        }

        /// <summary>
        /// 공통 UI 프리팹을 놓는다.
        ///
        /// ⚠ 이번 단계에서는 **로딩 화면만** 쓴다. 매칭 판은 인원을 채워 줄 대기열 서비스가
        ///    없어 "0 / 4명" 이 멈춰 있게 되므로 띄우지 않는다. 그 판단은 포탈이 한다 —
        ///    <c>ShowQueueLoading</c> 만 부르고 <c>Show</c> 는 부르지 않는다.
        /// </summary>
        private static bool PlaceCommonCanvas(Scene scene)
        {
            GameObject existing = Find(scene, CanvasName);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[LobbyPortalSetup] '{CanvasPrefabPath}' 을 찾지 못했습니다.");
                return false;
            }

            GameObject canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            canvas.name = CanvasName;

            TurnOffTestDefaults(canvas);

            Debug.Log($"[LobbyPortalSetup] 공통 UI 를 놓았습니다 — {CanvasName}");
            return true;
        }

        /// <summary>
        /// 프리팹의 **테스트용 기본값**을 끈다. 연동 문서가 실제 씬에서 바꾸라고 적어 둔 값들이다.
        ///
        /// <code>
        ///   MatchFlowController.suppressSceneLoad   true → false   실제로 씬을 연다
        ///   MatchFlow.joinLocalPlayerOnStart        true → false   가짜 사람을 넣지 않는다
        ///   MatchFlow.autoStart                     true → false   30초 자동 시작을 쓰지 않는다
        ///   SceneTransitionService.stubOnly         true → false   로그만 남기지 않는다
        ///   CommonMatchingUI.showOnStart            true → false   포탈이 열 때까지 숨어 있는다
        /// </code>
        /// </summary>
        private static void TurnOffTestDefaults(GameObject canvas)
        {
            SetBool(canvas.GetComponentInChildren<MatchFlowController>(true), "suppressSceneLoad", false);
            SetBool(canvas.GetComponentInChildren<MatchFlow>(true), "joinLocalPlayerOnStart", false);
            SetBool(canvas.GetComponentInChildren<MatchFlow>(true), "autoStart", false);
            SetBool(canvas.GetComponentInChildren<SceneTransitionService>(true), "stubOnly", false);
            SetBool(canvas.GetComponent<CommonMatchingUI>(), "showOnStart", false);
        }

        private static void SetBool(Component target, string field, bool value)
        {
            if (target == null)
            {
                Debug.LogWarning($"[LobbyPortalSetup] '{field}' 를 가진 부품을 찾지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"[LobbyPortalSetup] '{target.GetType().Name}' 에 '{field}' 가 없습니다.");
                return;
            }

            property.boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[LobbyPortalSetup] {target.GetType().Name}.{field} = {value}");
        }

        /// <summary>
        /// 미니게임 입구를 놓는다. 어느 게임인지는 설정 에셋이 정한다.
        ///
        /// <b>한 오브젝트에 부품 둘이 붙지만 하는 일은 겹치지 않는다.</b>
        /// <code>
        ///   ProximityPortal   보이는 것만 맡는다 — 가까우면 열리고 멀어지면 닫힌다
        ///   MiniGamePortal    F 입력과 실제 입장만 맡는다
        /// </code>
        /// 같은 오브젝트에 두는 이유는 <b>자리를 하나로 묶기 위해서</b>다. 따로 놓으면
        /// 눈에 보이는 문과 실제로 눌리는 자리가 어긋난다.
        ///
        /// 거리도 일부러 다르게 둔다 — 문은 6m 에서 열리고(<c>openDistance</c>),
        /// 들어갈 수 있는 것은 4m 부터다(<c>interactDistance</c>). 문이 먼저 보이고
        /// 다가가면 들어갈 수 있게 되는 순서다.
        /// </summary>
        private static bool PlacePortal(Scene scene)
        {
            GameObject existing = Find(scene, PortalName);
            if (existing != null) Object.DestroyImmediate(existing);

            MiniGameConfig config = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(ShipConfigPath);

            if (config == null)
            {
                Debug.LogError($"[LobbyPortalSetup] '{ShipConfigPath}' 을 찾지 못했습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.SceneName))
            {
                Debug.LogError(
                    $"[LobbyPortalSetup] '{config.DisplayName}' 의 SceneName 이 비어 있습니다. " +
                    "설정 에셋을 채워 주세요.");
                return false;
            }

            // 이미 있는 연출 프리팹을 그대로 쓴다. 새 아트를 만들지 않는다.
            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(PortalVisualPath);

            if (visual == null)
            {
                Debug.LogError($"[LobbyPortalSetup] '{PortalVisualPath}' 을 찾지 못했습니다.");
                return false;
            }

            GameObject portal = (GameObject)PrefabUtility.InstantiatePrefab(visual, scene);
            portal.name = PortalName;
            portal.transform.position = SnapToGround(PortalSpot);

            // 연출은 프리팹이 이미 들고 있다. 여기서는 입장 담당만 얹는다.
            MiniGamePortal entry = portal.GetComponent<MiniGamePortal>();
            if (entry == null) entry = portal.AddComponent<MiniGamePortal>();

            SerializedObject data = new SerializedObject(entry);
            data.FindProperty("config").objectReferenceValue = config;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);

            ProximityPortal look = portal.GetComponent<ProximityPortal>();

            if (look == null)
            {
                Debug.LogWarning(
                    $"[LobbyPortalSetup] '{PortalVisualPath}' 에 ProximityPortal 이 없습니다. " +
                    "문은 보이지 않고 입장만 됩니다.");
            }

            Debug.Log(
                $"[LobbyPortalSetup] 입구를 놓았습니다 — {PortalName} {portal.transform.position}, " +
                $"'{config.DisplayName}' → 씬 '{config.SceneName}'. " +
                $"연출 {(look != null ? "있음" : "없음")}");

            return true;
        }

        /// <summary>
        /// 가로세로 자리만 받아 <b>바닥 위</b>로 올린다.
        ///
        /// 높이를 손으로 적으면 지형이 조금만 바뀌어도 포탈이 공중에 뜨거나 땅에 묻힌다.
        /// 위에서 아래로 한 번 쏴서 닿는 곳에 놓으면 그런 일이 없다.
        ///
        /// ⚠ 씬을 연 <b>에디터</b>에서만 맞는 방법이다. 실행 중에는 부르지 않는다.
        /// </summary>
        private static Vector3 SnapToGround(Vector2 spot)
        {
            Vector3 from = new Vector3(spot.x, FallbackHeight + 8f, spot.y);

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f))
            {
                Debug.Log(
                    $"[LobbyPortalSetup] 바닥을 찾았습니다 — '{hit.collider.name}' y {hit.point.y:F2}");

                return new Vector3(spot.x, hit.point.y, spot.y);
            }

            Debug.LogWarning(
                $"[LobbyPortalSetup] ({spot.x}, {spot.y}) 아래에서 바닥을 찾지 못했습니다. " +
                $"높이 {FallbackHeight} 로 둡니다. 포탈이 떠 보이면 PortalSpot 을 옮겨 주세요.");

            return new Vector3(spot.x, FallbackHeight, spot.y);
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;

                foreach (Transform step in root.GetComponentsInChildren<Transform>(true))
                    if (step.name == name) return step.gameObject;
            }

            return null;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using ithappy.Cute_Characters.Controller;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mine.Net;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 광산 네트워크 씬과 플레이어 프리팹을 만든다. **한 번 눌러 만드는 도구다.**
    ///
    /// 손으로 만들면 "내 유니티에서는 되는데" 가 생긴다. 효진님이 같은 결과를 다시 만들 수
    /// 있도록 무엇을 지우고 무엇을 붙였는지를 코드로 남긴다.
    ///
    /// <b>원본은 건드리지 않는다.</b>
    /// <c>Scenes/Develop/HyoJin/MineTest.unity</c> 는 읽기만 한다.
    ///
    /// <code>
    ///   Tools/아라아띠/광산 네트워크 씬 만들기
    ///     1. MineNetPlayer.prefab   P_JaeYoung 을 바탕으로 네트워크 플레이어를 만든다
    ///     2. MineNet.unity          MineTest 를 복사해 네트워크용으로 고친다
    ///     3. MineBoot.unity         세션만 여는 빈 씬
    ///     4. Scene List             서버와 클라이언트가 같은 경로 두 개를 갖도록 등록한다
    /// </code>
    ///
    /// 여러 번 눌러도 같은 결과가 나온다.
    /// </summary>
    public static class MineSceneSetup
    {
        private const string MenuRoot = "Tools/아라아띠/";

        private const string SourceScenePath = "Assets/Game/Scenes/Develop/HyoJin/MineTest.unity";
        private const string SourcePlayerPrefab = "Assets/Game/Prefabs/Characters/P_JaeYoung.prefab";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/MiniGames/Mine/MineNetPlayer.prefab";

        /// <summary>씬 인스턴스에 맞춘 회전 속도. 프리팹 기본값(90)은 너무 느리다.</summary>
        private const float TunedRotateSpeed = 720f;

        /// <summary>씬 인스턴스에 맞춘 점프 높이. 프리팹 기본값(5)은 너무 높다.</summary>
        private const float TunedJumpHeight = 0.2f;

        [MenuItem(MenuRoot + "광산 네트워크 씬 만들기")]
        public static void BuildAll()
        {
            GameObject prefab = BuildPlayerPrefab();
            if (prefab == null) return;

            BuildGameScene();
            BuildBootScene(prefab);

            RegisterScene(MineNet.BootScenePath);
            RegisterScene(MineNet.ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MineSceneSetup] 완료했습니다.");
        }

        /// <summary>배치 빌드에서 부르는 입구. 컴파일 확인에도 쓴다.</summary>
        public static void BuildAllFromCommandLine()
        {
            BuildAll();
        }

        // ------------------------------------------------------------
        // 1. 플레이어 프리팹
        // ------------------------------------------------------------

        /// <summary>
        /// 네트워크용 광산 플레이어를 만든다.
        ///
        /// ⚠ 효진님의 <c>P_JaeYoung.prefab</c> 을 <b>고치지 않는다.</b> 로비도 쓰는
        ///    공용 캐릭터라, 여기에 <c>NetworkObject</c> 를 달면 로비에 스폰되지 않은
        ///    네트워크 오브젝트가 생긴다.
        /// </summary>
        private static GameObject BuildPlayerPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayerPrefab);

            if (source == null)
            {
                Debug.LogError($"[MineSceneSetup] '{SourcePlayerPrefab}' 을 찾지 못했습니다.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "MineNetPlayer";

            StripLocalOnlyParts(root);
            TuneMover(root);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<MineNetPlayer>();
            root.AddComponent<MineNetPlayerMover>();
            root.AddComponent<MineLocalView>();

            // 채굴 · 되메우기 · 힌트. 판정은 서버에서만 일어난다.
            root.AddComponent<MineNetPlayerActions>();

            // 돌이 깨질 때 폴짝 뛰는 연출. 원본 씬의 플레이어에도 붙어 있다.
            if (root.GetComponent<MineJump>() == null) root.AddComponent<MineJump>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ⚠ 저장 뒤에는 **경로에서 다시 읽어야** 한다. 위의 saved 는 임시 인스턴스라
            //    그대로 씬에 꽂으면 참조가 끊긴다. Warriors 에서 실측한 함정이다.
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Debug.Log($"[MineSceneSetup] 네트워크 플레이어 프리팹을 만들었습니다 - {PlayerPrefabPath}");
            return asset;
        }

        /// <summary>내 키보드로 직접 움직이는 부품을 뗀다. 서버가 대신 넣는다.</summary>
        private static void StripLocalOnlyParts(GameObject root)
        {
            int removed = 0;

            foreach (MovePlayerInput stock in root.GetComponentsInChildren<MovePlayerInput>(true))
            {
                Object.DestroyImmediate(stock);
                removed++;
            }

            foreach (MineMoveInput local in root.GetComponentsInChildren<MineMoveInput>(true))
            {
                Object.DestroyImmediate(local);
                removed++;
            }

            if (removed > 0) Debug.Log($"[MineSceneSetup] 로컬 전용 입력 {removed}개를 뗐습니다.");
        }

        /// <summary>
        /// 씬 인스턴스에 맞춰 회전 속도를 올린다.
        ///
        /// <c>MineMoveInput</c> 주석이 <c>Space = Self</c> 와 <c>RotateSpeed &gt; 0</c> 을
        /// 요구한다. 프리팹은 Self 는 맞는데 속도가 90 이라 보는 쪽으로 도는 것이 굼뜨다.
        /// 효진님이 씬에서 720 으로 올려 두었으므로 같은 값을 쓴다.
        /// </summary>
        private static void TuneMover(GameObject root)
        {
            CharacterMover mover = root.GetComponent<CharacterMover>();
            if (mover == null) return;

            SerializedObject data = new SerializedObject(mover);

            SerializedProperty space = data.FindProperty("m_Space");
            if (space != null) space.enumValueIndex = (int)Space.Self;

            SerializedProperty rotate = data.FindProperty("m_RotateSpeed");
            if (rotate != null) rotate.floatValue = TunedRotateSpeed;

            // ⚠ 효진님이 **씬 인스턴스에서** 덮어쓴 값이다. 프리팹 기본값은 5 라
            //    그대로 두면 한 번 눌러도 사람 키만큼 솟구친다. 실측으로 확인했다.
            SerializedProperty jump = data.FindProperty("m_JumpHeight");
            if (jump != null) jump.floatValue = TunedJumpHeight;

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------
        // 2. 게임 씬
        // ------------------------------------------------------------

        private static void BuildGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(Path.GetDirectoryName(MineNet.ScenePath));

            // ⚠ 먼저 **다른 이름으로 저장한 뒤** 고친다. 순서를 바꾸면 원본이 더럽혀진다.
            if (!EditorSceneManager.SaveScene(scene, MineNet.ScenePath, true))
            {
                Debug.LogError($"[MineSceneSetup] '{MineNet.ScenePath}' 저장에 실패했습니다.");
                return;
            }

            scene = EditorSceneManager.OpenScene(MineNet.ScenePath, OpenSceneMode.Single);

            HoldRulesForLaterSteps(scene);
            RemoveLocalPlayer(scene);
            CreateSpawnPoints(scene);
            CreateNetworkParts(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[MineSceneSetup] 네트워크 게임 씬을 만들었습니다 - {MineNet.ScenePath}");
        }

        /// <summary>
        /// 규칙을 잠시 꺼 둔다. **1단계 한정이다.**
        ///
        /// <c>MineGame</c> 은 <c>autoStart</c> 로 재생하자마자 판을 시작하고, 인스펙터에
        /// 꽂힌 <c>diggers</c> 배열로 턴을 돌린다. 접속자가 그때그때 생기는 네트워크와는
        /// 모양이 맞지 않아 <c>MineMatchState</c> 가 대신한다.
        ///
        /// <c>MineDigger</c> 도 끈다. 그쪽은 <c>IPlayerController</c> 에서 직접 키를 읽어
        /// <b>자기 화면에서</b> 격자를 고친다. 네트워크에서는 <c>MineNetPlayerActions</c> 가
        /// 그 자리를 대신하고, 격자 계산은 서버에서 한 번만 일어난다.
        /// </summary>
        private static void HoldRulesForLaterSteps(Scene scene)
        {
            int held = 0;

            held += Disable(FindComponent<MineGame>(scene), "판 진행");
            held += Disable(FindComponent<MineDigger>(scene), "채굴");

            Debug.Log($"[MineSceneSetup] 규칙 {held}개를 1단계 동안 꺼 두었습니다. (2단계에서 켠다)");
        }

        private static int Disable(UnityEngine.Behaviour target, string label)
        {
            if (target == null) return 0;

            target.enabled = false;
            Debug.Log($"[MineSceneSetup] '{label}' 를 껐습니다.");
            return 1;
        }

        /// <summary>
        /// 씬에 미리 놓인 플레이어를 치운다.
        ///
        /// 네트워크에서는 <b>서버가</b> 사람마다 하나씩 스폰한다. 그대로 두면 아무도
        /// 조작하지 않는 유령이 한 명 서 있게 된다.
        ///
        /// 지우지 않고 꺼 두기만 한다 — 자리와 설정이 남아 있어야 나중에 비교할 수 있다.
        /// </summary>
        private static void RemoveLocalPlayer(Scene scene)
        {
            MineDigger digger = FindComponent<MineDigger>(scene);
            if (digger == null) return;

            CharacterMover mover = digger.GetComponentInParent<CharacterMover>();
            GameObject body = mover != null ? mover.gameObject : digger.gameObject;

            body.SetActive(false);
            Debug.Log($"[MineSceneSetup] 씬에 있던 플레이어 '{body.name}' 를 껐습니다. 서버가 스폰합니다.");
        }

        /// <summary>
        /// P1~P4 가 설 자리를 만든다.
        ///
        /// 원본 씬에는 자리 표식이 하나도 없다(플레이어가 한 명뿐이었다). 격자 한가운데
        /// 근처에 네 자리를 나란히 둔다 — 릴레이라 서로 멀 이유가 없고, 첫 사람이
        /// 시작 칸을 정하면 나머지는 그 자리에서 이어받는다. (MINE.md 3장)
        ///
        /// 자리는 <c>Transform</c> 이므로 씬에서 끌어다 옮기면 그대로 반영된다.
        /// </summary>
        private static void CreateSpawnPoints(Scene scene)
        {
            GameObject existing = Find(scene, "MineSpawnPoints");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject root = new GameObject("MineSpawnPoints");
            SceneManager.MoveGameObjectToScene(root, scene);

            // 격자 한가운데를 기준으로 삼는다. 없으면 원점.
            MineGrid grid = FindComponent<MineGrid>(scene);
            Vector3 center = grid != null
                ? grid.CellToWorld(grid.Size / 2, grid.Size / 2)
                : Vector3.zero;

            root.transform.position = center;

            for (int i = 0; i < MineNet.MaxCrew; i++)
            {
                GameObject spot = new GameObject($"MineSpawn_{i + 1:00}");
                SceneManager.MoveGameObjectToScene(spot, scene);
                spot.transform.SetParent(root.transform, false);

                // 가운데를 중심으로 좌우로 1.5m 씩 벌려 세운다.
                //
                // ⚠ 높이 2m 는 **일부러 띄운 것이다.** 사람이 다 모일 때까지는 몸이
                //   숨은 채 중력도 멈춰 있다가(MineNetPlayer.ShowBody), 카운트다운 "3" 과
                //   함께 넷이 한꺼번에 떨어진다. 그 낙하가 판이 열렸다는 신호다.
                //   0.5m 로는 0.32초라 카메라가 탑뷰에서 내려오는 사이에 끝나 안 보였다.
                //
                //   ⚠ **씬의 값과 같이 움직여야 한다.** 이 도구는 MineSpawnPoints 를
                //     통째로 지우고 다시 만들므로, 씬에서 손으로 옮겨 둔 높이는 여기서
                //     덮인다. 한쪽만 고치면 다음 재생성 때 조용히 되돌아간다.
                spot.transform.localPosition = new Vector3((i - (MineNet.MaxCrew - 1) * 0.5f) * 1.5f, 2f, 0f);
                spot.transform.localRotation = Quaternion.identity;

                spot.AddComponent<MineSpawnPoint>();
            }

            Debug.Log($"[MineSceneSetup] 플레이어 자리 {MineNet.MaxCrew}곳을 만들었습니다. (중심 {center})");
        }

        /// <summary>
        /// 쓸 수 있는 목표 도안을 모은다.
        ///
        /// 서버가 시드로 하나를 골라 번호를 복제하고, 모두가 같은 번호로 같은 그림을 건다.
        /// 순서가 컴퓨터마다 달라지면 안 되므로 이름순으로 줄을 세운다.
        /// </summary>
        private static MineDrawingTarget[] LoadDrawings()
        {
            string[] found = AssetDatabase.FindAssets("t:MineDrawingTarget");

            MineDrawingTarget[] list = found
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<MineDrawingTarget>)
                .Where(one => one != null)
                .ToArray();

            Debug.Log($"[MineSceneSetup] 목표 도안 {list.Length}개를 찾았습니다 - " +
                      string.Join(", ", list.Select(one => one.displayName)));

            return list;
        }

        /// <summary>게임 씬 쪽 네트워크 부품. 서버 정리와 <b>매치 공통 상태</b>.</summary>
        private static void CreateNetworkParts(Scene scene)
        {
            GameObject existing = Find(scene, "MineNetwork");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject manager = new GameObject("MineNetwork");
            SceneManager.MoveGameObjectToScene(manager, scene);

            manager.AddComponent<MineServerCleanup>();

            // 판 전체의 공통 상태(대기 · 카운트다운 · 턴)를 복제한다.
            // 씬에 미리 놓인 NetworkObject 라, Fusion 이 이 씬을 열 때 함께 등록한다.
            manager.AddComponent<NetworkObject>();
            manager.AddComponent<MineMatchState>();

            // 격자 한 장을 서버가 계산하고 모두가 같은 것을 본다.
            MineGridSync board = manager.AddComponent<MineGridSync>();
            board.EditorSetDrawings(LoadDrawings());
            EditorUtility.SetDirty(board);

            Debug.Log("[MineSceneSetup] 씬에 MineNetwork 를 만들었습니다.");
        }

        // ------------------------------------------------------------
        // 3. 시작 씬
        // ------------------------------------------------------------

        /// <summary>
        /// 세션만 여는 씬. **거의 비어 있어야 한다.**
        ///
        /// ⚠ <c>PeerMode.Multiple</c> 에서 Fusion 은 <c>StartGameArgs.Scene</c> 을
        ///    러너 전용 씬으로 새로 로드한다. 게임 씬에서 세션을 시작하면 동굴도 격자도
        ///    두 벌이 된다. ShipCoop 에서 실측한 함정이다.
        /// </summary>
        private static void BuildBootScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject manager = new GameObject("MineNetworkRunner");
            SceneManager.MoveGameObjectToScene(manager, scene);

            manager.AddComponent<NetworkRunner>();
            manager.AddComponent<NetworkSceneManagerDefault>();
            manager.AddComponent<MineLauncher>();
            manager.AddComponent<MineInputProvider>();

            MinePlayerSpawner spawner = manager.AddComponent<MinePlayerSpawner>();

            NetworkObject body = playerPrefab != null ? playerPrefab.GetComponent<NetworkObject>() : null;

            SerializedObject data = new SerializedObject(spawner);
            data.FindProperty("playerPrefab").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);

            // 게임 씬이 뜨기 전까지 화면이 까맣지 않도록 카메라 하나만 둔다.
            GameObject camera = new GameObject("BootCamera");
            SceneManager.MoveGameObjectToScene(camera, scene);
            camera.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.AddComponent<AudioListener>();

            Directory.CreateDirectory(Path.GetDirectoryName(MineNet.BootScenePath));
            EditorSceneManager.SaveScene(scene, MineNet.BootScenePath);

            Debug.Log($"[MineSceneSetup] 시작 씬을 만들었습니다 - {MineNet.BootScenePath}");
        }

        // ------------------------------------------------------------
        // 4. Scene List
        // ------------------------------------------------------------

        private static void RegisterScene(string path)
        {
            List<EditorBuildSettingsScene> list = EditorBuildSettings.scenes.ToList();

            if (list.Any(one => one.path == path))
            {
                foreach (EditorBuildSettingsScene one in list)
                    if (one.path == path) one.enabled = true;

                EditorBuildSettings.scenes = list.ToArray();
                return;
            }

            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();

            Debug.Log($"[MineSceneSetup] Scene List 에 '{path}' 를 등록했습니다.");
        }

        // ------------------------------------------------------------

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

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }
    }
}

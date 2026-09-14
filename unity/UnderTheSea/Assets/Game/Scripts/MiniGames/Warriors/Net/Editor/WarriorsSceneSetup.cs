using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Warriors.Net;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// Warriors 네트워크 씬과 플레이어 프리팹을 만든다. **한 번 눌러 만드는 도구다.**
    ///
    /// 손으로 만들면 "내 유니티에서는 되는데" 가 생긴다. 서연님이 같은 결과를 다시 만들 수 있게
    /// 무엇을 지우고 무엇을 붙였는지를 코드로 남긴다.
    ///
    /// <b>원본은 건드리지 않는다.</b>
    /// <c>Scenes/Main/MiniGames/Warriors.unity</c> 와
    /// <c>Scenes/Develop/SeoYeon/WarriorsTest.unity</c> 는 읽기만 한다.
    ///
    /// <code>
    ///   Tools/아라아띠/Warriors 네트워크 씬 만들기
    ///     1. WarriorsNetPlayer.prefab   StandalonePlayer 를 바탕으로 네트워크 플레이어를 만든다
    ///     2. WarriorsNet.unity          Warriors.unity 를 복사해 네트워크용으로 고친다
    ///     3. WarriorsBoot.unity         세션만 여는 빈 씬
    ///     4. Scene List                 서버와 클라이언트가 같은 경로 두 개를 갖도록 등록한다
    /// </code>
    ///
    /// 여러 번 눌러도 같은 결과가 나온다.
    /// </summary>
    public static class WarriorsSceneSetup
    {
        private const string MenuRoot = "Tools/아라아띠/";

        private const string SourceScenePath = "Assets/Game/Scenes/Main/MiniGames/Warriors.unity";
        private const string SourcePlayerPrefab =
            "Assets/Game/Prefabs/MiniGames/Warriors/Player/WarriorsStandalonePlayer.prefab";
        private const string PlayerPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/Player/WarriorsNetPlayer.prefab";

        private const string EnemyFolder = "Assets/Game/Prefabs/MiniGames/Warriors/Enemies/";

        /// <summary>네트워크 사본을 만들 몬스터. 스포너의 표에 들어 있는 것들이다.</summary>
        private static readonly string[] EnemyNames = { "FishEnemy", "CrabEnemy", "JellyfishEnemy" };

        [MenuItem(MenuRoot + "Warriors 네트워크 씬 만들기")]
        public static void BuildAll()
        {
            GameObject prefab = BuildPlayerPrefab();

            if (prefab == null) return;

            WarriorsEnemyDirector.EnemyPair[] enemies = BuildEnemyPrefabs();

            BuildGameScene(enemies);
            BuildBootScene(prefab);

            RegisterScene(WarriorsNet.BootScenePath);
            RegisterScene(WarriorsNet.ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[WarriorsSceneSetup] 완료했습니다.\n" +
                $"  프리팹    {PlayerPrefabPath}\n" +
                $"  게임 씬   {WarriorsNet.ScenePath}\n" +
                $"  시작 씬   {WarriorsNet.BootScenePath}");
        }

        /// <summary>커맨드라인용.</summary>
        public static void BuildAllFromCommandLine()
        {
            BuildAll();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------
        // 1. 플레이어 프리팹
        // ------------------------------------------------------------

        /// <summary>
        /// <c>WarriorsStandalonePlayer</c> 를 바탕으로 네트워크 플레이어를 만든다.
        ///
        /// <b>빼는 것</b>
        /// <code>
        ///   MovePlayerInput . CharacterMover   ithappy 에셋이 스스로 키보드를 읽는다.
        ///                                      그대로 두면 한 번 누를 때 두 배로 걷는다
        ///   WarriorsKeyboardInput              공격 입력은 3단계다. 지금 켜면 각자 자기
        ///                                      화면에서만 벤다
        /// </code>
        ///
        /// <b>넣는 것</b>
        /// <code>
        ///   NetworkObject . NetworkTransform   자리를 모두에게 보낸다
        ///   WarriorsNetPlayerMover             서버가 ApplyMovement 를 부른다
        ///   WarriorsLocalView                  내 카메라를 내 캐릭터에 붙인다
        ///   WarriorsPlayerLife                 목숨 4개와 Down
        /// </code>
        ///
        /// ⚠ <c>WarriorsLocalPlayerController</c> 는 <b>지우지 않는다.</b> 이동 규칙
        ///    (속도 . 회전 . 중력 . 데드존)이 그 안에 있고, 서버가 그 함수를 부른다.
        ///    스스로 키보드를 읽지 않도록 <c>WarriorsNetPlayerMover</c> 가 꺼 준다.
        /// </summary>
        private static GameObject BuildPlayerPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayerPrefab);

            if (source == null)
            {
                Debug.LogError($"[WarriorsSceneSetup] '{SourcePlayerPrefab}' 를 찾지 못했습니다.");
                return null;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "WarriorsNetPlayer";

            StripLocalOnlyParts(root);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<WarriorsNetPlayerMover>();
            root.AddComponent<WarriorsLocalView>();
            root.AddComponent<WarriorsPlayerLife>();
            root.AddComponent<WarriorsNetPlayerCombat>();

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || saved == null)
            {
                Debug.LogError($"[WarriorsSceneSetup] '{PlayerPrefabPath}' 저장에 실패했습니다.");
                return null;
            }

            // 적 사본과 같은 이유로 경로에서 다시 읽는다. (위 BuildEnemyPrefabs 주석 참고)
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Debug.Log($"[WarriorsSceneSetup] 플레이어 프리팹을 만들었습니다 - {PlayerPrefabPath}");
            return reloaded != null ? reloaded : saved;
        }

        /// <summary>스스로 키보드를 읽는 부품을 뗀다. 이름으로 찾아 타입 참조를 만들지 않는다.</summary>
        private static void StripLocalOnlyParts(GameObject root)
        {
            string[] unwanted = { "MovePlayerInput", "CharacterMover", "WarriorsKeyboardInput" };

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
            {
                if (behaviour == null) continue;

                if (unwanted.Contains(behaviour.GetType().Name))
                {
                    Debug.Log($"[WarriorsSceneSetup] 로컬 전용 '{behaviour.GetType().Name}' 를 뗐습니다.");
                    Object.DestroyImmediate(behaviour, true);
                }
            }
        }

        // ------------------------------------------------------------
        // 2. 네트워크 게임 씬
        // ------------------------------------------------------------

        private static void BuildGameScene(WarriorsEnemyDirector.EnemyPair[] enemies)
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(Path.GetDirectoryName(WarriorsNet.ScenePath));

            // ⚠ 먼저 **다른 이름으로 저장한 뒤** 고친다. 순서를 바꾸면 원본이 더럽혀진다.
            if (!EditorSceneManager.SaveScene(scene, WarriorsNet.ScenePath, true))
            {
                Debug.LogError($"[WarriorsSceneSetup] '{WarriorsNet.ScenePath}' 저장에 실패했습니다.");
                return;
            }

            scene = EditorSceneManager.OpenScene(WarriorsNet.ScenePath, OpenSceneMode.Single);

            StopLocalBootstrap(scene);
            HoldRulesForLaterSteps(scene);
            MarkSpawnPoints(scene);
            CreateNetworkParts(scene, enemies);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WarriorsSceneSetup] 네트워크 게임 씬을 만들었습니다 - {WarriorsNet.ScenePath}");
        }

        /// <summary>
        /// 씬이 스스로 플레이어를 만들지 않게 한다.
        ///
        /// <c>WarriorsSceneBootstrap</c> 은 <c>Instantiate</c> 로 로컬 플레이어를 하나 만든다.
        /// 네트워크에서는 <b>서버가</b> 사람마다 하나씩 스폰하므로, 그대로 두면
        /// 아무도 조작하지 않는 유령이 한 명 서 있게 된다.
        ///
        /// 컴포넌트를 지우지 않고 <c>createStandalonePlayer</c> 만 끈다.
        /// 그 안의 <c>WarriorsRun.BeginRun()</c> 은 그대로 돌아야 한다 - 판의 시드다.
        /// </summary>
        private static void StopLocalBootstrap(Scene scene)
        {
            WarriorsSceneBootstrap bootstrap = FindComponent<WarriorsSceneBootstrap>(scene);

            if (bootstrap == null)
            {
                Debug.LogWarning("[WarriorsSceneSetup] WarriorsSceneBootstrap 을 찾지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(bootstrap);
            data.FindProperty("createStandalonePlayer").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[WarriorsSceneSetup] 씬이 스스로 플레이어를 만들지 않도록 껐습니다.");
        }

        /// <summary>
        /// 전투 규칙을 잠시 꺼 둔다. **1단계 한정이다.**
        ///
        /// 스포너 . 진행 . 리듬은 아직 서버 권위로 옮겨지지 않았다. 켜 두면 모든 PC 가
        /// 각자 몬스터를 만들고 각자 라운드를 넘긴다. 2~5단계에서 하나씩 켠다.
        /// </summary>
        private static void HoldRulesForLaterSteps(Scene scene)
        {
            int held = 0;

            // 스포너는 꺼 둔 채로 둔다. WarriorsEnemyDirector 가 1페이즈에 서버에서만 켠다.
            held += Disable(FindComponent<WarriorsEnemySpawner>(scene), "몬스터 스포너");
            held += Disable(FindComponent<WarriorsGameFlow>(scene), "진행");
            held += Disable(FindComponent<WarriorsRhythmBattle>(scene), "리듬 전투");

            Debug.Log($"[WarriorsSceneSetup] 전투 규칙 {held}개를 1단계 동안 꺼 두었습니다. (2~5단계에서 켠다)");
        }

        private static int Disable(UnityEngine.Behaviour target, string label)
        {
            if (target == null) return 0;
            target.enabled = false;
            Debug.Log($"[WarriorsSceneSetup] '{label}' 를 껐습니다.");
            return 1;
        }

        /// <summary>
        /// 플레이어가 설 자리에 표식을 붙인다.
        ///
        /// 자리 자체는 아레나 프리팹에 이미 <c>PlayerSpawn_01</c> ~ <c>_04</c> 로 있다.
        /// 원본 프리팹을 고치지 않고 <b>이 씬에서만</b> 표식을 얹는다.
        /// </summary>
        private static void MarkSpawnPoints(Scene scene)
        {
            int marked = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform step in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!step.name.StartsWith("PlayerSpawn_")) continue;
                    if (step.GetComponent<WarriorsSpawnPoint>() != null) continue;

                    step.gameObject.AddComponent<WarriorsSpawnPoint>();
                    marked++;
                }
            }

            Debug.Log($"[WarriorsSceneSetup] 플레이어 자리 {marked}곳에 표식을 붙였습니다.");
        }

        /// <summary>게임 씬 쪽 네트워크 부품. 서버 정리와 <b>매치 공통 상태</b>.</summary>
        private static void CreateNetworkParts(Scene scene, WarriorsEnemyDirector.EnemyPair[] enemies)
        {
            GameObject existing = Find(scene, "WarriorsNetwork");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject manager = new GameObject("WarriorsNetwork");
            SceneManager.MoveGameObjectToScene(manager, scene);
            manager.AddComponent<WarriorsServerCleanup>();

            // 판 전체의 공통 상태(대기 . 카운트다운 . 페이즈 . 승패)를 복제한다.
            // 씬에 미리 놓인 NetworkObject 라, Fusion 이 이 씬을 열 때 함께 등록한다.
            manager.AddComponent<NetworkObject>();
            manager.AddComponent<WarriorsMatchState>();

            // 몬스터를 서버가 만들도록 경로를 갈아끼운다.
            WarriorsEnemyDirector director = manager.AddComponent<WarriorsEnemyDirector>();
            director.EditorSetEnemies(enemies);
            EditorUtility.SetDirty(director);

            WarriorsKrakenBoss boss = FindComponent<WarriorsKrakenBoss>(scene);
            Transform[] stands = BuildTentacleStands(scene, manager);

            // 2페이즈 촉수. 서 있을 자리를 만들고 크라켄과 함께 꽂는다.
            WarriorsPhase2Director tentacles = manager.AddComponent<WarriorsPhase2Director>();
            tentacles.EditorSetStage(boss, stands);
            EditorUtility.SetDirty(tentacles);

            // 3페이즈 리듬. 자리는 2페이즈와 같은 것을 그대로 쓴다 —
            // 촉수를 베던 그 자리에서 마지막 일격까지 간다.
            WarriorsPhase3Director rhythm = manager.AddComponent<WarriorsPhase3Director>();
            rhythm.EditorSetStage(boss, stands);
            EditorUtility.SetDirty(rhythm);
        }

        /// <summary>
        /// 2페이즈에 두 사람이 설 자리를 만든다.
        ///
        /// 크라켄은 원점에 있고 촉수는 z ≈ 4.6~5.25 · x = ∓4.2 / ∓1.4 에 박혀 있다.
        /// 자기 쪽 두 팔의 가운데(x = ∓2.8)에 서면 양쪽 모두 칼이 닿는다.
        /// z 는 0 — 크라켄을 마주 본 채, 촉수까지 5m 남짓이다.
        ///
        /// 자리를 <c>Transform</c> 으로 두는 이유는 <b>내일 같이 조정</b>하기 위해서다.
        /// 씬에서 끌어다 옮기면 그대로 반영된다.
        /// </summary>
        private static Transform[] BuildTentacleStands(Scene scene, GameObject parent)
        {
            Vector3[] places = { new Vector3(-2.8f, 0f, 0f), new Vector3(2.8f, 0f, 0f) };
            Transform[] stands = new Transform[places.Length];

            for (int i = 0; i < places.Length; i++)
            {
                GameObject stand = new GameObject($"TentacleStand_{i + 1:00}");
                SceneManager.MoveGameObjectToScene(stand, scene);
                stand.transform.SetParent(parent.transform, false);

                // 크라켄(+z 쪽)을 마주 본다.
                stand.transform.SetPositionAndRotation(places[i], Quaternion.identity);
                stands[i] = stand.transform;
            }

            Debug.Log($"[WarriorsSceneSetup] 2페이즈 촉수 자리 {stands.Length}곳을 만들었습니다.");
            return stands;
        }

        /// <summary>
        /// 몬스터의 네트워크 사본을 만든다.
        ///
        /// ⚠ 서연님의 원본 프리팹을 <b>고치지 않는다.</b> 원본에 <c>NetworkObject</c> 를 달면
        ///    <c>WarriorsTest</c> 혼자 플레이에서 스폰되지 않은 네트워크 오브젝트가 생긴다.
        /// </summary>
        private static WarriorsEnemyDirector.EnemyPair[] BuildEnemyPrefabs()
        {
            List<WarriorsEnemyDirector.EnemyPair> made = new List<WarriorsEnemyDirector.EnemyPair>();
            List<string> paths = new List<string>();

            foreach (string name in EnemyNames)
            {
                string sourcePath = EnemyFolder + name + ".prefab";
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

                if (source == null)
                {
                    Debug.LogError($"[WarriorsSceneSetup] '{sourcePath}' 를 찾지 못했습니다.");
                    continue;
                }

                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(
                    root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "WarriorsNet" + name;

                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<WarriorsNetEnemy>();

                string savePath = EnemyFolder + "WarriorsNet" + name + ".prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, savePath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok || saved == null)
                {
                    Debug.LogError($"[WarriorsSceneSetup] '{savePath}' 저장에 실패했습니다.");
                    continue;
                }

                made.Add(new WarriorsEnemyDirector.EnemyPair { sourceName = name, networked = null });
                paths.Add(savePath);

                Debug.Log($"[WarriorsSceneSetup] 네트워크 몬스터를 만들었습니다 - {savePath}");
            }

            // ⚠ 방금 만든 프리팹의 컴포넌트를 **그대로 참조하면 안 된다.**
            //    Fusion 이 임포트하면서 NetworkObject 를 다시 만들어 참조가 끊긴다.
            //    씬에 넣으면 fileID: 0 으로 저장되고, 서버가 "사본이 없다" 고 한다.
            //    실측으로 확인했다. 저장·새로고침한 뒤 **경로로 다시 읽는다.**
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < made.Count; i++)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                NetworkObject networked = asset != null ? asset.GetComponent<NetworkObject>() : null;

                if (networked == null)
                {
                    Debug.LogError($"[WarriorsSceneSetup] '{paths[i]}' 에서 NetworkObject 를 읽지 못했습니다.");
                    continue;
                }

                made[i] = new WarriorsEnemyDirector.EnemyPair
                {
                    sourceName = made[i].sourceName,
                    networked = networked,
                };
            }

            return made.ToArray();
        }

        // ------------------------------------------------------------
        // 3. 시작 씬
        // ------------------------------------------------------------

        /// <summary>
        /// 세션만 여는 빈 씬을 만든다.
        ///
        /// 여기에는 <b>NetworkRunner 와 런처와 스포너뿐</b>이다. 아레나도 HUD 도 없다.
        ///
        /// ⚠ 스포너는 반드시 러너와 <b>같은 오브젝트</b>에 있어야 한다.
        ///    Fusion 이 자동으로 등록하는 <c>SimulationBehaviour</c> 는 그것뿐이다.
        /// </summary>
        private static void BuildBootScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject manager = new GameObject("NetworkManager");
            manager.AddComponent<NetworkRunner>();
            manager.AddComponent<NetworkSceneManagerDefault>();
            manager.AddComponent<WarriorsLauncher>();

            WarriorsPlayerSpawner spawner = manager.AddComponent<WarriorsPlayerSpawner>();
            SerializedObject data = new SerializedObject(spawner);
            data.FindProperty("playerPrefab").objectReferenceValue = playerPrefab.GetComponent<NetworkObject>();
            data.ApplyModifiedPropertiesWithoutUndo();

            // 접속하는 몇 초 동안 "카메라가 없다" 경고 대신 검은 화면이 나오게 하려는 것뿐이다.
            // 플레이어가 스폰되면 WarriorsLocalView 가 이 카메라를 끈다.
            GameObject camera = new GameObject("Boot Camera");
            camera.tag = "MainCamera";
            Camera view = camera.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            camera.AddComponent<AudioListener>();

            Directory.CreateDirectory(Path.GetDirectoryName(WarriorsNet.BootScenePath));

            if (!EditorSceneManager.SaveScene(scene, WarriorsNet.BootScenePath))
            {
                Debug.LogError($"[WarriorsSceneSetup] '{WarriorsNet.BootScenePath}' 저장에 실패했습니다.");
                return;
            }

            Debug.Log($"[WarriorsSceneSetup] 시작 씬을 만들었습니다 - {WarriorsNet.BootScenePath}");
        }

        // ------------------------------------------------------------
        // 4. Scene List
        // ------------------------------------------------------------

        /// <summary>
        /// ⚠ <b>서버와 클라이언트가 같은 목록을 봐야 한다.</b> Fusion 은 씬을 경로가 아니라
        ///    <b>Scene List 의 번호</b>로 주고받는다. 한쪽에만 있으면 그 피어는 씬을 못 연다.
        ///    오류도 나지 않는다 - 세션에는 붙고 화면만 비어 있다.
        /// </summary>
        private static void RegisterScene(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(s => s.path == path))
            {
                Debug.Log($"[WarriorsSceneSetup] Scene List 에 이미 있습니다 - {path}");
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[WarriorsSceneSetup] Scene List 에 등록했습니다 - {path} (번호 {scenes.Count - 1})");
        }

        // ------------------------------------------------------------

        private static GameObject Find(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
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

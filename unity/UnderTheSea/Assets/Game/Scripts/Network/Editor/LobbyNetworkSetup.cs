using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ithappy.Cute_Characters.Controller;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// Lobby 씬을 Fusion 네트워크 월드로 배선한다. (PRD 08-2)
    ///
    /// <b>왜 스크립트로 하는가.</b>
    /// `Lobby.unity` 는 50만 줄이 넘는다. 손으로 YAML 을 고치면 어디를 건드렸는지 알기 어렵고
    /// 되돌리기도 힘들다. 무엇을 왜 넣었는지가 이 파일에 코드로 남는 편이 낫다.
    ///
    /// <b>여러 번 돌려도 안전하다.</b>
    /// 이미 있는 것은 다시 만들지 않고 참조만 다시 잇는다.
    /// 씬에 사람이 손으로 넣은 다른 오브젝트는 건드리지 않는다.
    ///
    /// 하는 일
    ///   1. `NetworkPlayer.prefab` 생성 — P_JaeYoung 모델을 재사용하되 로컬 입력 컴포넌트를 뺀다
    ///   2. Lobby 에 박혀 있던 `JaeYoung` 인스턴스 제거
    ///   3. `NetworkManager` (Runner + SceneManager + Launcher + Spawner) 배치
    ///   4. `SpawnPoints` 배치와 Spawner 배선
    ///   5. `MainCamera` 에 서버용 정리 컴포넌트 부착
    /// </summary>
    public static class LobbyNetworkSetup
    {
        private const string MenuPath = "Tools/아라아띠/Lobby 를 Fusion 네트워크 씬으로 배선";

        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";
        private const string SourceCharacterPath = "Assets/Game/Prefabs/Characters/P_JaeYoung.prefab";
        private const string NetworkPlayerPath = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";

        private const string NetworkManagerName = "NetworkManager";
        private const string SpawnRootName = "SpawnPoints";

        /// <summary>
        /// 씬에 박혀 있던 로컬 캐릭터의 위치. 지형 위 검증된 좌표라 스폰 기준으로 그대로 쓴다.
        /// (Lobby.unity 의 JaeYoung 인스턴스 m_LocalPosition 값)
        /// </summary>
        private static readonly Vector3 LegacyPlayerPosition = new Vector3(20.84f, 1.2f, 48.56f);

        /// <summary>스폰 포인트를 몇 개 깔 것인가. 인원이 더 많으면 순서대로 돌려쓴다.</summary>
        private const int SpawnPointCount = 4;

        /// <summary>스폰 포인트 간격(m). 캐릭터가 서로 겹쳐 태어나지 않을 정도면 된다.</summary>
        private const float SpawnSpacing = 2.5f;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            GameObject networkPlayer = EnsureNetworkPlayerPrefab();
            if (networkPlayer == null)
            {
                return;
            }

            // 씬 순서에 주의한다. ServerTestScene 을 먼저 고치고 마지막에 Lobby 를 열어 둔다.
            RewireServerTestScene();
            SetUpLobbyScene(networkPlayer);
        }

        /// <summary>커맨드라인용. 실패하면 종료 코드 1 로 빠진다.</summary>
        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyNetworkSetup] 실패: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- 프리팹

        /// <summary>
        /// Fusion 이 스폰할 캐릭터 프리팹을 만든다.
        ///
        /// <b>PlayerCapsule 을 재사용하지 않는 이유.</b>
        /// PlayerCapsule 은 원시 캡슐 메시에 <c>PlayerMovement</c> 가 <c>transform.position</c> 을
        /// 직접 더하는 구조다. 중력도 접지 판정도 없어서 지형이 있는 Lobby 에서는 공중에 뜨거나
        /// 지면을 뚫는다. ServerTestScene(평면) 전용이다.
        ///
        /// <b>P_JaeYoung 을 그대로 쓸 수 없는 이유.</b>
        /// <c>MovePlayerInput</c> 이 로컬 입력을 직접 읽고 <c>CharacterMover</c> 가
        /// <c>Update()</c> 에서 CharacterController 를 움직인다. Fusion 의 tick 기반 권한 모델과
        /// 충돌해 원격에서 캐릭터가 떨린다.
        ///
        /// 그래서 <b>모델 · Animator · CharacterController 는 재사용</b>하고
        /// 입력 · 이동 컴포넌트만 Fusion 용으로 갈아 끼운다.
        /// </summary>
        private static GameObject EnsureNetworkPlayerPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath);
            if (existing != null)
            {
                Debug.Log($"[LobbyNetworkSetup] NetworkPlayer 프리팹이 이미 있습니다: {NetworkPlayerPath}");
                return existing;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPath);
            if (source == null)
            {
                Debug.LogError($"[LobbyNetworkSetup] 원본 캐릭터를 찾지 못했습니다: {SourceCharacterPath}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

            // 원본과의 연결을 끊는다. 연결된 채로 두면 원본이 바뀔 때 네트워크 프리팹까지 흔들린다.
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.name = "NetworkPlayer";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            // ⚠ 지우는 순서가 중요하다.
            //    MovePlayerInput 에 [RequireComponent(typeof(CharacterMover))] 가 걸려 있어
            //    CharacterMover 를 먼저 지우려고 하면 Unity 가 막는다.
            RemoveAll<MovePlayerInput>(instance);
            RemoveAll<CharacterMover>(instance);

            // NetworkPlayerMover 가 [RequireComponent(typeof(NetworkObject))] 라 이것이 먼저다.
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkTransform>();
            instance.AddComponent<NetworkPlayerMover>();
            instance.AddComponent<LocalPlayerView>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, NetworkPlayerPath, out bool ok);
            Object.DestroyImmediate(instance);

            if (!ok || saved == null)
            {
                Debug.LogError($"[LobbyNetworkSetup] NetworkPlayer 프리팹 저장에 실패했습니다: {NetworkPlayerPath}");
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[LobbyNetworkSetup] NetworkPlayer 프리팹을 만들었습니다: {NetworkPlayerPath}\n" +
                $"  재사용: 모델 · Animator · CharacterController\n" +
                $"  제거:   MovePlayerInput · CharacterMover (로컬 입력 · Update 이동)\n" +
                $"  추가:   NetworkObject · NetworkTransform · NetworkPlayerMover · LocalPlayerView");

            return saved;
        }

        private static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Object.DestroyImmediate(component);
            }
        }

        // ---------------------------------------------------------------- 씬

        private static void SetUpLobbyScene(GameObject networkPlayer)
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[LobbyNetworkSetup] Lobby 씬을 열지 못했습니다: {LobbyScenePath}");
                return;
            }

            RemoveLegacyPlayer(scene);

            Transform[] spawnPoints = EnsureSpawnPoints(scene);
            EnsureNetworkManager(scene, networkPlayer, spawnPoints);
            EnsureServerCameraCleanup(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[LobbyNetworkSetup] Lobby 배선을 마쳤습니다: {LobbyScenePath}");
        }

        /// <summary>
        /// 씬에 고정 배치돼 있던 로컬 플레이어를 지운다.
        ///
        /// 그대로 두면 접속자마다 생기는 NetworkPlayer 와 별개로 조작 불가능한 캐릭터가
        /// 하나 더 서 있게 되고, 그 캐릭터의 MovePlayerInput 이 내 입력을 같이 먹는다.
        /// 프로젝트 전체를 검색해 이 프리팹을 참조하는 곳이 Lobby 씬뿐임을 확인하고 지운다.
        /// </summary>
        private static void RemoveLegacyPlayer(Scene scene)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPath);
            if (source == null)
            {
                return;
            }

            List<GameObject> doomed = new List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    GameObject candidate = t.gameObject;

                    // 프리팹 인스턴스의 원본이 P_JaeYoung 인 것만 고른다. 이름만 보고 지우지 않는다.
                    GameObject origin = PrefabUtility.GetCorrespondingObjectFromOriginalSource(candidate);
                    if (origin == source && PrefabUtility.IsAnyPrefabInstanceRoot(candidate))
                    {
                        doomed.Add(candidate);
                    }
                }
            }

            foreach (GameObject go in doomed)
            {
                Debug.Log(
                    $"[LobbyNetworkSetup] 씬에 고정된 로컬 캐릭터 '{go.name}' 를 제거합니다. " +
                    $"위치 {go.transform.position}. 이제 캐릭터는 서버가 스폰합니다.");
                Object.DestroyImmediate(go);
            }

            if (doomed.Count == 0)
            {
                Debug.Log("[LobbyNetworkSetup] 제거할 고정 캐릭터가 없습니다. (이미 정리됨)");
            }
        }

        private static Transform[] EnsureSpawnPoints(Scene scene)
        {
            GameObject root = Find(scene, SpawnRootName);

            if (root == null)
            {
                root = new GameObject(SpawnRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = LegacyPlayerPosition;
            }

            List<Transform> points = new List<Transform>();

            for (int i = 0; i < SpawnPointCount; i++)
            {
                string name = $"Spawn_{i}";
                Transform point = root.transform.Find(name);

                if (point == null)
                {
                    GameObject go = new GameObject(name);
                    go.transform.SetParent(root.transform, worldPositionStays: false);

                    // 원래 캐릭터가 서 있던 자리를 중심으로 옆으로 벌린다.
                    // 지형 위 검증된 좌표라 물 밑이나 지형 아래로 빠질 걱정이 없다.
                    go.transform.localPosition = new Vector3((i - (SpawnPointCount - 1) * 0.5f) * SpawnSpacing, 0f, 0f);
                    point = go.transform;
                }

                points.Add(point);
            }

            return points.ToArray();
        }

        private static void EnsureNetworkManager(Scene scene, GameObject networkPlayer, Transform[] spawnPoints)
        {
            GameObject manager = Find(scene, NetworkManagerName);

            if (manager == null)
            {
                manager = new GameObject(NetworkManagerName);
                SceneManager.MoveGameObjectToScene(manager, scene);
            }

            // ServerTestScene 의 NetworkManager 와 같은 구성이다.
            // PlayerInputProvider 는 FusionLauncher 가 실행 중에 스스로 붙인다. (서버에는 붙이지 않는다)
            Ensure<NetworkRunner>(manager);
            Ensure<NetworkSceneManagerDefault>(manager);
            Ensure<FusionLauncher>(manager);
            PlayerSpawner spawner = Ensure<PlayerSpawner>(manager);

            WireSpawner(spawner, networkPlayer, spawnPoints);
        }

        /// <summary>Spawner 에 프리팹과 스폰 포인트를 물린다.</summary>
        private static void WireSpawner(PlayerSpawner spawner, GameObject networkPlayer, Transform[] spawnPoints)
        {
            NetworkObject prefab = networkPlayer.GetComponent<NetworkObject>();

            if (prefab == null)
            {
                Debug.LogError($"[LobbyNetworkSetup] '{networkPlayer.name}' 에 NetworkObject 가 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);

            so.FindProperty("playerPrefab").objectReferenceValue = prefab;

            SerializedProperty pointsProp = so.FindProperty("spawnPoints");
            pointsProp.arraySize = spawnPoints.Length;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(
                $"[LobbyNetworkSetup] PlayerSpawner 배선 완료 — 프리팹 {prefab.name}, " +
                $"스폰 포인트 {spawnPoints.Length}개");
        }

        /// <summary>
        /// PRD 08-1 의 ServerTestScene 도 같은 필드를 쓴다.
        /// <c>playerPrefab</c> 의 타입을 바꿨으므로 그쪽 참조가 끊긴다. 다시 이어 준다.
        /// 이 씬을 안 쓰더라도 깨진 채로 두지 않는다.
        /// </summary>
        private static void RewireServerTestScene()
        {
            const string path = "Assets/Game/Scenes/Develop/GeonHee/ServerTestScene.unity";
            const string capsulePath = "Assets/Game/Prefabs/NetworkTest/PlayerCapsule.prefab";

            GameObject capsule = AssetDatabase.LoadAssetAtPath<GameObject>(capsulePath);
            if (capsule == null)
            {
                Debug.LogWarning($"[LobbyNetworkSetup] {capsulePath} 를 찾지 못해 ServerTestScene 을 건너뜁니다.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            PlayerSpawner spawner = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<PlayerSpawner>(includeInactive: true))
                .FirstOrDefault();

            if (spawner == null)
            {
                Debug.LogWarning("[LobbyNetworkSetup] ServerTestScene 에 PlayerSpawner 가 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue = capsule.GetComponent<NetworkObject>();
            // 스폰 포인트는 두지 않는다. 비워 두면 예전과 같은 자리(PlayerId * 3, 1, 0)에 스폰된다.
            so.FindProperty("spawnPoints").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[LobbyNetworkSetup] ServerTestScene 의 PlayerSpawner 를 PlayerCapsule 로 다시 이었습니다.");
        }

        /// <summary>
        /// 씬 카메라에 서버용 정리 컴포넌트를 붙인다.
        /// 서버도 같은 Lobby 씬을 로드하므로 카메라 · AudioListener 가 따라 들어온다.
        /// </summary>
        private static void EnsureServerCameraCleanup(Scene scene)
        {
            Camera main = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Camera>(includeInactive: true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));

            if (main == null)
            {
                Debug.LogWarning("[LobbyNetworkSetup] MainCamera 를 찾지 못해 서버용 정리 컴포넌트를 붙이지 못했습니다.");
                return;
            }

            Ensure<DedicatedServerSceneCleanup>(main.gameObject);
            Debug.Log($"[LobbyNetworkSetup] '{main.name}' 에 DedicatedServerSceneCleanup 을 붙였습니다.");
        }

        // ---------------------------------------------------------------- 공용

        private static T Ensure<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static GameObject Find(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        }
    }
}

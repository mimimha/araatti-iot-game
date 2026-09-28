using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.MiniGames.ShipCoop.Net.Editor
{
    /// <summary>
    /// ShipCoop 네트워크 씬과 플레이어 프리팹을 만든다. **한 번 눌러 만드는 도구다.**
    ///
    /// 손으로 만들면 "내 유니티에서는 되는데" 가 생긴다. 팀원이 같은 결과를 다시 만들 수 있게
    /// 무엇을 지우고 무엇을 붙였는지를 코드로 남긴다.
    ///
    /// <b>원본은 건드리지 않는다.</b>
    /// <c>Scenes/Develop/MinHwa/ShipCoopTest.unity</c> 는 읽기만 한다. 복사본을 만들어 고친다.
    /// 민화님이 혼자 키보드로 확인하던 길이 그대로 남아 있어야 한다.
    ///
    /// <code>
    ///   Tools/아라아띠/ShipCoop 네트워크 씬 만들기
    ///     1. ShipCoopPlayer.prefab   NetworkPlayer 를 바탕으로 ShipCoop 용 플레이어를 만든다
    ///     2. ShipCoop.unity          ShipCoopTest 를 복사해 싱글 플레이어를 빼고 네트워크를 붙인다
    ///     3. ShipCoopBoot.unity      세션만 여는 빈 씬. 여기서 시작해야 게임 씬이 한 벌만 열린다
    ///     4. Scene List              서버와 클라이언트가 같은 경로 두 개를 갖도록 등록한다
    /// </code>
    ///
    /// 여러 번 눌러도 같은 결과가 나온다. (이미 있으면 지우고 다시 만든다)
    /// </summary>
    public static class ShipCoopSceneSetup
    {
        private const string MenuRoot = "Tools/아라아띠/";

        private const string SourceScenePath = "Assets/Game/Scenes/Develop/MinHwa/ShipCoopTest.unity";
        private const string SourcePlayerPrefab = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/ShipCoopPlayer.prefab";

        private const string SourceHolePrefab = "Assets/Game/Prefabs/MiniGames/ShipCoop/HullDamagePoint.prefab";
        private const string HolePrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopHullDamagePoint.prefab";

        /// <summary>
        /// 캐릭터 모델을 키우는 배율.
        ///
        /// 배가 <b>키 3m 사람</b>에 맞춰 지어져 있다. 모델은 1.27m 라 그대로 두면 난쟁이가 된다.
        /// 2.25 배 = 2.8575m. <c>ShipCoopPlayerMover.bodyHeight</c> 와 같은 값이다.
        /// </summary>
        private const float ModelScale = 2.25f;

        /// <summary>모델 전체를 담는 자식 이름. <c>ShipCoopCharacter.model</c> 이 이걸 위아래로 움직인다.</summary>
        private const string VisualChildName = "Visual";

        /// <summary>갑판 위 스폰 자리. ShipCoopTest 의 Player_01 · Player_02 자리에서 폭만 넓혔다.</summary>
        private static readonly Vector3[] SpawnPositions =
        {
            new Vector3(0.25f, -3.39f, 0.5f),
            new Vector3(-1.35f, -3.39f, 0.5f),
            new Vector3(1.85f, -3.39f, 0.5f),
            new Vector3(-2.95f, -3.39f, 0.5f),
        };

        [MenuItem(MenuRoot + "ShipCoop 네트워크 씬 만들기")]
        public static void BuildAll()
        {
            GameObject prefab = BuildPlayerPrefab();

            if (prefab == null)
            {
                return;
            }

            GameObject hole = BuildHolePrefab();

            BuildScene(hole);
            BuildBootScene(prefab);

            RegisterScene(ShipCoopNet.BootScenePath);
            RegisterScene(ShipCoopNet.ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[ShipCoopSceneSetup] 완료했습니다.\n" +
                $"  프리팹    {PlayerPrefabPath}\n" +
                $"  게임 씬   {ShipCoopNet.ScenePath}\n" +
                $"  시작 씬   {ShipCoopNet.BootScenePath}");
        }

        /// <summary>커맨드라인용.</summary>
        public static void BuildAllFromCommandLine()
        {
            BuildAll();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        // ------------------------------------------------------------
        // 1. 플레이어 프리팹
        // ------------------------------------------------------------

        /// <summary>
        /// <c>NetworkPlayer</c> 를 바탕으로 ShipCoop 용 플레이어를 만든다.
        ///
        /// <b>왜 NetworkPlayer 에서 시작하는가.</b> 거기에는 이미 <c>NetworkObject</c> ·
        /// <c>NetworkTransform</c> · 외형 복제(<c>NetworkPlayerAppearance</c>) 가 붙어 있고,
        /// 커마로 만든 진짜 캐릭터를 붙일 2단계로 이어진다. 맨몸에서 시작하면 그 길이 끊긴다.
        ///
        /// <b>빼는 것</b> — Lobby 전용이라 여기서는 방해만 된다
        /// <code>
        ///   NetworkPlayerMover   지형 보행용. 계단 · 경사 숫자가 배와 다르다
        ///   LocalPlayerView      LobbyGameplayCamera · 포털 · Loading Overlay 를 찾는다
        /// </code>
        ///
        /// <b>넣는 것</b>
        /// <code>
        ///   ShipCoopPlayerMover       갑판 보행. 서버가 확정한다
        ///   ShipCoopLocalView         내 카메라 확정 + HUD 에 나를 알린다
        ///   ShipCoopCharacter         걷는 애니메이션 + 보이는 갑판에 발 붙이기
        ///   ShipCoopNetworkedController  네트워크 입력을 IPlayerController 로 내놓는다
        ///   TaskWorker                   작업 자리에 붙고 떨어진다
        ///   CarryTask · ShipCoopHelp     집고 나르기 · 도움 요청 (서버에서만 판정)
        ///   ShipCoopWorkerSync           서버가 정한 자리 · 손 상태를 모두에게 알린다
        ///   ShipCoopRider                배가 틀 때 같이 돈다 (서버에서만)
        ///   MiniGameDefaultAppearance    외형이 끝내 안 오면 기본 외형으로 — 미니게임 전용
        /// </code>
        ///
        /// ⚠ 이 오브젝트의 <c>IPlayerController</c> 는 <b>하나뿐이어야 한다.</b>
        ///    <c>TaskWorker.Awake</c> 가 <c>GetComponent</c> 로 찾기 때문에 둘이면 어느 쪽이
        ///    잡힐지 순서에 달린다. 키보드 기기는 캐릭터가 아니라 러너에 붙는다.
        /// </summary>
        private static GameObject BuildPlayerPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayerPrefab);

            if (source == null)
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{SourcePlayerPrefab}' 를 찾지 못했습니다.");
                return null;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "ShipCoopPlayer";

            StripLobbyParts(root);
            GameObject visual = WrapVisual(root);
            ConfigureBody(root);
            AttachShipCoopParts(root, visual);

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || saved == null)
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{PlayerPrefabPath}' 저장에 실패했습니다.");
                return null;
            }

            Debug.Log($"[ShipCoopSceneSetup] 플레이어 프리팹을 만들었습니다 — {PlayerPrefabPath}");
            return saved;
        }

        /// <summary>Lobby 전용 부품을 뗀다. 이름으로 찾는다 — 타입을 직접 참조하지 않아도 된다.</summary>
        private static void StripLobbyParts(GameObject root)
        {
            string[] unwanted = { "NetworkPlayerMover", "LocalPlayerView" };

            foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>().ToArray())
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (unwanted.Contains(behaviour.GetType().Name))
                {
                    Debug.Log($"[ShipCoopSceneSetup] Lobby 전용 '{behaviour.GetType().Name}' 를 뗐습니다.");
                    Object.DestroyImmediate(behaviour, true);
                }
            }
        }

        /// <summary>
        /// 보이는 것들을 자식 하나에 담고 배 크기에 맞춰 키운다.
        ///
        /// <b>왜 한 겹 더 씌우는가.</b> <c>ShipCoopCharacter</c> 는 발을 보이는 갑판에 붙이려고
        /// "보이는 몸" 을 위아래로 움직인다. NetworkPlayer 는 몸 · 모자 · 옷이 <b>전부 루트 바로
        /// 아래에 흩어져</b> 있어서 움직일 대상이 하나가 아니다. 하나만 움직이면 모자만 내려간다.
        ///
        /// 또 <c>transform.Find("Body")</c> 자동 탐색이 <b>몸통 메시</b>를 잡아버린다.
        /// 그래서 담는 자식을 만들고 그것을 명시적으로 넘긴다.
        /// </summary>
        private static GameObject WrapVisual(GameObject root)
        {
            GameObject visual = new GameObject(VisualChildName);
            visual.transform.SetParent(root.transform, false);

            // 루트 바로 아래에 있던 것들을 전부 옮긴다. 먼저 목록을 뜬 뒤 옮긴다 —
            // 옮기는 도중에 자식 목록이 바뀌면 건너뛰는 것이 생긴다.
            List<Transform> children = new List<Transform>();

            foreach (Transform child in root.transform)
            {
                if (child != visual.transform)
                {
                    children.Add(child);
                }
            }

            foreach (Transform child in children)
            {
                child.SetParent(visual.transform, false);
            }

            visual.transform.localScale = Vector3.one * ModelScale;

            MoveChildWatchers(root, visual);

            Debug.Log(
                $"[ShipCoopSceneSetup] 보이는 부품 {children.Count}개를 " +
                $"'{VisualChildName}'(x{ModelScale}) 아래로 묶었습니다.");

            return visual;
        }

        /// <summary>
        /// <b>바로 아래 자식</b>을 이름으로 찾는 부품을 모델과 같이 내려보낸다.
        ///
        /// 에셋(ithappy)의 <c>FacePicker</c> 가 그렇다.
        /// <code>
        ///   transform.Cast&lt;Transform&gt;().First(t =&gt; t.name.StartsWith("Faces"))
        /// </code>
        /// 바로 아래에서만 찾고, 못 찾으면 <c>First</c> 가 예외를 던진다. 위에서 한 겹 씌웠으니
        /// 루트에 그냥 두면 <c>OnValidate</c> 와 <c>Start</c> 에서 매번 터진다.
        /// 스폰될 때마다 터지므로 조용히 넘어가지도 않는다.
        ///
        /// 지우지 않고 <b>옮긴다.</b> 얼굴 메시 목록이 직렬화되어 들어 있고,
        /// 에셋 쪽 편집 도구(<c>FaceLoader</c>)가 그것을 본다.
        /// </summary>
        private static void MoveChildWatchers(GameObject root, GameObject visual)
        {
            string[] movable = { "FacePicker" };

            foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>().ToArray())
            {
                if (behaviour == null || !movable.Contains(behaviour.GetType().Name))
                {
                    continue;
                }

                if (UnityEditorInternal.ComponentUtility.CopyComponent(behaviour)
                    && UnityEditorInternal.ComponentUtility.PasteComponentAsNew(visual))
                {
                    Object.DestroyImmediate(behaviour, true);
                    Debug.Log(
                        $"[ShipCoopSceneSetup] '{behaviour.GetType().Name}' 를 " +
                        $"'{VisualChildName}' 로 옮겼습니다. (바로 아래 자식을 이름으로 찾는 부품)");
                }
                else
                {
                    Debug.LogWarning(
                        $"[ShipCoopSceneSetup] '{behaviour.GetType().Name}' 를 옮기지 못했습니다.", behaviour);
                }
            }
        }

        /// <summary>
        /// 들고 있는 물건이 보일 자리. 가슴 높이에 공 하나.
        ///
        /// <c>ShipCoopTest</c> 의 <c>HeldAmmo</c> 와 같은 크기 · 높이다.
        /// 무엇을 들었는지는 <c>CarryTask</c> 가 색으로 구분한다. (에셋이 오면 모델로 바뀐다)
        ///
        /// 콜라이더는 뗀다. 들고 있는 물건이 갑판이나 자기 몸에 걸리면 안 된다.
        /// </summary>
        private static GameObject CreateHeldItem(GameObject root)
        {
            GameObject held = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            held.name = "HeldItem";
            held.transform.SetParent(root.transform, false);
            held.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            held.transform.localScale = Vector3.one * 0.6f;

            Collider bump = held.GetComponent<Collider>();

            if (bump != null)
            {
                Object.DestroyImmediate(bump);
            }

            held.SetActive(false);
            return held;
        }

        /// <summary>
        /// 부딪히는 몸을 배 치수에 맞춘다.
        ///
        /// <c>ShipCoopPlayerMover</c> 가 <c>Spawned</c> 에서 다시 한 번 맞추지만,
        /// 에디터에서 프리팹을 열었을 때도 실제 크기가 보이는 편이 낫다.
        /// </summary>
        private static void ConfigureBody(GameObject root)
        {
            CharacterController body = root.GetComponent<CharacterController>();

            if (body == null)
            {
                body = root.AddComponent<CharacterController>();
            }

            const float height = 2.8575f;
            const float radius = 0.5f;

            body.height = height;
            body.radius = radius;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            body.stepOffset = 0.4f;
            body.slopeLimit = 50f;
            body.skinWidth = radius * 0.1f;
        }

        private static void AttachShipCoopParts(GameObject root, GameObject visual)
        {
            root.AddComponent<ShipCoopPlayerMover>();
            root.AddComponent<ShipCoopLocalView>();

            ShipCoopCharacter look = root.AddComponent<ShipCoopCharacter>();
            SerializedObject lookData = new SerializedObject(look);
            lookData.FindProperty("model").objectReferenceValue = visual.transform;
            lookData.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
            lookData.ApplyModifiedPropertiesWithoutUndo();

            // 네트워크로 온 입력을 IPlayerController 인 척 내놓는다.
            // TaskWorker 가 Awake 에서 찾는 상대이고, 이 오브젝트의 유일한 컨트롤러여야 한다.
            root.AddComponent<ShipCoopNetworkedController>();
            root.AddComponent<TaskWorker>();

            // 집고 나르기 · 도움 요청. 판정은 서버에서만 돌고,
            // 클라이언트에서는 ShipCoopWorkerSync 가 꺼 둔 뒤 복제받은 결과만 화면에 옮긴다.
            CarryTask carry = root.AddComponent<CarryTask>();
            root.AddComponent<ShipCoopHelp>();
            root.AddComponent<ShipCoopWorkerSync>();

            // 배가 트는 만큼 같이 돈다. 서버에서만 태운다.
            root.AddComponent<ShipCoopRider>();

            SerializedObject carryData = new SerializedObject(carry);
            carryData.FindProperty("heldVisual").objectReferenceValue = CreateHeldItem(root);
            carryData.ApplyModifiedPropertiesWithoutUndo();

            // 🙌 두 팔로 안는 자세. 이게 없으면 들고 있는 표시가 머리 위에 떠 있는다.
            // 싱글 씬은 ShipCoopDeckLayout 이 붙여 주지만 이 프리팹은 여기서 만들므로 직접 붙인다.
            // Animator 가 붙은 오브젝트에 있어야 OnAnimatorIK 가 불린다. (IK Pass 는 컨트롤러에 이미 켜져 있다)
            Animator animator = root.GetComponentInChildren<Animator>(true);

            if (animator != null && animator.GetComponent<ShipCoopCarryPose>() == null)
            {
                animator.gameObject.AddComponent<ShipCoopCarryPose>();
            }

            // 외형이 끝내 안 오면 기본 외형으로 정하는 안전망. **이 프리팹에만 붙는다.**
            // Lobby 의 NetworkPlayer 에는 없으므로 거기서는 기본 외형 확정을 부르는 코드가 없다.
            root.AddComponent<global::MiniGames.Common.MiniGameDefaultAppearance>();
        }

        // ------------------------------------------------------------
        // 2. 네트워크 씬
        // ------------------------------------------------------------

        private static void BuildScene(GameObject holePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(Path.GetDirectoryName(ShipCoopNet.ScenePath));

            // ⚠ 먼저 **다른 이름으로 저장한 뒤** 고친다. 순서를 바꾸면 원본이 더럽혀진다.
            if (!EditorSceneManager.SaveScene(scene, ShipCoopNet.ScenePath, true))
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{ShipCoopNet.ScenePath}' 저장에 실패했습니다.");
                return;
            }

            scene = EditorSceneManager.OpenScene(ShipCoopNet.ScenePath, OpenSceneMode.Single);

            RemoveSinglePlayerCharacters(scene);
            Transform[] spawnPoints = CreateSpawnPoints(scene);
            CreateNetworkParts(scene, holePrefab);
            HoldVoyageUntilSomeoneJoins(scene);
            CarrySpawnPointsWithShip(scene, spawnPoints);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[ShipCoopSceneSetup] 네트워크 씬을 만들었습니다 — {ShipCoopNet.ScenePath}");
        }

        /// <summary>
        /// 싱글 테스트용 캐릭터를 지운다.
        ///
        /// <c>Player_01</c> · <c>Player_02</c> 와 각자의 발밑 그림자다.
        /// 이것들이 남아 있으면 접속하지 않은 캐릭터가 갑판에 서 있고,
        /// HUD 의 <c>LocalWorker</c> 자동 탐색이 그 쪽을 집는다.
        /// </summary>
        private static void RemoveSinglePlayerCharacters(Scene scene)
        {
            string[] doomed = { "Player_01", "Player_02", "Player_01_FootShadow", "Player_02_FootShadow" };
            int removed = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (doomed.Contains(root.name))
                {
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }

            Debug.Log($"[ShipCoopSceneSetup] 싱글 테스트 캐릭터 {removed}개를 지웠습니다.");
        }

        private static Transform[] CreateSpawnPoints(Scene scene)
        {
            GameObject parent = Find(scene, "SpawnPoints");

            if (parent != null)
            {
                Object.DestroyImmediate(parent);
            }

            parent = new GameObject("SpawnPoints");
            SceneManager.MoveGameObjectToScene(parent, scene);

            Transform[] points = new Transform[SpawnPositions.Length];

            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                GameObject point = new GameObject($"Spawn_{i + 1}");
                point.transform.SetParent(parent.transform, false);
                point.transform.position = SpawnPositions[i];
                point.AddComponent<ShipCoopSpawnPoint>();
                points[i] = point.transform;
            }

            return points;
        }

        /// <summary>
        /// 스폰 자리도 배와 함께 돌게 한다.
        ///
        /// <b>왜 필요한가.</b> 배는 <b>제자리에서 돈다.</b> (<c>ShipCoopShipTurn</c>)
        /// 나중에 들어온 사람의 스폰 자리가 안 돌면, 배가 14도 돌아 있을 때
        /// 그 자리는 이미 <b>갑판 밖</b>이다. 그대로 떨어진다.
        ///
        /// ⚠ 사람 자체는 아직 배를 따라 돌지 않는다. 스폰된 플레이어를
        ///    <c>carried</c> 에 넣는 일은 2단계다. (<c>Carry(Transform)</c> 가 필요하다)
        /// </summary>
        private static void CarrySpawnPointsWithShip(Scene scene, Transform[] spawnPoints)
        {
            ShipCoopShipTurn turn = FindComponent<ShipCoopShipTurn>(scene);

            if (turn == null)
            {
                Debug.LogWarning("[ShipCoopSceneSetup] ShipCoopShipTurn 을 찾지 못해 스폰 자리를 태우지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(turn);
            SerializedProperty carried = data.FindProperty("carried");

            // 지워진 Player_01 · Player_02 가 빈 칸으로 남아 있다. 같이 치운다.
            for (int i = carried.arraySize - 1; i >= 0; i--)
            {
                if (carried.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    carried.DeleteArrayElementAtIndex(i);
                }
            }

            foreach (Transform point in spawnPoints)
            {
                carried.InsertArrayElementAtIndex(carried.arraySize);
                carried.GetArrayElementAtIndex(carried.arraySize - 1).objectReferenceValue = point;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ShipCoopSceneSetup] 스폰 자리 {spawnPoints.Length}개를 배에 태웠습니다.");
        }

        /// <summary>
        /// 게임 씬 쪽 네트워크 부품. 서버 정리와 <b>배 상태 복제</b>.
        ///
        /// 스포너는 여기 두지 않는다. Fusion 은 <b>러너와 같은 오브젝트에 붙은</b>
        /// <c>SimulationBehaviour</c> 만 등록하므로, 여기 두면 <c>PlayerJoined</c> 가 오지 않는다.
        /// 스포너는 시작 씬의 NetworkManager 에 있고, 스폰 자리는 실행 중에
        /// <see cref="ShipCoopSpawnPoint"/> 표식으로 찾는다.
        /// </summary>
        private static void CreateNetworkParts(Scene scene, GameObject holePrefab)
        {
            foreach (string stale in new[] { "NetworkManager", "ShipCoopNetwork" })
            {
                GameObject existing = Find(scene, stale);

                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }

            GameObject manager = new GameObject("ShipCoopNetwork");
            SceneManager.MoveGameObjectToScene(manager, scene);
            manager.AddComponent<ShipCoopServerCleanup>();

            // 배 한 척의 공유 상태(HP · 침수 · 진행도 · 페이즈)를 복제한다.
            // 씬에 미리 놓인 NetworkObject 라, Fusion 이 이 씬을 열 때 함께 등록한다.
            manager.AddComponent<NetworkObject>();
            manager.AddComponent<ShipCoopStateSync>();
            manager.AddComponent<ShipCoopEventSync>();

            // 구멍을 서버가 만들고 치우도록 경로를 갈아끼운다.
            ShipCoopHoleSpawner holes = manager.AddComponent<ShipCoopHoleSpawner>();

            if (holePrefab != null)
            {
                SerializedObject holeData = new SerializedObject(holes);
                holeData.FindProperty("holePrefab").objectReferenceValue =
                    holePrefab.GetComponent<NetworkObject>();
                holeData.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 네트워크 전용 파손 지점을 만든다.
        ///
        /// ⚠ 민화님의 <c>HullDamagePoint.prefab</c> 을 <b>고치지 않는다.</b>
        ///    원본에 <c>NetworkObject</c> 를 달면 <c>ShipCoopTest</c> 혼자 플레이에서
        ///    스폰되지 않은 네트워크 오브젝트가 생긴다. 그래서 사본을 만든다.
        ///
        /// <c>ShipCoopRider</c> 를 붙이는 이유: 스폰된 오브젝트는 씬의 <c>carried</c> 에
        /// 없어서 배가 틀어도 따라 돌지 않는다. 구멍만 갑판 밖으로 밀려난다.
        /// </summary>
        private static GameObject BuildHolePrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceHolePrefab);

            if (source == null)
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{SourceHolePrefab}' 를 찾지 못했습니다.");
                return null;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "ShipCoopHullDamagePoint";

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<ShipCoopHoleSync>();
            root.AddComponent<ShipCoopRider>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, HolePrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || saved == null)
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{HolePrefabPath}' 저장에 실패했습니다.");
                return null;
            }

            Debug.Log($"[ShipCoopSceneSetup] 네트워크용 파손 지점을 만들었습니다 — {HolePrefabPath}");
            return saved;
        }

        /// <summary>
        /// 빈 배가 혼자 출항하지 않게 한다.
        ///
        /// <c>ShipCoopGame.autoStart</c> 는 혼자 테스트하려고 켜 둔 값이다.
        /// Dedicated Server 는 <b>아무도 없는 채로 먼저 뜨므로</b> 그대로 두면
        /// 접속하기도 전에 배가 암초에 부딪혀 가라앉는다. 실측했다 — 40초 걸렸다.
        ///
        /// 대신 <c>ShipCoopPlayerSpawner</c> 가 첫 사람이 들어올 때 출항시킨다.
        /// 민화님의 <c>ShipCoopTest</c> 씬은 켜진 채로 남으므로 예전 그대로 굴러간다.
        /// </summary>
        private static void HoldVoyageUntilSomeoneJoins(Scene scene)
        {
            ShipCoopGame game = FindComponent<ShipCoopGame>(scene);

            if (game == null)
            {
                Debug.LogWarning("[ShipCoopSceneSetup] ShipCoopGame 을 찾지 못했습니다.");
                return;
            }

            SerializedObject data = new SerializedObject(game);
            data.FindProperty("autoStart").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[ShipCoopSceneSetup] 자동 출항을 껐습니다. 첫 사람이 들어올 때 출항합니다.");
        }

        // ------------------------------------------------------------
        // 3. 시작 씬
        // ------------------------------------------------------------

        /// <summary>
        /// 세션만 여는 빈 씬을 만든다.
        ///
        /// 여기에는 <b>NetworkRunner 와 런처뿐</b>이다. 배도 HUD 도 없다.
        /// Fusion 이 게임 씬을 열면 화면이 그쪽으로 넘어간다.
        ///
        /// 카메라를 하나 둔다. 접속하는 몇 초 동안 "카메라가 없다" 경고 대신
        /// 검은 화면이 나오게 하려는 것뿐이다. 플레이어가 스폰되면
        /// <c>ShipCoopLocalView</c> 가 이 카메라를 끈다.
        /// </summary>
        private static void BuildBootScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject manager = new GameObject("NetworkManager");
            manager.AddComponent<NetworkRunner>();
            manager.AddComponent<NetworkSceneManagerDefault>();
            manager.AddComponent<ShipCoopLauncher>();

            // 이 컴퓨터의 기기. 사람마다 하나이므로 캐릭터가 아니라 여기 붙는다.
            manager.AddComponent<KeyboardPlayerController>();
            manager.AddComponent<ShipCoopInputProvider>();

            // ⚠ 스포너는 반드시 러너와 **같은 오브젝트**에 있어야 한다.
            //    Fusion 이 자동으로 등록하는 SimulationBehaviour 는 그것뿐이다.
            ShipCoopPlayerSpawner spawner = manager.AddComponent<ShipCoopPlayerSpawner>();
            SerializedObject data = new SerializedObject(spawner);
            data.FindProperty("playerPrefab").objectReferenceValue =
                playerPrefab.GetComponent<NetworkObject>();
            data.ApplyModifiedPropertiesWithoutUndo();

            GameObject camera = new GameObject("Boot Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>().backgroundColor = new Color(0.05f, 0.09f, 0.14f);
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.AddComponent<AudioListener>();

            Directory.CreateDirectory(Path.GetDirectoryName(ShipCoopNet.BootScenePath));

            if (!EditorSceneManager.SaveScene(scene, ShipCoopNet.BootScenePath))
            {
                Debug.LogError($"[ShipCoopSceneSetup] '{ShipCoopNet.BootScenePath}' 저장에 실패했습니다.");
                return;
            }

            Debug.Log($"[ShipCoopSceneSetup] 시작 씬을 만들었습니다 — {ShipCoopNet.BootScenePath}");
        }

        // ------------------------------------------------------------
        // 4. Scene List
        // ------------------------------------------------------------

        /// <summary>
        /// 제품 Scene List 에 ShipCoop 씬을 더한다.
        ///
        /// ⚠ <b>서버와 클라이언트가 같은 목록을 봐야 한다.</b> Fusion 은 씬을 경로가 아니라
        ///    <b>Scene List 의 번호</b>로 주고받는다. 한쪽에만 있으면 그 피어는 씬을 못 연다.
        ///    오류도 나지 않는다 — 세션에는 붙고 화면만 비어 있다.
        /// </summary>
        private static void RegisterScene(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(s => s.path == path))
            {
                Debug.Log($"[ShipCoopSceneSetup] Scene List 에 이미 있습니다 — {path}");
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[ShipCoopSceneSetup] Scene List 에 등록했습니다 — {path} (번호 {scenes.Count - 1})");
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

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}

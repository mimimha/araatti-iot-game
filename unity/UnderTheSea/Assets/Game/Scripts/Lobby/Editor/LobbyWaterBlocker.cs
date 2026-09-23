using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// Lobby 의 **물에 못 들어가게 투명 벽**을 세운다.
    ///
    /// <b>왜 필요한가.</b>
    /// 정글 로비의 강(<c>WaterPlane_02</c> · <c>WaterPlane_03</c>)에 들어가면 캐릭터가
    /// 그 자리에서 움직이지 못한다. 두 물 판에는 <c>MeshCollider</c> 가 달려 있는데,
    /// 물가는 얕아서 **물 표면이 캐릭터의 발과 머리 사이 높이에 온다.**
    /// <c>NetworkPlayer</c> 의 CharacterController 는 키 1.2m · 반지름 0.35m 에
    /// skinWidth 가 0.0001 밖에 안 돼서, 캡슐 중간을 가로지르는 삼각형 메시를 만나면
    /// 밀려나지 못하고 그대로 낀다. 이동은 서버가 <c>controller.Move()</c> 로 확정하므로
    /// (<see cref="NetworkPlayerMover"/>) 한 번 끼면 클라이언트에서는 손쓸 방법이 없다.
    ///
    /// <b>무엇을 하는가.</b>
    ///   1. 물 판 아래 지형을 격자로 훑어 **어디가 물에 잠기는지** 를 가른다.
    ///   2. 잠긴 칸과 마른 칸의 경계, 즉 **물가 선을 따라** <c>BoxCollider</c> 울타리를 세운다.
    ///      렌더러가 없어 보이지 않는다.
    ///   3. 물 판 자신의 <c>MeshCollider</c> 를 끈다. 울타리를 넘어 들어갈 일은 없지만,
    ///      끼는 원인을 남겨 둘 이유도 없다.
    ///
    /// <b>왜 사각형으로 두르지 않는가.</b>
    /// 물 판은 물 모양이 아니라 그냥 큰 사각형이라, 그 안의 **절반 넘는 넓이가 물 위로 솟은 땅**이다
    /// (진단 기준 <c>WaterPlane_02</c> 는 169칸 중 102칸, <c>WaterPlane_03</c> 은 130칸).
    /// 사각형 둘레를 막으면 걸어다녀야 할 정글까지 같이 막힌다.
    ///
    /// <b>왜 스크립트로 하는가.</b>
    /// <c>Lobby.unity</c> 는 50만 줄이 넘는다. 손으로 YAML 을 고치면 무엇을 왜 넣었는지
    /// 남지 않는다. <see cref="LobbyPortalSetup"/> 과 같은 이유다.
    ///
    /// <b>여러 번 돌려도 안전하다.</b> 이미 있으면 다시 만들지 않고 크기만 다시 맞춘다.
    /// 되돌리려면 <c>Tools/아라아띠/로비 물 투명벽 걷어내기</c> 를 누른다.
    ///
    /// <code>
    ///   Tools/아라아띠/로비 물 상태 진단     — 왜 끼는지 수치로 확인만 한다 (씬을 고치지 않는다)
    ///   Tools/아라아띠/로비 물 투명벽 세우기
    ///   Tools/아라아띠/로비 물 투명벽 걷어내기
    /// </code>
    /// </summary>
    public static class LobbyWaterBlocker
    {
        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

        /// <summary>투명 벽을 모아 두는 루트. 이 이름으로 찾아서 지우므로 바꾸면 안 된다.</summary>
        private const string BlockerRootName = "WaterBlockers";

        /// <summary>막을 물 판. 씬의 <c>Water</c> 아래에 있는 것들이다.</summary>
        private static readonly string[] WaterObjectNames = { "WaterPlane_02", "WaterPlane_03" };

        /// <summary>
        /// 유니티 기본 Plane 메시는 한 변이 10 이고 원점이 가운데다. 그래서 로컬 좌표로 ±5.
        /// 물 판은 이 메시를 스케일만 바꿔 쓰므로, 로컬 ±5 가 곧 물의 가장자리다.
        /// </summary>
        private const float PlaneHalfExtent = 5f;

        /// <summary>벽 두께(월드 m). 얇으면 빠른 이동이 스윕 사이로 빠질 수 있어 넉넉히 둔다.</summary>
        private const float WallThickness = 0.3f;

        /// <summary>
        /// 벽 높이(월드 m). 아래로 내려가는 몫은 물가 지형이 수면보다 낮은 경우를,
        /// 위로 올라가는 몫은 점프로 넘는 것을 막는다.
        /// <see cref="NetworkPlayerMover"/> 의 jumpHeight 는 1.2m, 캡슐 키가 1.2m 이므로
        /// 수면 위 6m 면 밟고 넘을 방법이 없다.
        /// </summary>
        private const float WallHeight = 8f;

        /// <summary>벽의 세로 중심(월드 m, 수면 기준). 아래 2m · 위 6m 가 되도록 둔다.</summary>
        private const float WallCenterY = 2f;

        /// <summary>
        /// 물가 선을 찾는 격자 한 칸의 크기(월드 m).
        ///
        /// 작을수록 울타리가 물가에 정확히 붙지만 <c>BoxCollider</c> 수가 는다.
        /// 0.5m 면 캡슐 반지름(0.35m)보다 촘촘해서 빠져나갈 틈이 생기지 않는다.
        /// </summary>
        private const float CellSize = 0.5f;

        /// <summary>
        /// 어깨 높이 = 캡슐 키 × 이 비율.
        ///
        /// 캡슐은 발끝부터 정수리까지라, 어깨는 그보다 조금 아래다.
        /// 사람 몸 비율로 대략 0.85 쯤이다.
        /// </summary>
        private const float ShoulderRatio = 0.85f;

        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";

        /// <summary>캡슐 키를 못 읽었을 때 쓸 값. 현재 프리팹 값과 같다.</summary>
        private const float FallbackCapsuleHeight = 1.2f;

        /// <summary>
        /// **물이 어깨까지 차는 깊이.** 이보다 깊으면 막고, 얕으면 걸어서 건너게 둔다.
        ///
        /// 값을 박아 두지 않고 <c>NetworkPlayer</c> 프리팹의 CharacterController 에서 읽는다.
        /// 캡슐 크기를 바꾸면 막는 기준도 같이 따라가야 하기 때문이다.
        /// </summary>
        internal static float ShoulderDepth()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;

            float capsule = cc != null
                ? Mathf.Max(cc.height, cc.radius * 2f)
                : FallbackCapsuleHeight;

            return capsule * ShoulderRatio;
        }

        [MenuItem("Tools/아라아띠/로비 물 투명벽 세우기")]
        public static void Install()
        {
            Scene scene = OpenLobby();

            GameObject root = FindOrCreateRoot(scene);
            int walls = 0;
            int planes = 0;

            // ⚠ 물 판 콜라이더는 **울타리를 다 세운 뒤에** 끈다.
            //    먼저 끄면 물가를 재는 레이가 물 대신 물 아래 지형을 잡는 것이 아니라,
            //    아직 안 잰 다른 물 판을 땅으로 세어 물가 선이 틀어진다.
            var planesToPatch = FindWaterPlanes(scene).ToList();

            foreach (Transform water in planesToPatch)
            {
                walls += BuildBlockerFor(water, root.transform);
                planes++;
            }

            foreach (Transform water in planesToPatch)
            {
                DisableWaterCollider(water);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[물 투명벽] 세웠다. 물 판 {planes}개 · 벽 {walls}장. 씬을 저장했다.");
        }

        [MenuItem("Tools/아라아띠/로비 물 투명벽 걷어내기")]
        public static void Remove()
        {
            Scene scene = OpenLobby();

            GameObject root = FindRoot(scene);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            // 벽을 걷어내면 물에 다시 들어갈 수 있게 된다. 끼는 원인이던 MeshCollider 도
            // 같이 되살려야 "원래대로" 다. 켜 두지 않으면 물 위를 걷게 되어 더 이상해진다.
            foreach (Transform water in FindWaterPlanes(scene))
            {
                MeshCollider collider = water.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[물 투명벽] 걷어냈다. 물 판 MeshCollider 도 원래대로 켰다. 씬을 저장했다.");
        }

        /// <summary>
        /// 왜 끼는지를 수치로 본다. 씬을 고치지 않는다.
        ///
        /// 물 판 위를 격자로 훑으며 아래로 레이를 쏴서 **물 밑 지형 높이**를 잰다.
        /// 그 높이가 <c>수면 - 캡슐키</c> 와 <c>수면</c> 사이에 들어오면, 거기 선 캐릭터의
        /// 캡슐 중간을 물 표면이 가로지른다는 뜻이다. 그게 끼는 자리다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 물 상태 진단")]
        public static void Report()
        {
            Scene scene = OpenLobby();

            // NetworkPlayer 의 CharacterController 값. 프리팹에서 읽어 와 추측을 없앤다.
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab");
            CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;
            float capsuleHeight = cc != null ? Mathf.Max(cc.height, cc.radius * 2f) : 1.2f;

            Debug.Log($"[물 진단] 캡슐 키 {capsuleHeight:F2}m · 반지름 {(cc != null ? cc.radius : 0.35f):F2}m " +
                      $"· skinWidth {(cc != null ? cc.skinWidth : 0f):F4}");

            foreach (Transform water in FindWaterPlanes(scene))
            {
                MeshCollider self = water.GetComponent<MeshCollider>();

                int samples = 0, pinned = 0, dry = 0, deep = 0;
                float minGround = float.MaxValue, maxGround = float.MinValue;

                const int Steps = 12;
                for (int ix = 0; ix <= Steps; ix++)
                {
                    for (int iz = 0; iz <= Steps; iz++)
                    {
                        Vector3 local = new Vector3(
                            Mathf.Lerp(-PlaneHalfExtent, PlaneHalfExtent, ix / (float)Steps), 0f,
                            Mathf.Lerp(-PlaneHalfExtent, PlaneHalfExtent, iz / (float)Steps));

                        if (!TrySampleGround(water, local, out float ground))
                        {
                            continue;
                        }

                        // 물 판이 기울어 있어 수면 높이는 자리마다 다르다. 점마다 따로 읽는다.
                        float surfaceY = water.TransformPoint(local).y;

                        samples++;
                        minGround = Mathf.Min(minGround, ground);
                        maxGround = Mathf.Max(maxGround, ground);

                        if (ground >= surfaceY) dry++;                              // 물 위로 솟은 땅
                        else if (ground > surfaceY - capsuleHeight) pinned++;       // 캡슐이 수면에 걸린다
                        else deep++;                                                // 머리까지 잠긴다
                    }
                }

                if (samples == 0)
                {
                    Debug.LogWarning($"[물 진단] {water.name}: 아래에서 아무 콜라이더도 못 찾았다.");
                    continue;
                }

                Debug.Log(
                    $"[물 진단] {water.name}  수면 y(가운데)={water.position.y:F2}  " +
                    $"바닥 y={minGround:F2}~{maxGround:F2}  " +
                    $"표본 {samples}개 → 물 위 땅 {dry} · **캡슐이 수면에 걸림 {pinned}** · 완전히 잠김 {deep}  " +
                    $"(MeshCollider {(self != null && self.enabled ? "켜짐" : "꺼짐/없음")})");
            }
        }

        private static bool IsWaterCollider(Collider collider)
        {
            return collider != null && WaterObjectNames.Contains(collider.gameObject.name);
        }

        private static Scene OpenLobby()
        {
            Scene open = SceneManager.GetActiveScene();
            if (open.IsValid() && open.path == LobbyScenePath)
            {
                return open;
            }

            return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        }

        private static IEnumerable<Transform> FindWaterPlanes(Scene scene)
        {
            var found = new List<Transform>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (WaterObjectNames.Contains(t.name))
                    {
                        found.Add(t);
                    }
                }
            }

            foreach (string name in WaterObjectNames)
            {
                if (found.All(t => t.name != name))
                {
                    Debug.LogWarning($"[물 투명벽] 씬에서 '{name}' 를 못 찾았다. 이름이 바뀌었는지 확인해라.");
                }
            }

            return found;
        }

        private static GameObject FindRoot(Scene scene)
        {
            return scene.GetRootGameObjects().FirstOrDefault(go => go.name == BlockerRootName);
        }

        private static GameObject FindOrCreateRoot(Scene scene)
        {
            GameObject root = FindRoot(scene);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(BlockerRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        /// <summary>
        /// 물 판 하나의 **물가 선을 따라** 울타리를 세운다. 세운 벽 수를 돌려준다.
        ///
        /// 물 판은 기울고 돌아가 있다(<c>WaterPlane_02</c> 는 y 로 70.8° 돌고 5.5° 기울었다).
        /// 그래서 월드 축으로 상자를 놓으면 물과 안 맞는다. 울타리 오브젝트에 물 판의
        /// **위치 · 회전 · 스케일을 그대로 복사**하고 그 로컬 좌표로만 계산하면,
        /// 회전이 얼마든 물가에 정확히 붙는다.
        ///
        /// 격자를 훑어 칸마다 물에 잠기는지를 정하고, 잠긴 칸과 마른 칸이 맞닿는 변에만
        /// 벽을 놓는다. 이어지는 벽은 하나의 긴 상자로 합쳐 개수를 줄인다.
        /// </summary>
        private static int BuildBlockerFor(Transform water, Transform root)
        {
            string name = $"Blocker_{water.name}";
            Transform existing = root.Find(name);

            Vector3 scale = water.lossyScale;

            // 월드에서 정사각형 칸이 되도록 축마다 칸 수를 따로 잡는다.
            // 물 판은 x · z 스케일이 크게 다르다(예: 1.06 대 2.92).
            int nx = Mathf.Max(1, Mathf.RoundToInt(PlaneHalfExtent * 2f * Mathf.Abs(scale.x) / CellSize));
            int nz = Mathf.Max(1, Mathf.RoundToInt(PlaneHalfExtent * 2f * Mathf.Abs(scale.z) / CellSize));

            float lx = PlaneHalfExtent * 2f / nx;   // 칸 하나의 로컬 x 폭
            float lz = PlaneHalfExtent * 2f / nz;

            float shoulder = ShoulderDepth();

            bool[,] wet = new bool[nx, nz];
            int wetCount = 0;
            int shallowCount = 0;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    Vector3 local = new Vector3(
                        -PlaneHalfExtent + (i + 0.5f) * lx, 0f,
                        -PlaneHalfExtent + (j + 0.5f) * lz);

                    // 지형을 못 찾은 칸은 마른 것으로 둔다. 없는 곳에 벽을 세우지 않는다.
                    if (!TrySampleGround(water, local, out float ground))
                    {
                        continue;
                    }

                    // 물이 어깨까지 차는 곳만 막는다. 발목 · 무릎까지만 잠기는 개울은
                    // 걸어서 건널 수 있어야 한다.
                    float depth = water.TransformPoint(local).y - ground;

                    if (depth >= shoulder)
                    {
                        wet[i, j] = true;
                        wetCount++;
                    }
                    else if (depth > 0f)
                    {
                        shallowCount++;
                    }
                }
            }

            if (wetCount == 0)
            {
                // 막을 곳이 없으면 껍데기도 남기지 않는다.
                // 예전에 세워 둔 울타리가 있으면 같이 걷어낸다.
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                Debug.Log($"[물 투명벽] {water.name}: 어깨({shoulder:F2}m)까지 차는 곳이 없다. " +
                          $"얕은 물 {shallowCount}칸뿐이라 울타리를 세우지 않았다." +
                          (existing != null ? " 예전에 세운 울타리는 걷어냈다." : string.Empty));
                return 0;
            }

            // 여기서부터 진짜로 벽이 필요하다. 이제 울타리 오브젝트를 만든다.
            Transform blocker = existing;
            if (blocker == null)
            {
                blocker = new GameObject(name).transform;
                blocker.SetParent(root, false);
            }

            // Water 루트가 원점 · 무회전 · 스케일 1 이라 로컬 값이 곧 월드 값이다.
            blocker.SetPositionAndRotation(water.position, water.rotation);
            blocker.localScale = water.lossyScale;

            // 인스펙터가 보여 주는 회전 값도 같이 맞춘다.
            // 쿼터니언만 넣으면 힌트가 (0,0,0) 으로 남아, 나중에 누가 인스펙터에서
            // 회전 칸을 건드리는 순간 울타리가 통째로 엉뚱한 방향으로 튄다.
            TransformUtils.SetInspectorRotation(
                blocker, TransformUtils.GetInspectorRotation(water));

            foreach (BoxCollider stale in blocker.GetComponents<BoxCollider>())
            {
                Object.DestroyImmediate(stale);
            }

            // 로컬 두께 · 높이. 스케일이 걸리므로 월드 값을 되나눈다.
            float tx = WallThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float tz = WallThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.z));
            float h = WallHeight / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            float cy = WallCenterY / Mathf.Max(0.0001f, Mathf.Abs(scale.y));

            int walls = 0;

            // 세로 경계(로컬 x 가 일정한 선). i-1 칸과 i 칸 사이. 양끝도 본다.
            for (int i = 0; i <= nx; i++)
            {
                float x = -PlaneHalfExtent + i * lx;
                int runStart = -1;

                for (int j = 0; j <= nz; j++)
                {
                    bool edge = j < nz && IsWet(wet, i - 1, j, nx, nz) != IsWet(wet, i, j, nx, nz);

                    if (edge && runStart < 0)
                    {
                        runStart = j;
                    }
                    else if (!edge && runStart >= 0)
                    {
                        float z0 = -PlaneHalfExtent + runStart * lz;
                        float z1 = -PlaneHalfExtent + j * lz;
                        AddWall(blocker,
                            new Vector3(x, cy, (z0 + z1) * 0.5f),
                            new Vector3(tx, h, z1 - z0));
                        walls++;
                        runStart = -1;
                    }
                }
            }

            // 가로 경계(로컬 z 가 일정한 선).
            for (int j = 0; j <= nz; j++)
            {
                float z = -PlaneHalfExtent + j * lz;
                int runStart = -1;

                for (int i = 0; i <= nx; i++)
                {
                    bool edge = i < nx && IsWet(wet, i, j - 1, nx, nz) != IsWet(wet, i, j, nx, nz);

                    if (edge && runStart < 0)
                    {
                        runStart = i;
                    }
                    else if (!edge && runStart >= 0)
                    {
                        float x0 = -PlaneHalfExtent + runStart * lx;
                        float x1 = -PlaneHalfExtent + i * lx;
                        AddWall(blocker,
                            new Vector3((x0 + x1) * 0.5f, cy, z),
                            new Vector3(x1 - x0, h, tz));
                        walls++;
                        runStart = -1;
                    }
                }
            }

            Debug.Log($"[물 투명벽] {water.name}: 격자 {nx}×{nz} · 어깨({shoulder:F2}m) 이상 {wetCount}칸 " +
                      $"· 건널 수 있는 얕은 물 {shallowCount}칸 → 벽 {walls}장");
            return walls;
        }

        /// <summary>격자 밖은 마른 것으로 본다. 물 판 바깥은 물이 아니기 때문이다.</summary>
        private static bool IsWet(bool[,] wet, int i, int j, int nx, int nz)
        {
            if (i < 0 || j < 0 || i >= nx || j >= nz)
            {
                return false;
            }

            return wet[i, j];
        }

        private static void AddWall(Transform owner, Vector3 center, Vector3 size)
        {
            BoxCollider box = owner.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.isTrigger = false;   // 막아야 하므로 트리거가 아니다
        }

        /// <summary>
        /// 물 판의 로컬 좌표 한 점 아래에 있는 **밟고 설 지형의 높이**를 잰다.
        ///
        /// 물 판 자신과 다른 물 판은 빼고 본다. 물을 땅으로 세면 어디나 마른 땅이 되어
        /// 울타리가 하나도 서지 않는다.
        /// </summary>
        private static bool TrySampleGround(Transform water, Vector3 local, out float groundY)
        {
            Vector3 world = water.TransformPoint(local);

            RaycastHit[] hits = Physics.RaycastAll(
                world + Vector3.up * 50f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);

            groundY = float.MinValue;

            foreach (RaycastHit hit in hits)
            {
                if (IsWaterCollider(hit.collider))
                {
                    continue;
                }

                if (hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                }
            }

            return groundY > float.MinValue;
        }

        /// <summary>
        /// 물 판의 MeshCollider 를 끈다. 지우지 않고 끄는 이유는
        /// <see cref="Remove"/> 로 되돌릴 수 있게 하기 위해서다.
        /// </summary>
        private static void DisableWaterCollider(Transform water)
        {
            MeshCollider collider = water.GetComponent<MeshCollider>();
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
        }

        /// <summary>배치 모드 진입점. <c>-executeMethod Lobby.Editor.LobbyWaterBlocker.ReportFromCommandLine</c></summary>
        public static void ReportFromCommandLine()
        {
            Report();
        }

        /// <summary>배치 모드 진입점. <c>-executeMethod Lobby.Editor.LobbyWaterBlocker.InstallFromCommandLine</c></summary>
        public static void InstallFromCommandLine()
        {
            Install();
        }
    }
}

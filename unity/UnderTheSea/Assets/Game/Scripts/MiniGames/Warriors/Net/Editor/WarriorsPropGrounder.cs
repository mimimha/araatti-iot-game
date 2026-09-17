#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// **공중에 뜬 배경 소품을 찾아 바닥에 앉힌다.**
    ///
    /// 왜 도구로 하는가. 프리팹 좌표만 봐서는 어떤 야자수가 떠 있는지 알 수 없다.
    /// 배경에는 바위 언덕 위에 서 있는 나무도 많아서, y 값이 크다고 떠 있는 것이 아니다.
    /// 소품 <b>발밑에서 아래로 레이를 쏴</b> 바닥까지의 거리를 재면 추측 없이 가려진다.
    ///
    /// <c>ReportOnly</c> 로 먼저 목록만 뽑고, 확인한 뒤 <c>GroundProps</c> 로 내려앉힌다.
    ///
    /// ⚠ **예전 방식(<c>Collider.ClosestPoint</c>)은 못 쓴다.** 이 배경의 콜라이더 111개 중
    ///   79개가 non-convex MeshCollider 인데, Unity 는 non-convex 메시에 <c>ClosestPoint</c> 를
    ///   지원하지 않고 <b>넘긴 좌표를 그대로 돌려준다</b>. 그래서 낙차가 언제나 0 으로 나와
    ///   "떠 있는 소품 0개" 가 찍혔다 — 화면에는 떠 있는 야자수가 보이는데도.
    ///   지금은 물리 씬을 따로 만들어 <b>진짜 레이캐스트</b>를 쏜다. 레이캐스트는 non-convex 도 맞는다.
    /// </summary>
    public static class WarriorsPropGrounder
    {
        private const string ArenaPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/Arena/WarriorsBeachArena.prefab";

        /// <summary>측정에 쓰는 씬. 물리가 필요해 프리팹이 아니라 이 씬을 연다.</summary>
        private const string ScenePath =
            "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";

        /// <summary>이만큼보다 더 떠 있으면 "뜬 것" 으로 본다(m).</summary>
        private const float FloatTolerance = .35f;

        /// <summary>발밑에서 이만큼 아래까지 바닥을 찾는다(m).</summary>
        private const float ProbeDepth = 60f;

        /// <summary>
        /// 이보다 더 깊이 떨어지는 것은 <b>내리지 않고 보고만</b> 한다.
        ///
        /// 배경에는 콜라이더가 없는 바위가 있다. 그 위에 제대로 서 있는 나무는 레이가 바위를
        /// 통과해 훨씬 아래 지면을 맞히므로 "5m 떠 있음" 처럼 잡힌다. 그대로 내리면 바위 속에
        /// 파묻힌다. 실제로 떠 있는 것들은 대부분 3m 안쪽이라 여기서 끊는다.
        /// </summary>
        private const float MaxDrop = 3.5f;

        /// <summary>
        /// <see cref="MaxDrop"/> 를 넘어도 내려도 되는 것이 확인된 자리(x, z).
        ///
        /// 이 덤불은 원래 씬에서 y 2.07 에 있었고 그때 측정한 바닥이 y 0.05 였다 —
        /// 즉 <b>밑에 받쳐 주는 바위가 없다는 것이 이미 측정으로 확인된</b> 자리다.
        /// (프리팹을 잘못 건드리는 바람에 씬의 덮어쓰기가 지워져 y 8.33 으로 올라갔고,
        ///  그 뒤로는 낙차가 8.28m 로 잡혀 제한에 걸린다.)
        /// </summary>
        private static readonly Vector2[] DeepAllowed = { new(10.40f, 60.36f) };

        public static void ReportOnly() => Run(false);

        public static void GroundProps() => Run(true);

        private static void Run(bool apply)
        {
            // ⚠ 프리팹 편집용 씬(LoadPrefabContents)에는 물리가 없어 레이캐스트가 아무것도
            //   맞히지 못한다. 물리를 켠 씬을 따로 만들려 해도 SceneManager.CreateScene 은
            //   **플레이 모드 전용**이라 배치에서 거부당한다. 그래서 <b>진짜 씬을 연다</b> —
            //   에디터에서 열린 씬에는 물리 씬이 있고 에디트 모드에서도 레이가 나간다.
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[소품점검] '{ScenePath}' 를 열지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            GameObject root = null;

            foreach (GameObject one in scene.GetRootGameObjects())
            {
                if (one.name.Contains("Arena") || one.GetComponentInChildren<Renderer>(true) != null)
                {
                    root = one;
                    if (one.name.Contains("Arena")) break;
                }
            }

            if (root == null)
            {
                Debug.LogError("[소품점검] 씬에서 배경을 찾지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            // 레이를 쏘기 전에 트랜스폼을 물리 쪽에 반영한다.
            Physics.SyncTransforms();

            StringBuilder floatingReport = new();
            StringBuilder unmeasured = new();
            StringBuilder tooDeep = new();
            int plants = 0, floating = 0, moved = 0, noGround = 0;

            RaycastHit[] hits = new RaycastHit[32];

            foreach (Transform prop in root.GetComponentsInChildren<Transform>(true))
            {
                string name = prop.gameObject.name;

                bool isPlant = name.Contains("Palm") || name.Contains("Tree") || name.Contains("Bush");
                if (!isPlant || name.Contains("LOD")) continue;

                Renderer body = prop.GetComponentInChildren<Renderer>(true);
                if (body == null) continue;

                plants++;

                // 발밑(바운즈 최하단)에서 조금 위를 시작점으로 아래로 쏜다.
                Vector3 feet = new(prop.position.x, body.bounds.min.y, prop.position.z);
                Vector3 from = feet + Vector3.up * .5f;

                int count = Physics.RaycastNonAlloc(
                    from, Vector3.down, hits, ProbeDepth, ~0, QueryTriggerInteraction.Ignore);

                float best = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    Transform surface = hits[i].transform;
                    if (surface == null) continue;

                    // 자기 자신(과 자식)은 바닥이 아니다.
                    if (surface == prop || surface.IsChildOf(prop)) continue;

                    // 다른 식물 위에 서 있는 것으로 치면 안 된다. 나무끼리 서로를 바닥 삼으면
                    // 같이 떠 있는 두 그루가 둘 다 "정상" 이 된다.
                    string surfaceName = surface.gameObject.name;
                    if (surfaceName.Contains("Palm") || surfaceName.Contains("Tree") ||
                        surfaceName.Contains("Bush")) continue;

                    float drop = feet.y - hits[i].point.y;
                    if (drop < 0f) continue;
                    if (drop < best) best = drop;
                }

                // ⚠ **바닥을 하나도 못 찾은 것을 "정상" 과 섞으면 안 된다.**
                //    물 위에 떠 있고 밑에 아무 콜라이더도 없는 야자수가 여기 해당한다.
                if (best == float.MaxValue)
                {
                    noGround++;
                    unmeasured.AppendLine(
                        $"  {name,-30} 좌표({prop.position.x,7:F2},{prop.position.y,6:F2},{prop.position.z,7:F2})  밑에 바닥 없음");
                    continue;
                }

                if (best <= FloatTolerance) continue;

                floating++;
                floatingReport.AppendLine(
                    $"  {name,-30} 좌표({prop.position.x,7:F2},{prop.position.y,6:F2},{prop.position.z,7:F2})  바닥까지 {best:F2}m");

                if (best > MaxDrop && !IsDeepAllowed(prop.position))
                {
                    tooDeep.AppendLine(
                        $"  {name,-30} 좌표({prop.position.x,7:F2},{prop.position.y,6:F2},{prop.position.z,7:F2})  바닥까지 {best:F2}m — 내리지 않음");
                    continue;
                }

                if (!apply) continue;

                // ⚠ **씬에서 옮기고 씬을 저장한다. 프리팹을 고치지 않는다.**
                //
                //   한때 여기서 낙차만 모아 두고 프리팹에 <c>Transform.Find(경로)</c> 로 적용했는데,
                //   배경에는 <b>이름이 같은 야자수가 여럿</b>이라 Find 가 언제나 첫 번째 것만 돌려줬다.
                //   같은 나무를 36번 반복해서 내려 y 가 -11m 까지 파묻혔다.
                //   게다가 씬은 이 프리팹의 자식 좌표를 이미 덮어쓰고 있어서(override) 씬에서 잰
                //   값을 프리팹에 넣는 것 자체가 좌표계가 어긋난 일이었다.
                //
                //   씬의 트랜스폼은 하나씩 구분되므로 여기서 직접 옮기는 것이 정확하다.
                prop.position -= Vector3.up * best;
                moved++;
            }

            Debug.Log(
                $"[소품점검] 검사한 식물 {plants}개\n" +
                $"[소품점검] 떠 있는 소품 {floating}개\n{floatingReport}" +
                $"[소품점검] 낙차가 {MaxDrop}m 를 넘어 손대지 않은 식물\n{tooDeep}" +
                $"[소품점검] 밑에 바닥이 없어 재지 못한 식물 {noGround}개\n{unmeasured}");

            if (apply && moved > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[소품점검] {moved}개를 바닥에 앉히고 씬을 저장했습니다.");
            }

            EditorApplication.Exit(0);
        }

        private static bool IsDeepAllowed(Vector3 position)
        {
            foreach (Vector2 spot in DeepAllowed)
            {
                if (Mathf.Abs(position.x - spot.x) < .2f &&
                    Mathf.Abs(position.z - spot.y) < .2f) return true;
            }

            return false;
        }
    }
}
#endif

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 섬의 **해안선을 따라 투명 벽**을 세운다. 바다로 걸어 들어가지 못하게 한다.
    ///
    /// <b>왜 필요한가.</b>
    /// 이 씬의 바다에는 물 콜라이더가 없다. <c>OceanCollider</c> 가 있긴 한데 180° 뒤집혀 있어
    /// (회전 <c>(0, 180, 180)</c>) 레이에도 안 걸리고 실질적으로 아무것도 막지 않는다.
    /// 그래서 바다는 "물" 이 아니라 **계속 아래로 내려가는 지형**이다. 해변에서 걸어 들어가면
    /// y=1 → 0 → -5 → -9.8(<c>BaseGround</c>) 까지 그냥 걸어 내려간다.
    /// 물에 빠지는 게 아니라 바다 밑바닥을 걷는 것이고, 그 분지가 넓은 데다 군데군데
    /// 8m 넘는 바위 절벽이 있어서 되돌아 나오지 못한다.
    ///
    /// <b>어떻게 막는가.</b>
    ///   1. 섬 일대를 격자로 훑어 칸마다 **밟고 설 면의 높이**를 잰다.
    ///      맨 위 콜라이더를 보므로 부두처럼 물 위에 놓인 구조물은 마른 땅으로 잡힌다.
    ///      칸 안의 여러 점을 재서 **가장 높은 값**을 쓴다. 한가운데 한 점만 재면
    ///      레이가 부두 판자 틈으로 빠져 바다 밑바닥을 읽고, 부두 한가운데에 벽이 선다.
    ///   2. 해수면(<see cref="SeaLevel"/>)에서 잰 **물 깊이가 어깨까지 차는지**로 가른다.
    ///      어깨보다 얕으면 걸어 다닐 수 있는 곳, 그보다 깊으면 막을 곳이다.
    ///      기준 깊이는 <see cref="LobbyWaterBlocker.ShoulderDepth"/> 가 캐릭터 캡슐에서 읽어 온다.
    ///   3. 스폰 지점에서 걸어 다닐 수 있는 칸끼리 **물 흐르듯 이어 나가(flood fill)**
    ///      실제로 갈 수 있는 땅덩어리를 찾는다. 바다 한가운데 솟은 바위섬 둘레까지
    ///      막지 않기 위해서다.
    ///   4. 그 땅덩어리에서 바다 쪽으로 <see cref="SwimReach"/>(15m) 를 더 열어 **헤엄 수역**으로 삼고,
    ///      수역이 바깥 바다와 맞닿는 변에만 벽을 세운다. 벽은 바다 밑바닥까지 내려간다.
    ///      (예전에는 여기서 넓히지 않고 해안선 바로 앞에 세웠다. 헤엄이 생기기 전 이야기다)
    ///   5. **갑판 밑 바다**를 따로 막는다. 1 은 맨 위 면만 보므로 잔교 갑판 밑이 바다여도
    ///      뭍으로 잡는다. 그런데 갑판 옆에 낮은 뗏목 · 모래가 붙어 있으면 거기 선 캐릭터는
    ///      키가 갑판 밑 틈에 들어가 **갑판 아래로 걸어 들어가** 빠진다.
    ///      그래서 낮은 곳과 "밑이 바다인 갑판" 이 맞닿는 변에, 갑판 윗면보다 낮은 벽을 세운다.
    ///      갑판 위를 걷는 사람은 막지 않는다.
    ///
    /// <b>얕은 물은 막지 않는다.</b> 섬을 가로지르는 개울처럼 발목 · 무릎까지만 잠기는 곳은
    /// 그냥 걸어서 건널 수 있어야 한다. 예전에는 조금이라도 잠기면 막아서 개울도 못 건넜다.
    ///
    /// <b>벽 높이는 자리마다 다르다.</b> 해수면 기준으로 일정하게 세우면, 10m 절벽 위에
    /// 서 있는 곳에서는 벽이 발밑에 깔려 그냥 걸어 나가게 된다. 그래서 각 구간에서
    /// 가장 높은 땅 높이를 재서 그보다 <see cref="WallAboveLand"/> 만큼 더 올린다.
    ///
    /// <b>여러 번 돌려도 안전하다.</b> 이미 있으면 지우고 다시 만든다.
    ///
    /// <code>
    ///   Tools/아라아띠/로비 해안선 투명벽 세우기
    ///   Tools/아라아띠/로비 해안선 투명벽 걷어내기
    /// </code>
    /// </summary>
    public static class LobbyCoastlineBlocker
    {
        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

        /// <summary><see cref="LobbyWaterBlocker"/> 가 쓰는 루트와 같은 것을 쓴다.</summary>
        private const string BlockerRootName = "WaterBlockers";
        private const string CoastlineName = "Blocker_Coastline";

        /// <summary>
        /// 해수면 높이. <c>OceanCollider</c> 가 y=0 에 놓여 있는 것이 근거다.
        /// 이 값보다 낮은 땅은 바다로 본다.
        /// </summary>
        private const float SeaLevel = 0f;

        /// <summary>훑을 범위. 섬 전체와 그 바깥 바다까지 넉넉히 덮는다.</summary>
        private static readonly Vector2 AreaMin = new Vector2(-200f, -120f);
        private static readonly Vector2 AreaMax = new Vector2(240f, 320f);

        /// <summary>격자 한 칸(m). 캡슐 반지름 0.35m 보다 크지만, 벽이 변 전체를 덮어 틈은 없다.</summary>
        private const float CellSize = 1f;

        /// <summary>
        /// 칸 안에서 잴 점의 간격(m). 칸 가장자리(±0.5)에 선 벽(두께 0.4)과 겹치지 않을 만큼 안쪽이다.
        /// </summary>
        private const float CellSampleOffset = 0.25f;

        /// <summary>스폰 지점. 여기서부터 뭍을 이어 나간다.</summary>
        private static readonly Vector3 SpawnHint = new Vector3(20.84f, 1.73f, 48.56f);

        private const float WallThickness = 0.4f;

        /// <summary>땅 높이보다 이만큼 더 높이 세운다. 점프(1.2m)로 넘을 수 없어야 한다.</summary>
        private const float WallAboveLand = 5f;

        /// <summary>
        /// 해수면보다 이만큼 아래까지 내린다.
        /// 벽이 서는 자리는 물이 어깨까지 차는 선(해수면에서 1m 남짓 아래)이라,
        /// 그보다 더 내려가야 발밑으로 빠져나가지 못한다.
        /// </summary>
        private const float WallBelowSea = 3f;

        /// <summary>
        /// **헤엄칠 수 있는 바다의 폭(m).** 물이 어깨까지 차는 해안선에서 이만큼 바깥에 벽을 세운다.
        ///
        /// 예전에는 해안선 바로 앞(어깨 깊이)에 벽을 세웠다. 바다에 빠지면 되돌아 나오지 못했기 때문이다.
        /// 이제 <c>NetworkPlayerMover</c> 가 헤엄 · 잠수(Space 로 위아래)를 하므로 깊은 바다에서도 돌아올 수 있다.
        ///
        /// 15m 인 이유(<c>Tools/아라아띠/로비 바깥 바다 깊이 진단</c>, 2026-09-25):
        ///   해안에서  5m  가운데 깊이 1.8m — 캐릭터(1.2m)가 겨우 잠긴다
        ///            15m  가운데 깊이 5.1m — 잠수하면 머리 위로 몇 m 가 남는다
        ///            25m~ 바다 밑 최저선(−9.8m)에 닿고 물 위로 솟은 바위 · 섬이 늘어난다
        /// 물속 안개가 8m 앞을 절반쯤 가리므로 그보다 멀리 열어도 보이는 것은 평평한 바닥뿐이다.
        ///
        /// 0 으로 두면 예전처럼 해안선 바로 앞에 세운다.
        /// </summary>
        private const float SwimReach = 15f;

        /// <summary>
        /// 헤엄 수역을 넓혀 갈 때 **이보다 높은 땅(수면 기준 m)은 넘지 않고, 그쪽으로는 벽도 세우지 않는다.**
        /// 바다에서 솟은 절벽 · 바위는 지형이 이미 막는다. 해안선 벽이 "닿지 못하는 뭍" 쪽에 벽을 안 세우는 것과 같은 이유다.
        /// 이보다 낮은 바위는 올라설 수 있으므로 수역에 넣고, 수역 끝에 걸리면 벽을 세운다.
        /// </summary>
        private const float SwimHighGround = 1.5f;

        /// <summary>헤엄 수역 벽을 바닥보다 이만큼 더 내린다. 바닥에 붙어 헤엄쳐도 밑으로 못 빠져나간다.</summary>
        private const float WallBelowSeabed = 1f;

        /// <summary>갑판 밑 벽을 갑판 윗면보다 이만큼 낮게 둔다. 갑판 위를 걷는 발에 걸리면 안 된다.</summary>
        private const float DeckClearance = 0.1f;

        /// <summary>
        /// 갑판으로 치는 부두 조각의 이름 앞부분. 밑이 비어 있어 캐릭터가 들어갈 수 있는 것만 넣는다.
        ///
        /// 이름으로 고르는 이유: 모든 메시에 "밑이 비었나" 를 재 보면, 터레인 위에 놓인 소품 ·
        /// 땅에 묻힌 바위 밑면까지 갑판으로 잡혀 섬 곳곳에 엉뚱한 벽이 선다. 실제로 그랬다.
        /// <c>SM_Env_Dock_Stairs</c> 는 땅에 걸친 계단이라 빠진다(<c>_0</c> 까지 맞춰 거른다).
        /// </summary>
        private static readonly string[] DeckPrefixes = { "SM_Env_Dock_0", "Plank_" };

        [MenuItem("Tools/아라아띠/로비 해안선 투명벽 세우기")]
        public static void Install()
        {
            Scene scene = OpenLobby();

            int nx = Mathf.CeilToInt((AreaMax.x - AreaMin.x) / CellSize);
            int nz = Mathf.CeilToInt((AreaMax.y - AreaMin.y) / CellSize);

            // 물이 어깨까지 차면 막는다. 그보다 얕으면 걸어서 지나갈 수 있어야 한다.
            float shoulder = LobbyWaterBlocker.ShoulderDepth();
            float capsule = LobbyWaterBlocker.CapsuleHeight();

            // 1) 칸마다 밟고 설 면의 높이를 잰다. 갑판 밑 바다(5단계)에 쓸 값도 같이 잰다.
            var height = new float[nx, nz];
            var walkable = new bool[nx, nz];
            var deckTop = new float[nx, nz];
            var lowStand = new float[nx, nz];
            int walkableCount = 0;
            int shallowCount = 0;

            var buffer = new RaycastHit[32];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!TrySampleCell(i, j, buffer, capsule, shoulder,
                            out float y, out deckTop[i, j], out lowStand[i, j]))
                    {
                        // 아무것도 없는 칸은 막을 곳으로 친다. 허공으로 걸어 나가면 안 된다.
                        height[i, j] = float.MinValue;
                        continue;
                    }

                    height[i, j] = y;

                    if (!IsTooDeep(y, shoulder))
                    {
                        walkable[i, j] = true;
                        walkableCount++;

                        if (y < SeaLevel)
                        {
                            shallowCount++;
                        }
                    }
                }
            }

            // 2) 스폰에서 이어 나가 실제로 걸어 다닐 수 있는 땅덩어리를 찾는다.
            if (!TryCellOf(SpawnHint, nx, nz, out int si, out int sj) || !walkable[si, sj])
            {
                Debug.LogError($"[해안선] 스폰 지점 {SpawnHint} 이 걸어 다닐 수 있는 칸이 아니다. " +
                               $"해수면({SeaLevel}) · 어깨 깊이({shoulder:F2}m) 값을 확인해라.");
                return;
            }

            var land = new bool[nx, nz];
            int landCount = 0;
            var queue = new Queue<(int i, int j)>();
            queue.Enqueue((si, sj));
            land[si, sj] = true;
            landCount++;

            var steps = new (int di, int dj)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            while (queue.Count > 0)
            {
                (int ci, int cj) = queue.Dequeue();

                foreach ((int di, int dj) in steps)
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz) continue;
                    if (land[ni, nj] || !walkable[ni, nj]) continue;

                    land[ni, nj] = true;
                    landCount++;
                    queue.Enqueue((ni, nj));
                }
            }

            Debug.Log($"[해안선] 격자 {nx}×{nz} ({CellSize}m) · 어깨 깊이 {shoulder:F2}m 기준 · " +
                      $"지나다닐 수 있는 칸 {walkableCount}(그중 걸어서 건너는 얕은 물 {shallowCount}) 중 " +
                      $"스폰에서 실제로 갈 수 있는 곳 {landCount}칸");

            // 2-1) 해안선에서 SwimReach 만큼 바다 쪽으로 넓힌다. 벽은 넓힌 수역의 끝에 선다.
            bool[,] region = SwimReach > 0f
                ? ExpandIntoSea(land, height, shoulder, nx, nz, out int swimCells)
                : land;

            if (SwimReach > 0f)
            {
                Debug.Log($"[해안선] 헤엄 수역 {SwimReach:F0}m — 바다 쪽으로 {swimCells}칸을 더 열었다.");
            }

            // 3) 갈 수 있는 곳과 깊은 물이 맞닿는 변에만 벽을 세운다.
            Transform blocker = PrepareBlocker(scene);
            int walls = 0;

            // 세로 경계 — 로컬 x 가 일정한 선. i-1 칸과 i 칸 사이.
            for (int i = 0; i <= nx; i++)
            {
                int runStart = -1;
                float runTop = float.MinValue;
                float runBottom = float.MaxValue;

                for (int j = 0; j <= nz; j++)
                {
                    bool edge = false;
                    float top = float.MinValue;
                    float bottom = float.MaxValue;

                    if (j < nz)
                    {
                        edge = WallBetween(region, land, height, shoulder, i - 1, j, i, j, nx, nz, out top, out bottom);
                    }

                    if (edge)
                    {
                        if (runStart < 0) { runStart = j; runTop = float.MinValue; runBottom = float.MaxValue; }
                        runTop = Mathf.Max(runTop, top);
                        runBottom = Mathf.Min(runBottom, bottom);
                    }
                    else if (runStart >= 0)
                    {
                        float x = AreaMin.x + i * CellSize;
                        float z0 = AreaMin.y + runStart * CellSize;
                        float z1 = AreaMin.y + j * CellSize;
                        AddWall(blocker, x, (z0 + z1) * 0.5f, WallThickness, z1 - z0, runTop, runBottom);
                        walls++;
                        runStart = -1;
                    }
                }
            }

            // 가로 경계 — 로컬 z 가 일정한 선.
            for (int j = 0; j <= nz; j++)
            {
                int runStart = -1;
                float runTop = float.MinValue;
                float runBottom = float.MaxValue;

                for (int i = 0; i <= nx; i++)
                {
                    bool edge = false;
                    float top = float.MinValue;
                    float bottom = float.MaxValue;

                    if (i < nx)
                    {
                        edge = WallBetween(region, land, height, shoulder, i, j - 1, i, j, nx, nz, out top, out bottom);
                    }

                    if (edge)
                    {
                        if (runStart < 0) { runStart = i; runTop = float.MinValue; runBottom = float.MaxValue; }
                        runTop = Mathf.Max(runTop, top);
                        runBottom = Mathf.Min(runBottom, bottom);
                    }
                    else if (runStart >= 0)
                    {
                        float z = AreaMin.y + j * CellSize;
                        float x0 = AreaMin.x + runStart * CellSize;
                        float x1 = AreaMin.x + i * CellSize;
                        AddWall(blocker, (x0 + x1) * 0.5f, z, x1 - x0, WallThickness, runTop, runBottom);
                        walls++;
                        runStart = -1;
                    }
                }
            }

            // 4) 낮은 곳과 "밑이 바다인 갑판" 이 맞닿는 변에 갑판보다 낮은 벽을 세운다.
            int deckWalls = AddUnderDeckWalls(blocker, land, deckTop, lowStand, capsule, nx, nz);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[해안선] 세웠다. 벽 {walls}장 · 갑판 밑 벽 {deckWalls}장. 씬을 저장했다.");
        }

        [MenuItem("Tools/아라아띠/로비 해안선 투명벽 걷어내기")]
        public static void Remove()
        {
            Scene scene = OpenLobby();

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == BlockerRootName);
            Transform existing = root != null ? root.transform.Find(CoastlineName) : null;

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[해안선] 걷어냈다. 씬을 저장했다.");
        }

        /// <summary>물이 어깨까지 차는가. 밟을 것이 아예 없는 칸도 막을 곳으로 친다.</summary>
        private static bool IsTooDeep(float groundY, float shoulder)
        {
            return SeaLevel - groundY >= shoulder;
        }

        /// <summary>
        /// 두 칸 사이에 벽이 필요한지. **갈 수 있는 곳과 깊은 물이 맞닿을 때만** 세운다.
        ///
        /// 갈 수 있는 곳과 "닿지 못하는 뭍"(절벽 너머 바위 등)이 맞닿는 곳에는 세우지 않는다.
        /// 거기는 지형이 이미 막고 있어서, 벽을 세우면 뭍 한가운데 보이지 않는 벽이 생긴다.
        /// </summary>
        private static bool NeedsWall(
            bool[,] land, float[,] height, float shoulder,
            int ai, int aj, int bi, int bj, int nx, int nz, out float topLand)
        {
            topLand = float.MinValue;

            bool aLand = Inside(ai, aj, nx, nz) && land[ai, aj];
            bool bLand = Inside(bi, bj, nx, nz) && land[bi, bj];

            // 둘 다 갈 수 있거나 둘 다 아니면 경계가 아니다.
            if (aLand == bLand)
            {
                return false;
            }

            // 반대쪽 칸이 "어깨까지 차는 물" 또는 "격자 밖" 일 때만 막는다.
            int oi = aLand ? bi : ai;
            int oj = aLand ? bj : aj;

            if (Inside(oi, oj, nx, nz) && !IsTooDeep(height[oi, oj], shoulder))
            {
                // 얕지만 스폰에서 닿지 못하는 곳이다. 지형이 이미 막고 있으니 벽은 필요 없다.
                return false;
            }

            topLand = height[aLand ? ai : bi, aLand ? aj : bj];
            return true;
        }

        /// <summary>
        /// 해안선(<paramref name="land"/>)에서 **바다 쪽으로 <see cref="SwimReach"/> m 안의 칸**을 더한 수역을 돌려준다.
        ///
        /// 해안에 맞닿은 깊은 물에서 시작해 물 흐르듯 넓혀 간다. 칸마다 "가장 가까운 해안 칸" 을 물려받아
        /// 그 칸과의 직선 거리로 잰다. 한 칸씩 센 거리로 재면 대각선 쪽이 덜 나가 벽이 마름모꼴이 된다.
        /// <see cref="SwimHighGround"/> 보다 높은 땅은 넘지 않는다(지형이 막는다).
        /// </summary>
        private static bool[,] ExpandIntoSea(
            bool[,] land, float[,] height, float shoulder, int nx, int nz, out int added)
        {
            var region = (bool[,])land.Clone();
            var source = new (int i, int j)[nx, nz];
            var queue = new Queue<(int i, int j)>();
            var steps = new (int di, int dj)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            float reachCells = SwimReach / CellSize;
            added = 0;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!land[i, j]) continue;

                    foreach ((int di, int dj) in steps)
                    {
                        int ni = i + di, nj = j + dj;
                        if (!Inside(ni, nj, nx, nz) || region[ni, nj] || !CanSwimInto(height[ni, nj])) continue;

                        region[ni, nj] = true;
                        source[ni, nj] = (i, j);
                        added++;
                        queue.Enqueue((ni, nj));
                    }
                }
            }

            while (queue.Count > 0)
            {
                (int ci, int cj) = queue.Dequeue();
                (int si, int sj) = source[ci, cj];

                foreach ((int di, int dj) in steps)
                {
                    int ni = ci + di, nj = cj + dj;
                    if (!Inside(ni, nj, nx, nz) || region[ni, nj] || !CanSwimInto(height[ni, nj])) continue;

                    float dx = ni - si, dz = nj - sj;
                    if (dx * dx + dz * dz > reachCells * reachCells) continue;

                    region[ni, nj] = true;
                    source[ni, nj] = (si, sj);
                    added++;
                    queue.Enqueue((ni, nj));
                }
            }

            return region;
        }

        /// <summary>헤엄 수역에 넣을 칸인가. 밟을 것이 있고, 넘을 수 없을 만큼 높은 땅이 아니어야 한다.</summary>
        private static bool CanSwimInto(float groundY)
        {
            return groundY > float.MinValue && groundY < SeaLevel + SwimHighGround;
        }

        /// <summary>
        /// 두 칸 사이에 벽이 필요한지. <see cref="SwimReach"/> 가 0 이면 예전 규칙(<see cref="NeedsWall"/>)을 그대로 쓴다.
        ///
        /// 헤엄 수역이면 **수역 안과 밖이 맞닿는 곳 모두**에 세운다. 다만 밖이 높은 땅
        /// (<see cref="SwimHighGround"/> 이상)이면 지형이 막으므로 세우지 않는다.
        /// 벽 아래끝(<paramref name="bottom"/>)은 두 칸 가운데 낮은 바닥보다 <see cref="WallBelowSeabed"/> 만큼 더 내린다.
        /// </summary>
        private static bool WallBetween(
            bool[,] region, bool[,] land, float[,] height, float shoulder,
            int ai, int aj, int bi, int bj, int nx, int nz, out float topLand, out float bottom)
        {
            bottom = SeaLevel - WallBelowSea;

            if (SwimReach <= 0f)
            {
                return NeedsWall(land, height, shoulder, ai, aj, bi, bj, nx, nz, out topLand);
            }

            topLand = float.MinValue;

            bool aIn = Inside(ai, aj, nx, nz) && region[ai, aj];
            bool bIn = Inside(bi, bj, nx, nz) && region[bi, bj];

            if (aIn == bIn)
            {
                return false;
            }

            int ii = aIn ? ai : bi, ij = aIn ? aj : bj;
            int oi = aIn ? bi : ai, oj = aIn ? bj : aj;

            float outside = Inside(oi, oj, nx, nz) ? height[oi, oj] : float.MinValue;

            if (outside >= SeaLevel + SwimHighGround)
            {
                return false;
            }

            topLand = height[ii, ij];

            float lowest = outside > float.MinValue ? Mathf.Min(height[ii, ij], outside) : height[ii, ij];
            bottom = Mathf.Min(bottom, lowest - WallBelowSeabed);
            return true;
        }

        private static bool Inside(int i, int j, int nx, int nz)
        {
            return i >= 0 && j >= 0 && i < nx && j < nz;
        }

        private static Vector3 CellCenter(int i, int j)
        {
            return new Vector3(
                AreaMin.x + (i + 0.5f) * CellSize, 0f,
                AreaMin.y + (j + 0.5f) * CellSize);
        }

        private static bool TryCellOf(Vector3 world, int nx, int nz, out int i, out int j)
        {
            i = Mathf.FloorToInt((world.x - AreaMin.x) / CellSize);
            j = Mathf.FloorToInt((world.z - AreaMin.y) / CellSize);
            return Inside(i, j, nx, nz);
        }

        /// <summary>
        /// 칸 안에서 <see cref="CellSampleOffset"/> 간격 3×3 점을 재, 밟고 설 면이 가장 높은 값을 돌려준다.
        /// 한 점이라도 부두 판자에 닿으면 그 칸은 부두다.
        ///
        /// 갑판 밑 바다(5단계)에 쓸 값도 같이 돌려준다. 없으면 <see cref="float.PositiveInfinity"/>.
        ///   <paramref name="deckTop"/>   밑이 바다인 갑판 점들 가운데 가장 낮은 윗면
        ///   <paramref name="lowStand"/>  그 밖의 점들에서 캐릭터가 설 수 있는 가장 낮은 면.
        ///                                밑이 마른 갑판이면 갑판 밑 모래로 친다. 깊은 물은 빼고 잰다
        /// </summary>
        private static bool TrySampleCell(
            int i, int j, RaycastHit[] buffer, float capsule, float shoulder,
            out float y, out float deckTop, out float lowStand)
        {
            Vector3 center = CellCenter(i, j);
            y = float.MinValue;
            deckTop = float.PositiveInfinity;
            lowStand = float.PositiveInfinity;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3 at = center + new Vector3(dx * CellSampleOffset, 0f, dz * CellSampleOffset);

                    if (!SampleColumn(at, buffer, capsule, out float top, out float below, out bool isDeck))
                    {
                        continue;
                    }

                    y = Mathf.Max(y, top);

                    bool hasBelow = isDeck && below > float.MinValue;

                    float stand = hasBelow ? below : top;

                    if (hasBelow && IsTooDeep(below, shoulder))
                    {
                        deckTop = Mathf.Min(deckTop, top);
                    }
                    else if (!IsTooDeep(stand, shoulder))
                    {
                        // 판자 틈으로 보이는 바다는 설 자리가 아니다.
                        lowStand = Mathf.Min(lowStand, stand);
                    }
                }
            }

            return y > float.MinValue;
        }

        /// <summary>
        /// 한 점 위에서 아래로 쏴, 밟고 설 맨 위 면의 높이를 돌려준다.
        /// 이미 세워 둔 투명벽은 땅이 아니므로 건너뛴다. 다시 세울 때 벽 꼭대기를 땅으로 읽으면 안 된다.
        /// </summary>
        private static bool TrySampleGround(Vector3 at, RaycastHit[] buffer, out float y)
        {
            return SampleColumn(at, buffer, float.PositiveInfinity, out y, out _, out _);
        }

        /// <summary>
        /// 한 점 위에서 아래로 쏴 맨 위 면(<paramref name="top"/>)을 잰다.
        /// 맨 위가 부두 조각(<see cref="DeckPrefixes"/>)이면 <paramref name="isDeck"/> 이 참이고,
        /// 그보다 캐릭터 키(<paramref name="capsule"/>) 이상 아래에 있는 가장 높은 면을
        /// <paramref name="below"/> 로 돌려준다. 갑판 밑에 캐릭터가 들어가 설 자리다.
        /// </summary>
        private static bool SampleColumn(
            Vector3 at, RaycastHit[] buffer, float capsule,
            out float top, out float below, out bool isDeck)
        {
            int count = Physics.RaycastNonAlloc(
                new Vector3(at.x, 150f, at.z), Vector3.down, buffer, 400f, ~0, QueryTriggerInteraction.Ignore);

            top = float.MinValue;
            below = float.MinValue;
            isDeck = false;
            Collider topCollider = null;

            for (int k = 0; k < count; k++)
            {
                if (buffer[k].collider.transform.root.name == BlockerRootName)
                {
                    continue;
                }

                if (buffer[k].point.y > top)
                {
                    top = buffer[k].point.y;
                    topCollider = buffer[k].collider;
                }
            }

            if (topCollider == null)
            {
                return false;
            }

            isDeck = DeckPrefixes.Any(p => topCollider.name.StartsWith(p));
            if (!isDeck)
            {
                return true;
            }

            for (int k = 0; k < count; k++)
            {
                float hy = buffer[k].point.y;
                if (buffer[k].collider.transform.root.name == BlockerRootName || hy > top - capsule)
                {
                    continue;
                }

                below = Mathf.Max(below, hy);
            }

            return true;
        }

        /// <summary>
        /// 5단계. **낮은 곳과 "밑이 바다인 갑판" 이 맞닿는 변**에 벽을 세운다.
        ///
        /// 낮은 쪽(<paramref name="lowStand"/>)에 선 캐릭터의 키가 갑판 밑으로 들어갈 때만 세운다.
        /// 그렇지 않으면 캐릭터는 갑판 위로 올라서므로 막을 까닭이 없다(잔교가 뭍에 닿는 끝 등).
        /// 벽 꼭대기는 갑판 윗면보다 <see cref="DeckClearance"/> 낮다. 갑판 위를 걷는 발에 걸리지 않는다.
        /// </summary>
        private static int AddUnderDeckWalls(
            Transform owner, bool[,] land, float[,] deckTop, float[,] lowStand, float capsule, int nx, int nz)
        {
            int walls = 0;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    // 오른쪽(i+1) · 위쪽(j+1) 이웃과의 변만 본다. 변 하나를 두 번 세지 않는다.
                    foreach ((int di, int dj) in new[] { (1, 0), (0, 1) })
                    {
                        int ni = i + di, nj = j + dj;
                        if (!Inside(ni, nj, nx, nz)) continue;

                        float top = Mathf.Min(
                            UnderDeckWallTop(land, deckTop, lowStand, capsule, i, j, ni, nj),
                            UnderDeckWallTop(land, deckTop, lowStand, capsule, ni, nj, i, j));

                        if (float.IsPositiveInfinity(top)) continue;

                        float bottom = SeaLevel - WallBelowSea;

                        if (di == 1)
                        {
                            AddBox(owner, AreaMin.x + ni * CellSize, AreaMin.y + (j + 0.5f) * CellSize,
                                WallThickness, CellSize, bottom, top);
                        }
                        else
                        {
                            AddBox(owner, AreaMin.x + (i + 0.5f) * CellSize, AreaMin.y + nj * CellSize,
                                CellSize, WallThickness, bottom, top);
                        }

                        walls++;
                    }
                }
            }

            return walls;
        }

        /// <summary>
        /// 낮은 칸 a 에서 갑판 칸 b 로 걸어 들어가면 갑판 밑 바다에 빠지는가.
        /// 빠지면 벽 꼭대기 높이를, 아니면 <see cref="float.PositiveInfinity"/> 를 돌려준다.
        /// </summary>
        private static float UnderDeckWallTop(
            bool[,] land, float[,] deckTop, float[,] lowStand, float capsule, int ai, int aj, int bi, int bj)
        {
            if (!land[ai, aj] || float.IsPositiveInfinity(lowStand[ai, aj]) || float.IsPositiveInfinity(deckTop[bi, bj]))
            {
                return float.PositiveInfinity;
            }

            if (deckTop[bi, bj] - lowStand[ai, aj] < capsule)
            {
                return float.PositiveInfinity;
            }

            return deckTop[bi, bj] - DeckClearance;
        }

        /// <summary>
        /// 벽 하나. 아래는 <paramref name="bottom"/>(해수면 밑 · 헤엄 수역이면 바다 밑바닥 밑)까지,
        /// 위는 그 구간에서 가장 높은 땅보다 더 높이 세운다.
        /// </summary>
        private static void AddWall(Transform owner, float x, float z, float sizeX, float sizeZ, float topLand, float bottom)
        {
            float top = Mathf.Max(topLand, SeaLevel) + WallAboveLand;

            AddBox(owner, x, z, sizeX, sizeZ, bottom, top);
        }

        private static void AddBox(Transform owner, float x, float z, float sizeX, float sizeZ, float bottom, float top)
        {
            BoxCollider box = owner.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(x, (bottom + top) * 0.5f, z);
            box.size = new Vector3(sizeX, top - bottom, sizeZ);
            box.isTrigger = false;
        }

        private static Transform PrepareBlocker(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == BlockerRootName);
            if (root == null)
            {
                root = new GameObject(BlockerRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            Transform existing = root.transform.Find(CoastlineName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(CoastlineName);
            go.transform.SetParent(root.transform, false);
            return go.transform;
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

        /// <summary>
        /// <c>OceanCollider</c> 의 MeshCollider 를 끈다. **얕은 바다에서 끼는 원인이다.**
        ///
        /// 이 판은 y=0 에 깔린 큰 사각형인데 180° 뒤집혀 있어 레이에는 안 걸린다.
        /// 그래서 "아무것도 안 하는 물건" 처럼 보이지만, 캐릭터 충돌은 삼각형 메시 양면에
        /// 모두 걸린다. 물이 어깨까지 차기 전까지 들어갈 수 있게 풀어 준 뒤로는
        /// 캡슐(발끝 0 ~ 정수리 1.2)이 y=0 을 가로지르게 되어, 강에서 겪었던 끼임이
        /// 똑같이 재현된다. skinWidth 가 0.0001 이라 수평 메시에서 빠져나오지 못한다.
        ///
        /// 끄더라도 잃는 것이 없다. 바다 밑은 Terrain 이 계속 이어지고,
        /// 해안선 울타리와 <c>NetworkPlayerMover</c> 의 구조 장치가 이미 바깥을 막는다.
        ///
        /// 실제로 캡슐을 놓아 보고 무엇에 걸리는지 끄기 전후로 찍어 남긴다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 바다 판 콜라이더 끄기")]
        public static void DisableOceanCollider()
        {
            Scene scene = OpenLobby();

            GameObject ocean = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "OceanCollider")?.gameObject;

            if (ocean == null)
            {
                Debug.LogWarning("[바다 판] 씬에서 OceanCollider 를 못 찾았다.");
                return;
            }

            Collider oceanCollider = ocean.GetComponent<Collider>();
            if (oceanCollider == null)
            {
                Debug.Log("[바다 판] 콜라이더가 없다. 할 일이 없다.");
                return;
            }

            ReportCapsuleHits("끄기 전");

            oceanCollider.enabled = false;

            ReportCapsuleHits("끈 뒤");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[바다 판] OceanCollider 의 콜라이더를 껐다. 씬을 저장했다.");
        }

        /// <summary>
        /// 얕은 바다 여러 곳에 플레이어 캡슐을 놓아 보고 **무엇과 겹치는지** 찍는다.
        /// 겹치는 것이 있으면 거기 선 캐릭터는 그 면에 끼어 못 움직인다.
        /// </summary>
        private static void ReportCapsuleHits(string label)
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab");
            CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;

            float height = cc != null ? cc.height : 1.2f;
            float radius = cc != null ? cc.radius : 0.35f;

            var buffer = new RaycastHit[32];
            int checkedSpots = 0, blocked = 0;
            var names = new HashSet<string>();

            // 얕은 바다(깊이 0 ~ 어깨) 칸을 찾아 캡슐을 놓아 본다.
            float shoulder = LobbyWaterBlocker.ShoulderDepth();

            for (float x = AreaMin.x; x <= AreaMax.x && checkedSpots < 40; x += 3f)
            {
                for (float z = AreaMin.y; z <= AreaMax.y && checkedSpots < 40; z += 3f)
                {
                    if (!TrySampleGround(new Vector3(x, 0f, z), buffer, out float ground))
                    {
                        continue;
                    }

                    float depth = SeaLevel - ground;
                    if (depth <= 0.1f || depth >= shoulder)
                    {
                        continue;
                    }

                    checkedSpots++;

                    // CharacterController 캡슐과 같은 모양. 발끝이 지면에 닿게 놓는다.
                    Vector3 bottom = new Vector3(x, ground + radius, z);
                    Vector3 top = new Vector3(x, ground + height - radius, z);

                    Collider[] hits = Physics.OverlapCapsule(
                        bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);

                    foreach (Collider h in hits)
                    {
                        if (h == null) continue;
                        names.Add(h.gameObject.name);
                        blocked++;
                    }
                }
            }

            Debug.Log($"[바다 판] {label} — 얕은 바다 {checkedSpots}곳에 캡슐을 놓아 봄. " +
                      $"겹친 콜라이더 {blocked}건" +
                      (names.Count > 0 ? $" → {string.Join(", ", names.Take(8))}" : " (없음 = 안 낀다)"));
        }

        /// <summary>배치 모드 진입점.</summary>
        public static void InstallFromCommandLine()
        {
            Install();
        }

        /// <summary>배치 모드 진입점.</summary>
        public static void DisableOceanColliderFromCommandLine()
        {
            DisableOceanCollider();
        }
    }
}

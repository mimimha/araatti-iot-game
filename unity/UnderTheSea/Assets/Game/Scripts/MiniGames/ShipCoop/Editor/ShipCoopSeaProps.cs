using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🌊 바다 위 시각물 세 가지를 만들고 씬에 잇는다. 배치 도구(<c>ShipCoopDeckLayout.Build</c>)의 한 단계. (SHIPCOOP.md 5장)
///
/// <code>
///   🏴‍☠️ 적선        P_EnemyShip.prefab          StylShip_Unity 를 중첩하고 재질만 우리 것(M_Ship_*_01)으로. 돛은 검은 해골 그대로
///   🏝 목적지 섬     P_DestinationIsland.prefab   섬덩이(Mountain) + 화산 + 바위 아치 + 야자수. 피벗은 수면 높이의 섬 중심
///   ⛵ 우리 배 돛    P_PirateShip.prefab          돛 · 깃발 재질 슬롯만 미색 돛(M_Ship_SailsRope_White_01)으로 덮어쓴다
/// </code>
///
/// 그리고 씬의 <c>VoyageSea.islandPrefab</c> 과 <c>Event_EnemyShip</c> 옆 <c>EnemyShipVisual</c> 의 참조를 채운다.
/// 여러 번 돌려도 같은 결과다 — 프리팹은 같은 경로에 덮어쓰고(GUID 유지), 컴포넌트는 있으면 값만 다시 맞춘다.
///
/// ⚠ 원본 StylShip 재질 · M_Ship_*_01 · T_Ship_*_BC.png 는 고치지 않는다. 사본만 만든다.
/// </summary>
public static class ShipCoopSeaProps
{
    private const string ShipModelPath = "Assets/Stylized_Pirate_Ship/StylShip_Unity.prefab";
    private const string OurShipPrefabPath = "Assets/Game/Prefabs/PirateShip/P_PirateShip.prefab";
    private const string EnemyPrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/P_EnemyShip.prefab";
    private const string IslandPrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/P_DestinationIsland.prefab";
    private const string SmokePath = "Assets/Synty/PolygonGeneric/Prefabs/FX/FX_Smoke_01.prefab";

    private const string MaterialFolder = "Assets/Game/Prefabs/PirateShip/";

    /// <summary>StylShip 원본 재질 이름의 조각 → 우리 URP 재질. P_PirateShip 이 덮어쓰는 것과 같은 짝.</summary>
    private static readonly (string piece, string material)[] MaterialMap =
    {
        ("ShipHull", "M_Ship_Hull_01"),
        ("Decks", "M_Ship_Decks_01"),
        ("Masts", "M_Ship_Masts_01"),
        ("Props", "M_Ship_Props_01"),
        ("SailsRope", "M_Ship_SailsRope_01"),
        ("Elements", "M_Ship_Elements_01"),
    };

    // 섬 부품
    private const string MountainPath = "Assets/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Mountain_01.prefab";
    private const string VolcanoPath = "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Volcano_01.prefab";
    private const string ArchPath = "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Rock_Arch_01.prefab";
    private static readonly string[] PalmPaths =
    {
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Tree_Palm_01.prefab",
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Tree_Palm_02.prefab",
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Tree_Palm_03.prefab",
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Tree_Palm_04.prefab",
    };

    /// <summary>섬덩이 배율. 162 × 23 × 108 → 81 × 12 × 54.</summary>
    private const float MountainScale = 0.5f;

    /// <summary>섬덩이를 이만큼 가라앉힌다(m). 해안선이 물 아래로 들어가 가장자리가 잘려 보이지 않는다.</summary>
    private const float MountainSink = 2f;

    /// <summary>화산 배율. 17m → 42m. 400m 밖 수평선에서 실루엣을 맡는다.</summary>
    private const float VolcanoScale = 2.5f;

    /// <summary>화산 밑을 섬덩이 꼭대기에 이만큼 묻는다(m). 떠 있으면 얹어 놓은 것처럼 보인다.</summary>
    private const float VolcanoBury = 3f;

    private const float PalmScale = 1.5f;
    private const int PalmCount = 6;

    public static void SetUp(StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("  🌊 바다 위 시각물");

        GameObject enemy = BuildEnemyShipPrefab(log);
        GameObject island = BuildIslandPrefab(log);
        WhitenOurSails(log);
        WireScene(enemy, island, log);
    }

    // ------------------------------------------------------------
    // 🏴‍☠️ 적선 프리팹
    // ------------------------------------------------------------

    private static GameObject BuildEnemyShipPrefab(StringBuilder log)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);

        if (model == null)
        {
            log.AppendLine($"    ⚠ 배 모델을 못 찾음: {ShipModelPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        }

        GameObject root = new GameObject("P_EnemyShip");

        try
        {
            GameObject ship = (GameObject)PrefabUtility.InstantiatePrefab(model);
            ship.transform.SetParent(root.transform, false);
            ship.transform.localPosition = Vector3.zero;
            ship.transform.localRotation = Quaternion.identity;
            ship.transform.localScale = Vector3.one;

            int swapped = 0;
            int kept = 0;

            foreach (Renderer draw in ship.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = draw.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    Material ours = OurMaterialFor(mats[i]);

                    if (ours != null && ours != mats[i])
                    {
                        mats[i] = ours;
                        changed = true;
                    }
                }

                if (changed)
                {
                    draw.sharedMaterials = mats;
                    swapped++;
                }
                else
                {
                    kept++;
                }
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath, out bool ok);

            if (!ok)
            {
                log.AppendLine($"    ⚠ 적선 프리팹 저장 실패: {EnemyPrefabPath}");
                return null;
            }

            log.AppendLine($"    🏴‍☠️ 적선 프리팹 — 렌더러 {swapped}개 재질을 우리 것으로 (그대로 {kept}개), 돛은 검은 해골 그대로 → {EnemyPrefabPath}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>StylShip 원본 재질(Built-in, URP 에서 마젠타)을 이름으로 짝지어 우리 URP 재질로.</summary>
    private static Material OurMaterialFor(Material original)
    {
        if (original == null)
        {
            return null;
        }

        for (int i = 0; i < MaterialMap.Length; i++)
        {
            if (original.name.Contains(MaterialMap[i].piece))
            {
                return AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + MaterialMap[i].material + ".mat");
            }
        }

        return null;
    }

    // ------------------------------------------------------------
    // 🏝 목적지 섬 프리팹
    // ------------------------------------------------------------

    /// <summary>
    /// 피벗은 <b>수면 높이의 섬 중심</b>. <c>VoyageSea.SpawnIsland</c> 가 y 0 에 놓고 z 400 → 25 로 당긴다.
    ///
    /// <code>
    ///   섬덩이   Mountain_01 × 0.5, 2m 가라앉힘          덩어리. 해안선이 물 아래
    ///   봉우리   Volcano_01 × 2.5 (42m)                 섬덩이 가장 높은 곳 위. 400m 밖 실루엣 담당
    ///   랜드마크 Rock_Arch_01 × 1                         우리가 다가가는 쪽(-z) 해안. 25m 에서 "도착" 을 말한다
    ///   야자수   Palm_01~04 × 1.5, 6그루                  -z 쪽 해안을 따라, 종류를 섞어서
    /// </code>
    /// 전부 콜라이더 없음 · 정적. 재질은 프리팹 것 그대로 (둘 다 URP 정상).
    /// </summary>
    private static GameObject BuildIslandPrefab(StringBuilder log)
    {
        GameObject mountainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPath);
        GameObject volcanoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VolcanoPath);
        GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArchPath);

        if (mountainPrefab == null)
        {
            log.AppendLine($"    ⚠ 섬덩이를 못 찾음: {MountainPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(IslandPrefabPath);
        }

        GameObject root = new GameObject("P_DestinationIsland");

        try
        {
            // 섬덩이 — 중심, 2m 가라앉힘.
            Transform mountain = Put(mountainPrefab, root.transform, "Island_Body", Vector3.down * MountainSink, Quaternion.identity, MountainScale);
            Bounds body = BoundsOf(mountain);

            // 봉우리 — 섬덩이 가장 높은 곳. 정점을 읽을 수 있으면 진짜 꼭대기, 아니면 가운데.
            Vector3 top = HighestPoint(mountain, body);

            if (volcanoPrefab != null)
            {
                Transform volcano = Put(volcanoPrefab, root.transform, "Island_Peak", Vector3.zero, Quaternion.identity, VolcanoScale);
                Bounds peak = BoundsOf(volcano);
                volcano.position += new Vector3(top.x - peak.center.x, (top.y - VolcanoBury) - peak.min.y, top.z - peak.center.z);
            }
            else
            {
                log.AppendLine($"    ⚠ 화산을 못 찾음: {VolcanoPath}");
            }

            // 랜드마크 — 우리가 다가가는 쪽(-z) 해안. 밑을 살짝 물에 담근다.
            if (archPrefab != null)
            {
                Transform arch = Put(archPrefab, root.transform, "Island_Arch", Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 1f);
                Bounds a = BoundsOf(arch);
                Vector3 want = new Vector3(body.center.x, -1f, body.min.z + body.extents.z * 0.25f);
                arch.position += new Vector3(want.x - a.center.x, want.y - a.min.y, want.z - a.center.z);
            }
            else
            {
                log.AppendLine($"    ⚠ 바위 아치를 못 찾음: {ArchPath}");
            }

            // 야자수 — -z 쪽 해안을 따라 반원으로. 종류를 돌려 쓴다. 각도 · 자리는 상수라 매번 같다.
            int palms = 0;

            for (int i = 0; i < PalmCount; i++)
            {
                GameObject palmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalmPaths[i % PalmPaths.Length]);

                if (palmPrefab == null)
                {
                    continue;
                }

                // 200° ~ 340° (−z 반쪽), 해안선 안쪽 70% 타원 위.
                float angle = Mathf.Lerp(200f, 340f, (i + 0.5f) / PalmCount) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(
                    body.center.x + Mathf.Cos(angle) * body.extents.x * 0.7f,
                    0.4f,
                    body.center.z + Mathf.Sin(angle) * body.extents.z * 0.7f);

                Put(palmPrefab, root.transform, $"Island_Palm_{i}", at, Quaternion.Euler(0f, i * 57f, 0f), PalmScale);
                palms++;
            }

            // 콜라이더 없음 · 정적. 보여주기만 하는 것이다.
            foreach (Collider bump in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(bump);
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = true;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, IslandPrefabPath, out bool ok);

            if (!ok)
            {
                log.AppendLine($"    ⚠ 섬 프리팹 저장 실패: {IslandPrefabPath}");
                return null;
            }

            Bounds all = BoundsOf(root.transform);
            log.AppendLine($"    🏝 목적지 섬 프리팹 — 섬덩이 {body.size.x:F0}×{body.size.y:F0}×{body.size.z:F0}m, " +
                           $"화산 꼭대기 y {all.max.y:F0}m, 아치 · 야자수 {palms}그루 → {IslandPrefabPath}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform Put(GameObject prefab, Transform parent, string name, Vector3 at, Quaternion turn, float scale)
    {
        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        made.name = name;
        made.transform.SetParent(parent, false);
        made.transform.localPosition = at;
        made.transform.localRotation = turn;
        made.transform.localScale = Vector3.one * scale;
        return made.transform;
    }

    private static Bounds BoundsOf(Transform root)
    {
        Renderer[] draws = root.GetComponentsInChildren<Renderer>(true);

        if (draws.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        return box;
    }

    /// <summary>메시에서 가장 높은 정점(월드). 정점을 못 읽으면 경계 가운데 꼭대기.</summary>
    private static Vector3 HighestPoint(Transform root, Bounds fallback)
    {
        Vector3 best = new Vector3(fallback.center.x, fallback.max.y, fallback.center.z);
        bool any = false;

        foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (part.sharedMesh == null || !part.sharedMesh.isReadable)
            {
                continue;
            }

            Vector3[] points = part.sharedMesh.vertices;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 world = part.transform.TransformPoint(points[i]);

                if (!any || world.y > best.y)
                {
                    best = world;
                    any = true;
                }
            }
        }

        return best;
    }

    // ------------------------------------------------------------
    // ⛵ 우리 배 돛 — 미색
    // ------------------------------------------------------------

    /// <summary>
    /// 미색 돛 텍스처 · 재질을 만들고(<see cref="ShipCoopSailArt"/>), <c>P_PirateShip</c> 의 돛 · 깃발 렌더러 슬롯만 그 재질로 덮어쓴다.
    /// 로프(<c>StylShip_Ropes</c>) · 삭구는 원래 재질 그대로다. P_PirateShip 은 ShipCoop 전용 래퍼라 로비 배는 안 바뀐다.
    /// </summary>
    private static void WhitenOurSails(StringBuilder log)
    {
        Material white = ShipCoopSailArt.CreateOrOverwrite(log);

        if (white == null)
        {
            log.AppendLine("    ⚠ 미색 돛 재질을 못 만들어 우리 배 돛을 그대로 둡니다");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(OurShipPrefabPath);

        if (root == null)
        {
            log.AppendLine($"    ⚠ 우리 배 프리팹을 못 열었습니다: {OurShipPrefabPath}");
            return;
        }

        try
        {
            int swapped = 0;

            foreach (Renderer draw in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = draw.name.ToLowerInvariant();

                if (!n.Contains("sail") && !n.Contains("flag"))
                {
                    continue;
                }

                Material[] mats = draw.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    // 돛 아틀라스 슬롯만. (Rope 만 쓰는 메시는 이름에 sail/flag 가 없어 여기 안 온다)
                    if (mats[i] != null && mats[i].name.Contains("SailsRope") && mats[i] != white)
                    {
                        mats[i] = white;
                        changed = true;
                    }
                }

                if (changed)
                {
                    draw.sharedMaterials = mats;
                    swapped++;
                }
            }

            if (swapped > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, OurShipPrefabPath);
            }

            int already = 0;

            foreach (Renderer draw in root.GetComponentsInChildren<Renderer>(true))
            {
                if (draw.sharedMaterial == white) already++;
            }

            log.AppendLine($"    ⛵ 우리 배 돛 — 이번에 {swapped}개 슬롯을 미색 돛으로 바꿈, 미색 돛 렌더러 {already}개 → {OurShipPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------
    // 씬에 잇기
    // ------------------------------------------------------------

    private static void WireScene(GameObject enemyPrefab, GameObject islandPrefab, StringBuilder log)
    {
        // 목적지 섬
        VoyageSea sea = Object.FindAnyObjectByType<VoyageSea>(FindObjectsInactive.Include);

        if (sea == null)
        {
            log.AppendLine("    ⚠ VoyageSea 를 못 찾아 섬을 잇지 못했습니다");
        }
        else if (islandPrefab != null)
        {
            SerializedObject so = new SerializedObject(sea);
            SerializedProperty island = so.FindProperty("islandPrefab");

            if (island.objectReferenceValue != islandPrefab)
            {
                island.objectReferenceValue = islandPrefab;
                so.ApplyModifiedProperties();
                log.AppendLine("    VoyageSea.islandPrefab ← P_DestinationIsland (회색 큐브 대신)");
            }
            else
            {
                log.AppendLine("    VoyageSea.islandPrefab 은 이미 P_DestinationIsland");
            }
        }

        // 적선
        EnemyShip enemy = Object.FindAnyObjectByType<EnemyShip>(FindObjectsInactive.Include);

        if (enemy == null)
        {
            log.AppendLine("    ⚠ 적선 사건(EnemyShip)을 못 찾아 적선 모양을 잇지 못했습니다");
            return;
        }

        EnemyShipVisual view = enemy.GetComponent<EnemyShipVisual>();

        if (view == null)
        {
            view = Undo.AddComponent<EnemyShipVisual>(enemy.gameObject);
        }

        SerializedObject data = new SerializedObject(view);
        data.FindProperty("enemy").objectReferenceValue = enemy;
        data.FindProperty("shipPrefab").objectReferenceValue = enemyPrefab;
        data.FindProperty("smokePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath);
        data.ApplyModifiedProperties();

        log.AppendLine($"    EnemyShipVisual ← P_EnemyShip · FX_Smoke_01 (자리 우현 x {EnemyShipVisual.FarX:F0} → {EnemyShipVisual.NearX:F0}, z {EnemyShipVisual.LaneZ:F0})");
    }
}

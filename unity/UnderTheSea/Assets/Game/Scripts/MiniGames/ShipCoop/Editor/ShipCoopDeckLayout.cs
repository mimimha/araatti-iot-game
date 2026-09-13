using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 배 모델을 얹고, 그 위에 **걸어다닐 바닥과 작업 자리를 놓는다.** (SHIPCOOP.md 4장)
///
/// 왜 스크립트로 만드는가
///   자리 배치가 곧 밸런스입니다. 4장 원칙 3의 "일 하나에 10~15초" 는
///   갑판 크기와 자리 간격에서 나오는 숫자라, 밸런싱하는 동안 배치를 계속 바꿔보게 됩니다.
///   여기 숫자만 고치고 다시 돌리면 전부 다시 놓입니다.
///
/// 치수는 **배 모델에 레이를 쏴서 잰 실제 바닥 높이**입니다.
///
/// <code>
/// 뒷갑판 (DeckAftUpper)  윗면 y  0.98   z -17.5 ~ -5.5   🛞 조타
/// 중간갑판 (DeckMid)     윗면 y -3.49   z  -5.5 ~  9.8   ⛵ 돛 · 📦 상자
/// 앞갑판 (DeckFore)      윗면 y -1.82   z   9.8 ~ 17.5   💣 대포
/// 배 중심 x = 1.05       좌우가 비대칭이라 0 이 아니다
/// </code>
///
/// ⚠ **바운딩 박스로 재면 안 됩니다.**
///
///    처음에는 갑판 메시의 bounds.max.y 를 썼는데, 난간과 테두리까지 포함되어
///    뒷·앞갑판이 실제 바닥보다 **39cm 높게** 나왔습니다. 그 위를 걸으니
///    캐릭터가 갑판에 떠 있었습니다. 지금 값은 위에서 아래로 레이를 쏴서
///    **발이 실제로 닿는 면**을 잰 것입니다. 모델을 바꾸면 같은 방법으로 다시 재세요.
///
/// ⚠ **배 모델의 콜라이더는 전부 끕니다.**
///
///    `MeshCollider` 가 47개 붙어 있는데, 계단이 울퉁불퉁해서 CharacterController 가
///    걸립니다. 대신 **보이지 않는 평평한 큐브**를 바닥으로 깔고 그 위를 걷게 합니다.
///    배는 보이기만 합니다. 그래야 지금 잘 도는 이동이 안 깨집니다.
///
/// ⚠ 기존 오브젝트를 **옮기기만** 합니다. 지우고 다시 만들지 않습니다.
///    ShipCoopGame · EventScheduler 가 물고 있는 연결이 끊어지면 안 되기 때문입니다.
///
/// 쓰는 법: Tools / ShipCoop / 배 모델과 갑판 배치
/// </summary>
public static class ShipCoopDeckLayout
{
    // ------------------------------------------------------------
    // 배에서 잰 값 — 모델을 바꾸면 ShipCoopDeckMeasure 로 다시 재서 고친다
    // ------------------------------------------------------------

    private const string ShipPrefabPath = "Assets/Game/Prefabs/PirateShip/P_PirateShip.prefab";
    private const string ShipName = "PirateShip";

    /// <summary>배 중심의 x. 모델이 좌우 대칭이 아니다.</summary>
    private const float CenterX = 1.05f;
    private const float DeckWidth = 10.3f;
    private const float DeckThickness = 0.3f;

    private const float AftSurfaceY = 0.98f;
    private const float AftBackZ = -17.5f;
    private const float AftFrontZ = -5.5f;

    private const float MidSurfaceY = -3.49f;
    private const float MidBackZ = -5.5f;
    private const float MidFrontZ = 9.8f;

    private const float ForeSurfaceY = -1.82f;
    private const float ForeBackZ = 9.8f;
    private const float ForeFrontZ = 17.5f;

    // ------------------------------------------------------------
    // 계단 — **배에 보이는 계단 밑에 정확히 깐다**
    //
    // ⚠ 처음에는 배 한가운데에 경사로 하나를 놓았습니다. 그런데 이 배의 계단은
    //    **좌현·우현에 하나씩** 있고 가운데는 비어 있습니다.
    //
    // <code>
    //    z -2.5   . S S S S . . . R R R R R R . . S S S S .
    //               ←보이는계단→   ←내경사로→   ←보이는계단→
    // </code>
    //
    //    보이는 데로 가면 못 올라가고, 올라가지는 데는 안 보입니다. 게다가
    //    가운데는 돛대가 막고 있었습니다. 그래서 **계단 밑에 하나씩 4개**를 깝니다.
    //
    // 기울기도 계단에 맞춥니다. 완만하게 하려고 길게 빼면 계단보다 앞에서부터
    // 올라가기 시작해서 **허공을 걷습니다.** 보이는 것과 밟는 것이 같아야 합니다.
    // ------------------------------------------------------------

    /// <summary>배 중심에서 좌우로 이만큼 떨어진 자리에 계단이 있다.</summary>
    private const float StairOffsetX = 3.84f;

    /// <summary>보이는 계단은 2.0m 폭. 가장자리에서 헛디디지 않게 조금 좁게 깐다.</summary>
    private const float StairWidth = 1.9f;

    private const float RampThickness = 0.4f;

    // 경사로가 아래층 쪽으로 뻗는 길이. 배의 계단이 실제로 차지하는 길이다.
    //   뒤  z -5.5 ~ -1.5  오름 4.47m → 48.2°
    //   앞  z  9.8 ~  7.6  오름 1.67m → 37.2°
    //
    // ⚠ 48.2° 는 가파릅니다. DebugPlayerMover 의 slopeLimit 이 이보다 커야 합니다.
    //    (50 이면 여유가 1.8° 뿐이라 55 로 올려 두었습니다.)
    private const float AftStairRun = 4.0f;
    private const float ForeStairRun = 2.2f;

    // 갑판 면에서 얼마나 띄울지. 오브젝트마다 크기가 달라 0 이면 바닥에 묻힌다.
    private const float HelmLift = 0.70f;
    private const float SailLift = 1.60f;
    private const float CannonLift = 0.70f;
    private const float DumpLift = 0.45f;
    private const float BoxLift = 0.45f;
    private const float DamageLift = 0.40f;

    private const string RootName = "ShipDecks";

    // ------------------------------------------------------------

    private static float AftCenterZ => (AftBackZ + AftFrontZ) * 0.5f;
    private static float MidCenterZ => (MidBackZ + MidFrontZ) * 0.5f;
    private static float ForeCenterZ => (ForeBackZ + ForeFrontZ) * 0.5f;

    private static float AftLength => AftFrontZ - AftBackZ;
    private static float MidLength => MidFrontZ - MidBackZ;
    private static float ForeLength => ForeFrontZ - ForeBackZ;

    [MenuItem("Tools/ShipCoop/배 모델과 갑판 배치")]
    public static void Build()
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("[배 모델과 갑판 배치]");

        PlaceShip(log);

        Transform root = FindOrCreateRoot();

        BuildDecks(root, log);
        BuildStairs(root, log);
        MoveStations(log);
        MovePlayers(log);

        EditorSceneManager.MarkAllScenesDirty();

        log.AppendLine();
        log.AppendLine("씬을 저장해야 남습니다. (Ctrl+S)");

        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------
    // 배 모델
    // ------------------------------------------------------------

    /// <summary>
    /// 배를 씬에 놓고 **콜라이더를 전부 끈다.**
    ///
    /// 배에는 `MeshCollider` 가 47개 붙어 있습니다. 그대로 두면 계단과 난간이
    /// 울퉁불퉁해서 걸어다닐 수가 없습니다. 걸어다니는 바닥은 아래에서 만드는
    /// **평평한 큐브**가 맡고, 배는 보이기만 합니다.
    /// </summary>
    private static void PlaceShip(StringBuilder log)
    {
        GameObject ship = GameObject.Find(ShipName);

        if (ship == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);

            if (prefab == null)
            {
                log.AppendLine($"  ⚠ 배 프리팹을 못 찾음: {ShipPrefabPath}");
                return;
            }

            ship = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ship.name = ShipName;
            Undo.RegisterCreatedObjectUndo(ship, "배 모델과 갑판 배치");
        }

        Undo.RecordObject(ship.transform, "배 모델과 갑판 배치");
        ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ship.transform.localScale = Vector3.one;

        int off = 0;

        foreach (Collider hit in ship.GetComponentsInChildren<Collider>(true))
        {
            if (!hit.enabled)
            {
                continue;
            }

            Undo.RecordObject(hit, "배 모델과 갑판 배치");
            hit.enabled = false;
            off++;
        }

        log.AppendLine($"  배 모델을 놓고 콜라이더 {off}개를 껐습니다. (평평한 큐브가 바닥을 맡습니다)");
    }

    private static Transform FindOrCreateRoot()
    {
        GameObject found = GameObject.Find(RootName);

        if (found == null)
        {
            found = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(found, "배 모델과 갑판 배치");
        }

        found.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return found.transform;
    }

    // ------------------------------------------------------------
    // 걸어다니는 바닥 — 보이지 않는 평평한 큐브
    // ------------------------------------------------------------

    private static void BuildDecks(Transform root, StringBuilder log)
    {
        // 층마다 카메라 구도가 다르다. (거리, 높이, 보는 높이, 보는 앞쪽)
        //
        // **중간갑판이 특별합니다.** 뒤에 선미루가 4.9m 높이로 서 있어서,
        // 뒷갑판과 같은 높이(7)로 보면 카메라가 그 구조물 **안으로** 들어갑니다.
        // 그러면 조타륜만 코앞에 보이고 갑판이 하나도 안 보입니다.
        // 선미루 꼭대기(y 3.9)보다 위에서 내려다보도록 높이를 올립니다.

        MakeDeck(root, "Deck_Aft", AftCenterZ, AftLength, AftSurfaceY, "뒷갑판",
                 new Vector4(13f, 7f, 1.5f, 6f), hideBehindZ: float.NegativeInfinity, alsoHide: null);

        // 중간갑판에 서면 **뒷갑판이 통째로 눈앞을 가로막습니다.**
        // 카메라를 높여도 그 사이에 있으니 소용이 없어서, 그 층에 있는 동안 감춥니다.
        //
        // 이름을 하나씩 적지 않고 **선미 쪽(z < 중간갑판 뒤끝)에 있는 것 전부**를 잡습니다.
        // 하나만 빠져도 그게 화면에 덩그러니 떠 있어서, 목록으로 관리하면 반드시 빠뜨립니다.
        //
        // ⚠ **계단(StairsUpper)은 감추지 않습니다.**
        //    걸어다니는 경사로가 보이지 않기 때문에, 이 모델이 **어디로 올라가는지
        //    알려주는 유일한 단서**입니다. 감췄더니 올라갈 길이 화면에서 사라졌습니다.
        //    계단은 z -6.2 ~ -0.7 이라 위치로는 안 잡히므로 그냥 두면 됩니다.
        MakeDeck(root, "Deck_Main", MidCenterZ, MidLength, MidSurfaceY, "중간갑판",
                 new Vector4(15f, 8.5f, 1.5f, 6f),
                 hideBehindZ: MidBackZ,
                 alsoHide: null);

        MakeDeck(root, "Deck_Fore", ForeCenterZ, ForeLength, ForeSurfaceY, "앞갑판",
                 new Vector4(13f, 8f, 1.5f, 6f), hideBehindZ: float.NegativeInfinity, alsoHide: null);

        log.AppendLine($"  뒷갑판   y {AftSurfaceY,6:F2}  z {AftBackZ,6:F1} ~ {AftFrontZ,5:F1}   🛞 조타");
        log.AppendLine($"  중간갑판 y {MidSurfaceY,6:F2}  z {MidBackZ,6:F1} ~ {MidFrontZ,5:F1}   ⛵ 돛 · 📦 상자");
        log.AppendLine($"  앞갑판   y {ForeSurfaceY,6:F2}  z {ForeBackZ,6:F1} ~ {ForeFrontZ,5:F1}   💣 대포 · 🪣 뱃전");
    }

    private static void MakeDeck(Transform root, string name, float centerZ, float length,
                                 float surfaceY, string label, Vector4 view,
                                 float hideBehindZ, string[] alsoHide)
    {
        Transform deck = FindOrCreateBox(root, name);

        deck.localScale = new Vector3(DeckWidth, DeckThickness, length);
        deck.localPosition = new Vector3(CenterX, surfaceY - DeckThickness * 0.5f, centerZ);

        HideButKeepCollider(deck);

        ShipDeck mark = deck.GetComponent<ShipDeck>();

        if (mark == null)
        {
            mark = Undo.AddComponent<ShipDeck>(deck.gameObject);
        }

        SerializedObject so = new SerializedObject(mark);
        so.FindProperty("deckName").stringValue = label;

        so.FindProperty("overrideCamera").boolValue = true;
        so.FindProperty("cameraDistance").floatValue = view.x;
        so.FindProperty("cameraHeight").floatValue = view.y;
        so.FindProperty("cameraLookHeight").floatValue = view.z;
        so.FindProperty("cameraLookAhead").floatValue = view.w;

        FillBlockers(so, hideBehindZ, alsoHide);

        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// 이 층에 있을 때 감출 배 부분들을 찾아 넣는다.
    ///
    /// **이름 목록이 아니라 위치로 잡습니다.** `hideBehindZ` 보다 뒤에 있는 것은 전부입니다.
    /// 이름으로 관리하면 하나씩 빠뜨리고, 빠진 것은 화면에 덩그러니 떠 있게 됩니다.
    /// 배 모델을 바꿔도 이 방식이면 그대로 동작합니다.
    ///
    /// <paramref name="alsoHide"/> 는 경계에 걸쳐 있어 위치로는 안 잡히는 것들입니다.
    /// (계단이 그렇습니다 — 아래층 위에 놓여 있지만 위층에 속한 물건입니다)
    /// </summary>
    private static void FillBlockers(SerializedObject so, float hideBehindZ, string[] alsoHide)
    {
        SerializedProperty list = so.FindProperty("hideWhenHere");
        list.arraySize = 0;

        if (float.IsNegativeInfinity(hideBehindZ) && (alsoHide == null || alsoHide.Length == 0))
        {
            return;
        }

        GameObject ship = GameObject.Find(ShipName);

        if (ship == null)
        {
            return;
        }

        var found = new System.Collections.Generic.List<Renderer>();

        foreach (Renderer r in ship.GetComponentsInChildren<Renderer>(true))
        {
            bool behind = r.bounds.center.z < hideBehindZ;
            bool named = false;

            if (alsoHide != null)
            {
                foreach (string want in alsoHide)
                {
                    if (r.name == want)
                    {
                        named = true;
                        break;
                    }
                }
            }

            if (behind || named)
            {
                found.Add(r);
            }
        }

        list.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
    }

    /// <summary>
    /// 안 보이게 하되 **콜라이더는 살려둔다.**
    ///
    /// 배 모델이 보이는 몫을 하고, 이 큐브는 걸어다닐 바닥만 맡습니다.
    /// 지우면 안 되고 끄면 안 됩니다. 렌더러만 끕니다.
    /// </summary>
    private static void HideButKeepCollider(Transform t)
    {
        Renderer draw = t.GetComponent<Renderer>();

        if (draw != null && draw.enabled)
        {
            Undo.RecordObject(draw, "배 모델과 갑판 배치");
            draw.enabled = false;
        }
    }

    // ------------------------------------------------------------
    // 계단
    // ------------------------------------------------------------

    private static readonly string[] StairNames =
    {
        "Stairs_ToAft_Port", "Stairs_ToAft_Starboard",
        "Stairs_ToFore_Port", "Stairs_ToFore_Starboard",
    };

    private static void BuildStairs(Transform root, StringBuilder log)
    {
        // 예전에 쓰던 경사로를 지운다. 이름이 바뀌어서 그냥 두면 **보이지 않는
        // 옛 경사로가 갑판 한가운데에 그대로 남아** 걸리적거린다.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);

            if (!child.name.StartsWith("Stairs_") || System.Array.IndexOf(StairNames, child.name) >= 0)
            {
                continue;
            }

            log.AppendLine($"  옛 경사로 {child.name} 를 지웠습니다.");
            Undo.DestroyObjectImmediate(child.gameObject);
        }

        // 좌현 · 우현에 하나씩. 배에 보이는 계단이 두 짝이라서 그렇다.
        float port = CenterX - StairOffsetX;
        float starboard = CenterX + StairOffsetX;

        int aft = MakeRamp(root, "Stairs_ToAft_Port", port,
                           MidBackZ, MidSurfaceY, AftSurfaceY, AftStairRun, forward: false);

        MakeRamp(root, "Stairs_ToAft_Starboard", starboard,
                 MidBackZ, MidSurfaceY, AftSurfaceY, AftStairRun, forward: false);

        int fore = MakeRamp(root, "Stairs_ToFore_Port", port,
                            MidFrontZ, MidSurfaceY, ForeSurfaceY, ForeStairRun, forward: true);

        MakeRamp(root, "Stairs_ToFore_Starboard", starboard,
                 MidFrontZ, MidSurfaceY, ForeSurfaceY, ForeStairRun, forward: true);

        log.AppendLine();
        log.AppendLine($"  계단   좌현·우현 x {port:F2} / {starboard:F2}, 폭 {StairWidth:F1}m");
        log.AppendLine($"         뒤 {aft}° (오름 {AftSurfaceY - MidSurfaceY:F2}m / 길이 {AftStairRun:F1}m)");
        log.AppendLine($"         앞 {fore}° (오름 {ForeSurfaceY - MidSurfaceY:F2}m / 길이 {ForeStairRun:F1}m)");
    }

    /// <summary>
    /// 층을 잇는 **경사로.** 위 갑판 경계에서 아래 갑판 쪽으로 뻗는다.
    ///
    /// ⚠ **칸을 쌓지 않습니다.** 처음에는 계단처럼 상자를 쌓았는데 못 올라갔습니다.
    ///
    /// <code>
    /// 층 높이차 4.86m ÷ 칸당 0.25m = 20칸
    /// 계단 길이 5.3m  ÷ 20칸       = 칸 깊이 0.265m
    /// 그런데 캐릭터 반지름은      0.35m
    /// </code>
    ///
    /// **발 디딜 자리가 몸보다 좁습니다.** 한 칸에 올라서는 순간 이미 다음 칸에
    /// 몸이 박혀 있어서, CharacterController 가 밀어내기만 하고 못 올라갑니다.
    /// 칸을 깊게 하려면 계단이 갑판 절반을 차지하고, 칸을 줄이면 한 칸이 너무 높아집니다.
    ///
    /// 그래서 **매끈한 경사로**로 둡니다. 보이는 계단은 배 모델이 맡으므로
    /// 걸어다니는 바닥까지 계단 모양일 이유가 없습니다.
    ///
    /// 대신 **보이는 계단과 같은 자리, 같은 기울기**여야 합니다. 그래야 계단을
    /// 밟는 것처럼 보입니다. (자리는 StairOffsetX, 기울기는 AftStairRun 참고)
    /// </summary>
    private static int MakeRamp(Transform root, string name, float x, float edgeZ,
                                float fromY, float toY, float run, bool forward)
    {
        Transform group = FindOrCreateGroup(root, name);

        for (int i = group.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(group.GetChild(i).gameObject);
        }

        float rise = toY - fromY;
        int direction = forward ? -1 : 1;

        // 위쪽 끝은 갑판 경계, 아래쪽 끝은 거기서 run 만큼 아래층 쪽으로.
        float highZ = edgeZ;
        float lowZ = edgeZ + direction * run;

        float length = Mathf.Sqrt(run * run + rise * rise);
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

        Transform ramp = FindOrCreateBox(group, "Ramp");

        // 기울기. 방향에 따라 어느 끝이 올라가는지가 뒤집힌다.
        Quaternion rotation = Quaternion.Euler(direction * angle, 0f, 0f);
        ramp.localRotation = rotation;
        ramp.localScale = new Vector3(StairWidth, RampThickness, length);

        // 윗면이 두 갑판을 잇는 선에 정확히 놓이도록 두께의 절반만큼 아래로 민다.
        Vector3 middle = new Vector3(x, (fromY + toY) * 0.5f, (highZ + lowZ) * 0.5f);
        Vector3 up = rotation * Vector3.up;
        ramp.localPosition = middle - up * (RampThickness * 0.5f);

        HideButKeepCollider(ramp);

        return Mathf.RoundToInt(angle);
    }

    // ------------------------------------------------------------
    // 작업 자리 — 배치가 곧 밸런스다
    // ------------------------------------------------------------

    private static void MoveStations(StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("  자리:");

        // 🛞 조타 — 배에 있는 조타륜(Wheel) 자리. 선미루 위다.
        Place<HelmTask>(log, "🛞 조타", CenterX, AftSurfaceY + HelmLift, -8.0f);

        // ⛵ 돛 — 주 돛대. 중간갑판 한가운데.
        Place<SailTask>(log, "⛵ 돛", CenterX, MidSurfaceY + SailLift, MidCenterZ);

        // 💣 대포 — 앞갑판 우현.
        Place<CannonTask>(log, "💣 대포", CenterX + 2.8f, ForeSurfaceY + CannonLift, ForeCenterZ);

        PlaceDumps(log);
        PlaceBoxes(log);
        PlaceDamageSpawns(log);
    }

    /// <summary>
    /// 🪣 뱃전 — **층마다 하나씩.** 우현 난간 쪽.
    ///
    /// 하나만 두면 양동이 왕복이 18초가 됩니다. 4장은 **왕복 4초**를 전제로
    /// 침수 속도(초당 2.5%)를 정했으므로, 그렇게 두면 아무리 퍼내도 안 줄어듭니다.
    /// </summary>
    private static void PlaceDumps(StringBuilder log)
    {
        float rail = CenterX + 4.0f;

        Vector3[] spots =
        {
            new Vector3(rail, MidSurfaceY + DumpLift, MidCenterZ),
            new Vector3(rail, AftSurfaceY + DumpLift, AftCenterZ),
            new Vector3(rail, ForeSurfaceY + DumpLift, ForeCenterZ),
        };

        WaterDumpPoint[] found = Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0)
        {
            log.AppendLine($"    {"🪣 뱃전",-14}  (씬에 없음 — 건너뜀)");
            return;
        }

        int made = 0;

        while (found.Length + made < spots.Length)
        {
            GameObject copy = Object.Instantiate(found[0].gameObject, found[0].transform.parent);
            copy.name = $"WaterDump_{found.Length + made + 1}";
            Undo.RegisterCreatedObjectUndo(copy, "배 모델과 갑판 배치");
            made++;
        }

        found = Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < found.Length && i < spots.Length; i++)
        {
            Undo.RecordObject(found[i].transform, "배 모델과 갑판 배치");
            found[i].transform.position = spots[i];
        }

        string extra = made > 0 ? $"  ({made}개 새로 만듦)" : "";
        log.AppendLine($"    {"🪣 뱃전",-14}  층마다 1개씩 {Mathf.Min(found.Length, spots.Length)}개{extra}");
    }

    /// <summary>
    /// 📦 보급 상자 세 개. 전부 중간갑판에 둔다.
    ///
    /// **포탄 상자를 대포와 다른 층에 둡니다.** 나르려면 계단을 건너야 하고
    /// 그동안 양손이 묶입니다. 그래야 "포탄 좀 가져와!" 가 나옵니다. (4장)
    /// 다만 너무 멀면 아무도 대포를 안 씁니다. 계단 하나 거리로 잡았습니다.
    /// </summary>
    private static void PlaceBoxes(StringBuilder log)
    {
        AmmoBox[] boxes = Object.FindObjectsByType<AmmoBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < boxes.Length; i++)
        {
            Vector3 where;
            string label;

            switch (boxes[i].Kind)
            {
                case Cargo.Ammo:
                    where = new Vector3(CenterX - 3.0f, MidSurfaceY + BoxLift, MidFrontZ - 3.0f);
                    label = "📦 포탄 상자";
                    break;

                case Cargo.Plank:
                    where = new Vector3(CenterX + 3.0f, MidSurfaceY + BoxLift, MidCenterZ - 2.0f);
                    label = "🪵 자재 상자";
                    break;

                default:
                    where = new Vector3(CenterX - 3.0f, MidSurfaceY + BoxLift, MidBackZ + 3.0f);
                    label = "🪣 양동이 상자";
                    break;
            }

            Undo.RecordObject(boxes[i].transform, "배 모델과 갑판 배치");
            boxes[i].transform.position = where;

            log.AppendLine($"    {label,-14}  {Describe(where)}");
        }
    }

    /// <summary>
    /// 💥 파손이 생길 자리. **세 층에 흩어 둔다.**
    ///
    /// `RepairTask` 는 씬에 없습니다. `HullDamage` 가 사건이 터질 때 여기에 만들어냅니다.
    /// 한 층에 몰리면 수리하는 사람이 거기 박히고, 4장 원칙 1이 깨집니다.
    /// </summary>
    private static void PlaceDamageSpawns(StringBuilder log)
    {
        Vector3[] spots =
        {
            new Vector3(CenterX - 3.0f, AftSurfaceY + DamageLift, AftCenterZ),
            new Vector3(CenterX + 3.0f, MidSurfaceY + DamageLift, MidCenterZ + 3.0f),
            new Vector3(CenterX - 3.5f, MidSurfaceY + DamageLift, MidBackZ + 2.0f),
            new Vector3(CenterX + 2.5f, ForeSurfaceY + DamageLift, ForeCenterZ - 2.0f),
        };

        int moved = 0;

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject spawn = GameObject.Find($"DamageSpawn_{i + 1:00}");

            if (spawn == null)
            {
                continue;
            }

            Undo.RecordObject(spawn.transform, "배 모델과 갑판 배치");
            spawn.transform.position = spots[i];
            moved++;
        }

        log.AppendLine($"    {"💥 파손 자리",-14}  {moved}곳을 세 층에 흩음");
    }

    private static void Place<T>(StringBuilder log, string label, float x, float y, float z) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0)
        {
            log.AppendLine($"    {label,-14}  (씬에 없음 — 건너뜀)");
            return;
        }

        Vector3 position = new Vector3(x, y, z);

        Undo.RecordObject(found[0].transform, "배 모델과 갑판 배치");
        found[0].transform.position = position;

        string extra = found.Length > 1 ? $"  ⚠ {found.Length}개 중 첫 번째만" : "";
        log.AppendLine($"    {label,-14}  {Describe(position)}{extra}");
    }

    // ------------------------------------------------------------

    private static void MovePlayers(StringBuilder log)
    {
        TaskWorker[] players = Object.FindObjectsByType<TaskWorker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (players.Length == 0)
        {
            return;
        }

        // 중간갑판에서 출항한다. 어느 층으로든 한 번에 갈 수 있는 자리다.
        //
        // ⚠ **경사로를 피해서 세운다.** 경사로 위에 세우면 파묻히거나 튕겨나간다.
        //    앞뒤로는 경사로 끝에서 2m 앞, 좌우로는 두 계단 사이(가운데)에 몰아 둔다.
        float startZ = MidBackZ + AftStairRun + 2f;
        float startX = CenterX - 2.4f;

        int off = 0;

        for (int i = 0; i < players.Length; i++)
        {
            Undo.RecordObject(players[i].transform, "배 모델과 갑판 배치");
            players[i].transform.position =
                new Vector3(startX + i * 1.6f, MidSurfaceY + 0.1f, startZ);

            // ⚠ **꺼져 있는 사람을 켜지 않는다.**
            //
            // `KeyboardPlayerController` 는 플레이어마다 키가 다르지 않습니다.
            // 모두 같은 키보드를 읽으므로, 둘 이상 켜 두면 **방향키 하나에
            // 전부 같이 걸어갑니다.** 혼자 테스트할 때는 한 명만 켜 두는 것이
            // 정상이고, 그것을 여기서 마음대로 되돌리면 안 됩니다.
            //
            // (사람이 안 보이는 진짜 원인은 대부분 자리입니다 — 경사로에 파묻히거나
            //  갑판 아래로 떨어진 것. 꺼짐은 대개 일부러 꺼 둔 것입니다.)
            if (!players[i].gameObject.activeSelf)
            {
                off++;
            }
        }

        log.AppendLine();
        log.AppendLine($"  사람 {players.Length}명을 중간갑판 z {startZ:F1} 에 세움 (경사로 z {MidBackZ:F1}~{MidBackZ + AftStairRun:F1} 을 피함)");

        if (off > 0)
        {
            log.AppendLine($"  그중 {off}명은 꺼져 있습니다. 그대로 둡니다. (혼자 테스트하려고 꺼 둔 것)");
        }
    }

    private static string Describe(Vector3 p) => $"x {p.x,5:F1}  y {p.y,6:F2}  z {p.z,6:F1}";

    private static Transform FindOrCreateGroup(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found == null)
        {
            GameObject made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
            made.transform.SetParent(parent, false);
            found = made.transform;
        }

        found.localPosition = Vector3.zero;
        return found;
    }

    private static Transform FindOrCreateBox(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null)
        {
            return found;
        }

        GameObject made = GameObject.CreatePrimitive(PrimitiveType.Cube);
        made.name = name;
        Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
        made.transform.SetParent(parent, false);
        return made.transform;
    }
}

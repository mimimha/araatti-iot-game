using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 갑판을 3층으로 짓고 작업 자리를 배치한다. (SHIPCOOP.md 4장 · 6장)
///
/// 왜 스크립트로 만드는가
///   자리 배치가 곧 밸런스입니다. 4장 원칙 3의 "일 하나에 10~15초" 는
///   **갑판 크기와 자리 간격에서 나오는 숫자**라서, 밸런싱하는 동안 배치를
///   계속 바꿔보게 됩니다. 손으로 끌어다 놓으면 바꿀 때마다 처음부터고,
///   무엇을 바꿨는지도 안 남습니다.
///
///   여기 숫자만 고치고 다시 돌리면 **전부 다시 놓입니다.**
///
/// 치수는 실제 배 에셋에서 잰 값입니다. (ShipCoopDeckMeasure)
///   폭 10.2m · 앞 13.7m · 중간 15.4m · 뒤 13.8m · 합쳐서 43m
///   이 치수로 회색 큐브를 놓았으므로, 나중에 모델로 바꿔도 자리가 안 움직입니다.
///
/// ⚠ 기존 오브젝트를 **옮기기만** 합니다. 지우고 다시 만들지 않습니다.
///    ShipCoopGame · EventScheduler 가 물고 있는 연결이 끊어지면 안 되기 때문입니다.
///
/// 쓰는 법: Tools / ShipCoop / 갑판 3층으로 배치
/// </summary>
public static class ShipCoopDeckLayout
{
    // ------------------------------------------------------------
    // 치수 — 밸런싱할 때 여기를 고친다
    // ------------------------------------------------------------

    private const float DeckWidth = 10.2f;      // 갑판 폭 (에셋 실측)
    private const float DeckThickness = 0.3f;

    // +z 가 뱃머리 방향. 조타는 선미(−z)에 둔다. 레퍼런스 구도와 같다.
    private const float AftLength = 13.8f;      // 뒷갑판 (선미루) — 조타
    private const float MainLength = 15.4f;     // 중간갑판 (주갑판) — 돛 · 보급
    private const float ForeLength = 13.7f;     // 앞갑판 (선수루) — 대포 · 뱃전

    // 층 높이. 실제 배는 선미가 제일 높고 주갑판이 제일 낮다.
    // 실측 높이차(4.2m)는 계단이 너무 길어져서 걸어 다닐 만큼으로 줄였다.
    private const float AftHeight = 2.0f;
    private const float MainHeight = 0f;
    private const float ForeHeight = 1.2f;

    // 계단. **좁게 둔다.** 여기가 병목이 되어야 서로 비켜주게 된다. (4장)
    private const float StairWidth = 3f;
    private const float StepRise = 0.25f;       // 한 칸 높이. DebugPlayerMover 의 stepHeight(0.4) 보다 낮아야 올라간다
    private const float StepRun = 0.35f;        // 한 칸 깊이

    // 갑판 면에서 얼마나 띄울지. 오브젝트마다 크기가 달라서 0 으로 두면 바닥에 묻힌다.
    // 지금 씬에 놓여 있던 높이를 그대로 쓴다. (평면 갑판이 y=0 이었으므로 그 값이 곧 띄움값)
    private const float HelmLift = 0.70f;
    private const float SailLift = 1.60f;
    private const float CannonLift = 0.70f;
    private const float DumpLift = 0.45f;
    private const float BoxLift = 0.45f;
    private const float DamageLift = 0.40f;

    private const string RootName = "ShipDecks";

    // ------------------------------------------------------------

    /// <summary>중간갑판의 앞뒤 경계 (z)</summary>
    private static float MainBackZ => -MainLength * 0.5f;
    private static float MainFrontZ => MainLength * 0.5f;

    /// <summary>뒷갑판 · 앞갑판의 한가운데 (z)</summary>
    private static float AftCenterZ => MainBackZ - AftLength * 0.5f;
    private static float ForeCenterZ => MainFrontZ + ForeLength * 0.5f;

    [MenuItem("Tools/ShipCoop/갑판 3층으로 배치")]
    public static void Build()
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("[갑판 3층 배치]");

        Transform root = FindOrCreateRoot();

        BuildDecks(root, log);
        BuildStairs(root, log);
        MoveStations(log);
        MovePlayers(log);
        RetireOldDeck(log);

        EditorSceneManager.MarkAllScenesDirty();

        log.AppendLine();
        log.AppendLine($"전체 길이 {AftLength + MainLength + ForeLength:F1}m · 폭 {DeckWidth:F1}m");
        log.AppendLine("씬을 저장해야 남습니다. (Ctrl+S)");

        Debug.Log(log.ToString());
    }

    private static Transform FindOrCreateRoot()
    {
        GameObject found = GameObject.Find(RootName);

        if (found == null)
        {
            found = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(found, "갑판 3층 배치");
        }

        found.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return found.transform;
    }

    // ------------------------------------------------------------
    // 갑판 세 장
    // ------------------------------------------------------------

    private static void BuildDecks(Transform root, StringBuilder log)
    {
        // 이름이 그대로 알림에 나온다. "뒷갑판 침수!" (9장)
        MakeDeck(root, "Deck_Aft", AftCenterZ, AftLength, AftHeight, "뒷갑판");
        MakeDeck(root, "Deck_Main", 0f, MainLength, MainHeight, "중간갑판");
        MakeDeck(root, "Deck_Fore", ForeCenterZ, ForeLength, ForeHeight, "앞갑판");

        log.AppendLine($"  뒷갑판   z {AftCenterZ,6:F1}  길이 {AftLength,5:F1}  바닥 y {AftHeight,4:F1}   🛞 조타");
        log.AppendLine($"  중간갑판 z {0f,6:F1}  길이 {MainLength,5:F1}  바닥 y {MainHeight,4:F1}   ⛵ 돛 · 📦 보급");
        log.AppendLine($"  앞갑판   z {ForeCenterZ,6:F1}  길이 {ForeLength,5:F1}  바닥 y {ForeHeight,4:F1}   💣 대포 · 🪣 뱃전");
    }

    /// <summary>갑판 한 장. surfaceY 가 걸어다니는 면의 높이다.</summary>
    private static void MakeDeck(Transform root, string name, float centerZ, float length, float surfaceY, string label)
    {
        Transform deck = FindOrCreateBox(root, name);

        deck.localScale = new Vector3(DeckWidth, DeckThickness, length);
        deck.localPosition = new Vector3(0f, surfaceY - DeckThickness * 0.5f, centerZ);

        // 카메라가 "지금 이 층" 을 알아보고, 알림이 층 이름을 쓸 수 있게 표시해 둔다.
        ShipDeck mark = deck.GetComponent<ShipDeck>();

        if (mark == null)
        {
            mark = Undo.AddComponent<ShipDeck>(deck.gameObject);
        }

        SerializedObject so = new SerializedObject(mark);
        so.FindProperty("deckName").stringValue = label;
        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------
    // 계단 — 좁게. 여기가 병목이다
    // ------------------------------------------------------------

    private static void BuildStairs(Transform root, StringBuilder log)
    {
        // 중간갑판에서 뒤로(선미로) 올라간다. 계단은 중간갑판 위에 놓인다.
        int aftSteps = MakeStairs(root, "Stairs_ToAft", MainBackZ, MainHeight, AftHeight, forward: false);

        // 중간갑판에서 앞으로(선수로) 올라간다.
        int foreSteps = MakeStairs(root, "Stairs_ToFore", MainFrontZ, MainHeight, ForeHeight, forward: true);

        log.AppendLine();
        log.AppendLine($"  계단   뒤쪽 {aftSteps}칸 · 앞쪽 {foreSteps}칸 · 폭 {StairWidth:F1}m (좁게 = 병목)");
    }

    /// <summary>
    /// 계단 한 벌. 아래 갑판 쪽에서 위 갑판 쪽으로 칸을 쌓는다.
    ///
    /// 칸 높이를 DebugPlayerMover 의 stepHeight 보다 낮게 둬야 걸어 올라갑니다.
    /// 높으면 벽이 되어서 위층에 영영 못 갑니다.
    /// </summary>
    private static int MakeStairs(Transform root, string name, float edgeZ, float fromY, float toY, bool forward)
    {
        Transform group = FindOrCreateGroup(root, name);

        float rise = toY - fromY;
        int steps = Mathf.Max(1, Mathf.CeilToInt(rise / StepRise));
        float actualRise = rise / steps;

        // 위 갑판 경계에서 아래 갑판 쪽으로 물러나며 쌓는다.
        int direction = forward ? -1 : 1;

        // 기존 칸을 지운다. 층 높이를 바꾸면 칸 수가 달라지기 때문이다.
        for (int i = group.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(group.GetChild(i).gameObject);
        }

        for (int i = 0; i < steps; i++)
        {
            Transform step = FindOrCreateBox(group, $"Step_{i + 1:00}");

            // ⚠ **갑판 경계에 제일 높은 칸이 와야 합니다.**
            //
            // i 가 커질수록 갑판에서 멀어지므로(아래 z), 높이는 반대로 낮아져야 합니다.
            // 거꾸로 쌓으면 갑판 경계에 2m 벽이 서고, 계단은 그 벽에서 멀어지며
            // 내려가는 모양이 됩니다. 그러면 위층에 영영 못 올라갑니다.
            float topY = fromY + actualRise * (steps - i);
            float z = edgeZ + direction * (StepRun * (i + 0.5f));

            // 칸마다 바닥까지 채운다. 옆에서 보면 계단 모양이 된다.
            step.localScale = new Vector3(StairWidth, topY - fromY, StepRun);
            step.localPosition = new Vector3(0f, fromY + (topY - fromY) * 0.5f, z);
        }

        return steps;
    }

    // ------------------------------------------------------------
    // 작업 자리 — 배치가 곧 밸런스다
    // ------------------------------------------------------------

    private static void MoveStations(StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("  자리:");

        // 🛞 조타 — 선미 한가운데. 레퍼런스 구도와 같다.
        Place<HelmTask>(log, "🛞 조타", 0f, AftHeight + HelmLift, AftCenterZ - AftLength * 0.25f);

        // ⛵ 돛 — 주 돛대. 중간갑판 한가운데.
        Place<SailTask>(log, "⛵ 돛", 0f, MainHeight + SailLift, 1f);

        // 💣 대포 — 앞갑판 우현.
        Place<CannonTask>(log, "💣 대포", 3.5f, ForeHeight + CannonLift, ForeCenterZ);

        PlaceDumps(log);
        PlaceBoxes(log);
        PlaceDamageSpawns(log);
    }

    /// <summary>
    /// 🪣 뱃전 — **층마다 하나씩.**
    ///
    /// 처음에는 배 반대편에 하나만 두었습니다. "가까우면 운반이 아니라 제자리걸음"
    /// 이라고 봤기 때문입니다. 그런데 갑판이 43m 가 되면서 그 전제가 깨졌습니다.
    ///
    /// <code>
    /// 뱃전 1개  →  양동이 상자에서 24m  →  왕복 18초
    /// </code>
    ///
    /// 4장은 **양동이 왕복 4초**를 전제로 침수 속도(초당 2.5%)를 정했습니다.
    /// 18초가 되면 구멍 하나가 왕복 한 번에 45% 를 붓습니다. **아무리 퍼내도 안 줄어듭니다.**
    ///
    /// 실제로도 물은 제일 가까운 난간에 버리지 배 반대편까지 들고 가지 않습니다.
    /// 운반의 대가는 이제 뱃전까지의 거리가 아니라 **배가 넓다는 것 자체**가 만듭니다.
    ///
    /// `CarryTask` 는 이미 가장 가까운 뱃전을 찾으므로 개수만 늘리면 됩니다.
    /// </summary>
    private static void PlaceDumps(StringBuilder log)
    {
        Vector3[] spots =
        {
            new Vector3(4.5f, MainHeight + DumpLift, 0f),              // 중간갑판 우현
            new Vector3(4.5f, AftHeight + DumpLift, AftCenterZ),       // 뒷갑판 우현
            new Vector3(4.5f, ForeHeight + DumpLift, ForeCenterZ),     // 앞갑판 우현
        };

        WaterDumpPoint[] found = Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0)
        {
            log.AppendLine($"    {"🪣 뱃전",-14}  (씬에 없음 — 건너뜀)");
            return;
        }

        // 모자라면 있는 것을 복제해서 채운다. 손으로 만들면 연결을 빠뜨리기 쉽다.
        int made = 0;

        while (found.Length + made < spots.Length)
        {
            GameObject copy = Object.Instantiate(found[0].gameObject, found[0].transform.parent);
            copy.name = $"WaterDump_{found.Length + made + 1}";
            Undo.RegisterCreatedObjectUndo(copy, "갑판 3층 배치");
            made++;
        }

        // 복제한 것까지 다시 모은다.
        found = Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < found.Length && i < spots.Length; i++)
        {
            Undo.RecordObject(found[i].transform, "갑판 3층 배치");
            found[i].transform.position = spots[i];
        }

        string extra = made > 0 ? $"  ({made}개 새로 만듦)" : "";
        log.AppendLine($"    {"🪣 뱃전",-14}  층마다 1개씩 {Mathf.Min(found.Length, spots.Length)}개{extra}");
    }

    /// <summary>
    /// 📦 보급 상자 세 개. 종류마다 쓰는 곳이 달라서 따로 놓는다.
    ///
    /// **포탄 상자를 대포와 다른 층에 둡니다.** 포탄을 나르려면 계단을 한 번 건너야 하고,
    /// 그동안 양손이 묶입니다. 그래야 "포탄 좀 가져와!" 가 나옵니다. (4장)
    ///
    /// 다만 너무 멀면 아무도 대포를 안 씁니다. 계단 하나 + 10m 정도로 잡았습니다.
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
                    // 중간갑판 앞쪽. 계단 하나 올라가면 대포다.
                    where = new Vector3(-3.5f, MainHeight + BoxLift, 4f);
                    label = "📦 포탄 상자";
                    break;

                case Cargo.Plank:
                    // 중간갑판 우현. 파손은 세 층에 다 생기므로 가운데 둔다.
                    where = new Vector3(4f, MainHeight + BoxLift, -2f);
                    label = "🪵 자재 상자";
                    break;

                default:
                    // 양동이. 중간갑판 좌현 뒤.
                    where = new Vector3(-4f, MainHeight + BoxLift, -4f);
                    label = "🪣 양동이 상자";
                    break;
            }

            Undo.RecordObject(boxes[i].transform, "갑판 3층 배치");
            boxes[i].transform.position = where;

            log.AppendLine($"    {label,-14}  {Describe(where)}");
        }
    }

    /// <summary>
    /// 💥 파손이 생길 자리.
    ///
    /// `RepairTask` 는 씬에 없습니다. `HullDamage` 가 사건이 터질 때 여기에 만들어냅니다.
    /// 그래서 옮겨야 하는 것은 수리 자리가 아니라 **이 표식들**입니다.
    ///
    /// **세 층에 흩어 둡니다.** 한 층에 몰리면 수리하는 사람이 거기 박히고,
    /// 그러면 4장 원칙 1("박히는 자리 0개")이 깨집니다.
    /// </summary>
    private static void PlaceDamageSpawns(StringBuilder log)
    {
        Vector3[] spots =
        {
            new Vector3(-3.5f, AftHeight + DamageLift, AftCenterZ),               // 뒷갑판
            new Vector3(3.5f, MainHeight + DamageLift, -3f),                      // 중간갑판 우현
            new Vector3(-4f, MainHeight + DamageLift, 5f),                        // 중간갑판 좌현
            new Vector3(3f, ForeHeight + DamageLift, ForeCenterZ - 2f),           // 앞갑판
        };

        int moved = 0;

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject spawn = GameObject.Find($"DamageSpawn_{i + 1:00}");

            if (spawn == null)
            {
                continue;
            }

            Undo.RecordObject(spawn.transform, "갑판 3층 배치");
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

        Undo.RecordObject(found[0].transform, "갑판 3층 배치");
        found[0].transform.position = position;

        string extra = found.Length > 1 ? $"  ⚠ {found.Length}개 중 첫 번째만" : "";
        log.AppendLine($"    {label,-14}  {Describe(position)}{extra}");
    }

    // ------------------------------------------------------------
    // 사람
    // ------------------------------------------------------------

    private static void MovePlayers(StringBuilder log)
    {
        TaskWorker[] players = Object.FindObjectsByType<TaskWorker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (players.Length == 0)
        {
            return;
        }

        // 중간갑판에서 출항한다. 어느 층으로든 한 번에 갈 수 있는 자리다.
        for (int i = 0; i < players.Length; i++)
        {
            float x = -3f + i * 2f;

            Undo.RecordObject(players[i].transform, "갑판 3층 배치");
            players[i].transform.position = new Vector3(x, MainHeight + 0.1f, -1f);
        }

        log.AppendLine();
        log.AppendLine($"  사람 {players.Length}명을 중간갑판에 세움");
    }

    // ------------------------------------------------------------

    /// <summary>
    /// 예전 평면 갑판을 끈다.
    ///
    /// 지우지 않고 끄기만 합니다. 무엇이 물고 있는지 모르는 상태에서 지우면
    /// 되돌리기가 어렵습니다. 확인한 뒤에 손으로 지우면 됩니다.
    /// </summary>
    private static void RetireOldDeck(StringBuilder log)
    {
        GameObject old = GameObject.Find("Deck");

        if (old == null || !old.activeSelf)
        {
            return;
        }

        Undo.RecordObject(old, "갑판 3층 배치");
        old.SetActive(false);

        log.AppendLine();
        log.AppendLine("  예전 평면 갑판(Deck)을 껐습니다. 확인 후 지우세요.");
    }

    private static string Describe(Vector3 p) => $"x {p.x,5:F1}  y {p.y,4:F1}  z {p.z,6:F1}";

    private static Transform FindOrCreateGroup(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found == null)
        {
            GameObject made = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(made, "갑판 3층 배치");
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
        Undo.RegisterCreatedObjectUndo(made, "갑판 3층 배치");
        made.transform.SetParent(parent, false);
        return made.transform;
    }
}

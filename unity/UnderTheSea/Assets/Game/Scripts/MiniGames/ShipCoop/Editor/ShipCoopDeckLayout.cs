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

    // ------------------------------------------------------------
    // 배에 있는 실물의 자리 — 작업 자리를 여기에 맞춘다
    //
    // ⚠ 이걸 안 맞추면 **화면에 보이는 대포와 붙는 자리가 따로 놉니다.**
    //    조타만 우연히 맞았고 돛과 대포는 1.7m / 13m 어긋나 있었습니다.
    //    모델에서 잰 값이라, 배를 바꾸면 다시 재야 합니다.
    // ------------------------------------------------------------

    /// <summary>조타륜(Wheel). 선미루 위.</summary>
    private const float WheelX = 1.05f;
    private const float WheelZ = -7.93f;

    /// <summary>주 돛대(MastMid) 밑동. 굵기 1.25m.</summary>
    private const float MastMidX = 1.03f;
    private const float MastMidZ = 3.80f;

    // 💣 대포는 배 모델에서 **중간갑판**에 놓여 있습니다. 설계는 앞갑판입니다. (4장)
    //
    //    중간갑판에 두면 돛과 3.1m 거리로 붙어서 **층을 나눈 의미가 없어지고**,
    //    앞갑판에는 뱃전만 남습니다. 그래서 자리를 바꾸는 대신
    //    **배에 있는 대포를 앞갑판으로 옮깁니다.** (MoveShipCannon)
    //
    //    ⚠ **대포는 우현(+x)을 봅니다.** 포신(가는 쪽)이 +x 끝에 있습니다.
    //       x 는 대포 **한가운데**라, 포신 끝은 여기서 +1.74m 입니다.
    //       4.45 면 포신이 6.19 — 난간(6.42) 바로 앞입니다. 더 밀면 난간을 뚫습니다.
    private const float CannonX = 4.45f;
    private const float CannonZ = 13.65f;

    // ------------------------------------------------------------
    // 바다 높이 — 암초 · 섬 · 항로선이 여기에 놓인다
    //
    // ⚠ 이걸 안 맞추면 **암초가 갑판 위로 굴러옵니다.**
    //    `VoyageSea` 가 원점(0,0,0)에 있었는데, 실제 배는 중간갑판이 y -3.49 라
    //    바다가 갑판보다 3.5m 위였습니다. 항로선(노란 선)도 갑판을 가로질렀습니다.
    //    회색 큐브 시절(갑판 y = 0)에 맞춰둔 값이 그대로 남아 있던 것입니다.
    //
    // 흘수선은 배에서 읽었습니다.
    //
    //   선체가 가장 넓은 곳    y  -4.0   11.20m
    //   포문(GunPorts) 밑      y  -7.39  ← 물 위에 있어야 한다
    //   용골(선체 맨 아래)     y -11.46
    //
    // 포문 바로 아래인 -8.0 으로 둡니다. 잠기는 깊이 3.5m, 건현 4.5m 입니다.
    // ------------------------------------------------------------

    private const float SeaLevelY = -8.0f;

    // ------------------------------------------------------------
    // 바다 판 — **뒤로 흘러야 배가 나아가 보인다**
    //
    // 배는 실제로 앞으로 가지 않습니다. 제자리에 서 있고 **바다가 뒤로 흘러갑니다.**
    // (VoyageSea) 그래서 물도 같이 흘러야 합니다. 가만히 있으면 배가 멈춰 보입니다.
    //
    // 이어 붙이는 방법
    //   한 장을 뒤로 밀다가 되돌리면 **툭 튑니다.** 대신 **같은 판을 60m 간격으로
    //   여러 장 깔고 다 같이 밉니다.** 60m 밀린 순간 뒤판이 앞판 자리에 정확히
    //   들어가 있어서, 되돌려도 그림이 안 바뀝니다.
    //
    // 로비가 쓰는 물과 같은 재질입니다. 씬마다 바다 색이 다르면 어색합니다.
    // ------------------------------------------------------------

    /// <summary>로비가 쓰는 물. 여기서 베껴 온다.</summary>
    private const string LobbyWaterPath =
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Materials/Water_River.mat";

    /// <summary>
    /// 우리 전용 물. 로비 것을 그대로 베낀 사본입니다.
    ///
    /// ⚠ **원본을 쓰면 안 됩니다.** 물결을 밀려면 재질 값을 매 프레임 바꿔야 하는데,
    ///    원본을 건드리면 **로비 물까지 같이 흘러갑니다.**
    ///    색을 바꾸고 싶으면 이 사본만 고치면 로비는 그대로입니다.
    /// </summary>
    private const string WaterMaterialPath = "Assets/Game/Art/Materials/ShipCoop/SeaWater.mat";

    private const string SeaGroupName = "Sea";

    /// <summary>판 한 장의 앞뒤 길이 (m). 이 길이로 되돌린다.</summary>
    private const float SeaTileLength = 60f;

    /// <summary>판의 좌우 폭 (m). 화면 밖까지 덮어야 끝이 안 보인다.</summary>
    private const float SeaWidth = 400f;

    /// <summary>배 뒤로 이만큼부터 깐다.</summary>
    private const float SeaBackZ = -120f;

    /// <summary>배 앞으로 이만큼까지 깐다. 목적지 섬(400m)보다 멀어야 한다.</summary>
    private const float SeaFrontZ = 480f;

    // ------------------------------------------------------------
    // 물거품 줄무늬 — **흐르는 것이 보이게 하는 유일한 것**
    //
    // ⚠ 물 판을 밀어도 **물결이 안 따라옵니다.**
    //
    //    `Water_River` 는 Synty 물 셰이더인데, 무늬를 **월드 좌표로** 그리고
    //    **시간으로 스스로 일렁입니다.** (`_Distortion_Speed` · `_Caustics_Speed`)
    //    그래서 판을 아무리 뒤로 밀어도 제자리에서 출렁이기만 하고,
    //    배가 나아가는 것은 표현이 안 됩니다. 실제로 잘 밀리고 있는데도
    //    멈춰 있는 것처럼 보입니다.
    //
    // 그래서 **물 위에 얹은 물체**로 보여줍니다. 이건 월드 좌표와 상관없이
    // 자기가 움직이므로 눈에 그대로 보입니다. 배 쪽으로 쓸려 내려오면
    // 속도가 읽히고, 돛을 당겨 빨라지면 더 빨리 지나갑니다.
    //
    // ⚠ **잘고 가늘어야 물거품으로 보입니다.** 처음에 7~15m 짜리로 깔았더니
    //    바다 위에 흰 판때기가 둥둥 뜬 것처럼 보였습니다.
    //
    // **한 칸(60m) 안의 무늬를 모든 칸에 똑같이 되풀이합니다.** 그래야 60m 마다
    // 되돌릴 때 앞칸이 뒷칸 자리에 정확히 들어가서 안 튑니다.
    // ------------------------------------------------------------

    // ⚠ **지금은 꺼 두었습니다.**
    //
    //    납작한 사각형을 물 위에 얹으면 아무리 잘게 줄여도 **흰 판때기로 보입니다.**
    //    7~15m 로도, 2~5m 로도 마찬가지였습니다. 그림 없는 네모라서 그렇습니다.
    //
    //    제대로 하려면 물거품 **그림(텍스처)** 이나 파티클이 있어야 합니다.
    //    그건 그림 작업이라 여기서 숫자로 만들 수 없습니다.
    //    재료가 생기면 이 스위치만 켜면 자리는 그대로 잡힙니다.
    private const bool UseFoam = false;

    private const string FoamMaterialPath = "Assets/Game/Art/Materials/ShipCoop/Wave.mat";

    /// <summary>줄무늬 굵기 (m). 굵으면 판때기로 보인다.</summary>
    private const float FoamWidth = 0.32f;

    /// <summary>물 위로 살짝 띄운다. 같은 높이면 서로 지지고 깜빡인다.</summary>
    private const float FoamLift = 0.05f;

    private const float FoamBackZ = -60f;
    private const float FoamFrontZ = 300f;

    /// <summary>한 칸(60m)에 놓을 물거품. { 좌우 x, 칸 안에서의 앞뒤 0~1, 길이 m }</summary>
    private static readonly float[,] FoamSpots =
    {
        { -74f, 0.03f, 4.0f },
        { -41f, 0.09f, 2.6f },
        {   9f, 0.14f, 3.4f },
        {  52f, 0.19f, 2.2f },
        { -23f, 0.27f, 4.6f },
        {  78f, 0.33f, 3.0f },
        { -58f, 0.41f, 2.4f },
        {  27f, 0.46f, 3.8f },
        {  -9f, 0.54f, 2.8f },
        {  63f, 0.61f, 4.2f },
        { -86f, 0.68f, 3.2f },
        {  41f, 0.72f, 2.5f },
        { -31f, 0.79f, 3.6f },
        {  16f, 0.85f, 2.9f },
        {  88f, 0.91f, 3.3f },
        { -50f, 0.96f, 2.7f },
    };

    // 갑판 면에서 얼마나 띄울지. 오브젝트마다 크기가 달라 0 이면 바닥에 묻힌다.
    private const float HelmLift = 0.70f;
    private const float SailLift = 1.60f;
    private const float CannonLift = 0.70f;
    private const float DumpLift = 0.45f;
    private const float BoxLift = 0.45f;
    private const float DamageLift = 0.40f;

    private const string RootName = "ShipDecks";

    // ------------------------------------------------------------

    /// <summary>뒤쪽 경사로가 중간갑판 쪽으로 뻗어 끝나는 z. 여기보다 앞에 물건을 둔다.</summary>
    private static float RampEndZ => MidBackZ + AftStairRun;

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
        BuildWalls(root, log);
        MoveSea(log);
        MoveStations(log);
        MovePlayers(log);
        AttachCharacters(log);

        EditorSceneManager.MarkAllScenesDirty();

        log.AppendLine();
        log.AppendLine("씬을 저장해야 남습니다. (Ctrl+S)");

        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------
    // 배 모델
    // ------------------------------------------------------------

    /// <summary>
    /// 배를 씬에 놓고 **콜라이더를 전부 켠다.**
    ///
    /// ⚠ 걸으라고 켜는 것이 아닙니다. 걷는 바닥은 여전히 **보이지 않는 평평한 큐브**가
    ///    맡습니다. 배의 `MeshCollider` 47개는 계단과 난간이 울퉁불퉁해서 그 위로는
    ///    걸어다닐 수가 없습니다.
    ///
    /// 켜는 이유는 두 가지, **둘 다 레이를 맞히기 위해서**입니다.
    ///
    ///   발 높이   `ShipCoopCharacter` 가 보이는 갑판을 찾아 발을 맞춥니다.
    ///             걷는 큐브와 보이는 갑판이 층마다 다르게 어긋나 있습니다.
    ///   가림      `ShipCoopCamera` 가 카메라와 사람 사이에 뭐가 끼었는지 봅니다.
    ///             난간·돛대는 카메라를 돌리면 비켜나므로 그때만 감춥니다.
    ///
    /// **캐릭터는 배를 통과합니다.** `DebugPlayerMover.IgnoreTheShip` 이
    /// `Physics.IgnoreCollision` 으로 캡슐과 배를 통째로 떼어놓습니다.
    /// 그래서 콜라이더가 켜져 있어도 걷는 느낌은 하나도 안 바뀝니다.
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

        int on = 0;

        foreach (Collider hit in ship.GetComponentsInChildren<Collider>(true))
        {
            if (hit.enabled)
            {
                on++;
                continue;
            }

            Undo.RecordObject(hit, "배 모델과 갑판 배치");
            hit.enabled = true;
            on++;
        }

        log.AppendLine($"  배 모델을 놓고 콜라이더 {on}개를 켰습니다. " +
                       "(걷는 용도가 아니라 **레이를 맞히는 용도** — 위 주석 참고)");

        TurnOffShipParts(ship, log);
        MoveShipCannon(ship, log);
    }

    /// <summary>
    /// 배에서 **아예 빼는 부분.**
    ///
    /// 층마다 감췄다 보였다 하는 것(`hideWhenHere`)과 다릅니다. 이건 **늘 꺼둡니다.**
    /// 어느 층에서 봐도 방해만 되는 것들입니다.
    ///
    /// **뒷 돛대 한 벌을 통째로 뺍니다.** 뒷갑판이 조타 자리라 여기가 제일 중요한데,
    /// 돛대가 카메라에서 9.5m 앞에 서 있어서 화면을 세로로 가릅니다.
    ///
    ///   MastAft     기둥. 화면을 위아래로 가로지른다
    ///   SailAft     뒷 돛. 조타륜 바로 위를 검게 덮는다
    ///   Flag_02     그 기둥 꼭대기의 깃발
    ///   RiggingAft  그 기둥에 걸린 밧줄 사다리
    ///
    /// 넷을 같이 빼는 이유는 **하나만 빼면 나머지가 허공에 뜨기** 때문입니다.
    /// 게임에서 쓰이지도 않습니다 — ⛵ 돛 작업은 주 돛대 하나만 씁니다.
    ///
    /// ⚠ **프리팹 파일은 건드리지 않습니다.** 씬에 놓인 배에서만 끕니다.
    ///
    ///    `P_PirateShip` 은 **로비(Lobby.unity)도 씁니다.** 프리팹에서 지우면
    ///    로비에 서 있는 배의 돛대까지 말없이 없어집니다.
    ///    되돌리려면 이 목록에서 이름만 빼면 됩니다.
    /// </summary>
    private static readonly string[] RemoveFromShip =
    {
        "MastAft", "SailAft", "Flag_02", "RiggingAft",
    };

    private static void TurnOffShipParts(GameObject ship, StringBuilder log)
    {
        int turned = 0;

        foreach (Transform t in ship.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(RemoveFromShip, t.name) < 0)
            {
                continue;
            }

            if (t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "배 모델과 갑판 배치");
                t.gameObject.SetActive(false);
            }

            turned++;
        }

        if (turned > 0)
        {
            log.AppendLine($"  배에서 {turned}개를 껐습니다: {string.Join(", ", RemoveFromShip)}");
        }
    }

    /// <summary>
    /// 배에 달린 대포를 **앞갑판으로 옮긴다.**
    ///
    /// 이 배는 대포가 하나뿐이고 그게 중간갑판에 있습니다. 설계는 앞갑판입니다. (4장)
    /// 자리를 배에 맞추면 돛과 3.1m 거리로 붙어서 **층을 나눈 의미가 없어지고**,
    /// 앞갑판에는 뱃전만 남습니다.
    ///
    /// 그래서 **보이는 대포를 옮깁니다.** 자리를 옮기는 것보다 이쪽이 맞습니다 —
    /// 설계가 먼저이고 모델이 거기에 맞춰야 합니다.
    ///
    /// ⚠ **지금 어디 있든 같은 자리로 간다.** 처음에는 "원래 자리에서 이만큼"
    ///    으로 옮겼는데, 그러면 배치를 두 번 돌릴 때 **대포가 두 번 날아갑니다.**
    ///    이 도구는 되풀이해 돌리는 것이 전제라 그러면 안 됩니다.
    ///    그래서 지금 차지하고 있는 자리(bounds)를 재서 목표에 맞춥니다.
    /// </summary>
    private static void MoveShipCannon(GameObject ship, StringBuilder log)
    {
        Transform cannon = null;

        foreach (Transform t in ship.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cannon")
            {
                cannon = t;
                break;
            }
        }

        Renderer draw = cannon != null ? cannon.GetComponent<Renderer>() : null;

        if (draw == null)
        {
            log.AppendLine("  ⚠ 배에서 대포를 못 찾았습니다. 앞갑판에 대포가 안 보일 겁니다.");
            return;
        }

        Bounds area = draw.bounds;

        // 앞뒤·좌우는 한가운데를 맞추고, 높이는 **바닥이 갑판에 닿게** 맞춘다.
        Vector3 move = new Vector3(
            CannonX - area.center.x,
            ForeSurfaceY - area.min.y,
            CannonZ - area.center.z);

        Undo.RecordObject(cannon, "배 모델과 갑판 배치");
        cannon.position += move;

        log.AppendLine($"  대포를 앞갑판으로 옮겼습니다. 포신 끝 x {area.max.x + move.x:F2}");
    }

    // ------------------------------------------------------------
    // 배 밖으로 못 나가게 하는 벽
    // ------------------------------------------------------------

    /// <summary>바닥에서 이만큼 아래부터 벽이 시작한다. 낮은 층까지 덮기 위함.</summary>
    private const float WallBottom = -5.0f;

    /// <summary>벽 꼭대기. 제일 높은 갑판(뒷갑판 0.98)보다 충분히 위.</summary>
    private const float WallTop = 4.0f;

    private const float WallThickness = 0.4f;

    /// <summary>
    /// 배 둘레에 **보이지 않는 벽**을 세운다.
    ///
    /// 떨어지면 바다인데 **다시 올라올 방법이 없습니다.** 남은 시간 동안 그 사람은
    /// 게임에서 빠지고, 4명이 해야 하는 일이 3명 몫이 됩니다. 벌이 너무 큽니다.
    ///
    /// 난간 메시를 콜라이더로 쓰지 않는 이유는 갑판과 같습니다 —
    /// 난간에는 살 사이에 틈이 있어서 캡슐이 끼거나 빠져나갑니다.
    /// 평평한 판 4장이 확실합니다.
    ///
    /// 층마다 따로 두르지 않고 **배 전체를 한 번에** 두릅니다. 층 사이는
    /// 높이 차이가 알아서 막아주고, 떨어질 수 있는 곳은 바깥쪽뿐입니다.
    /// </summary>
    private static void BuildWalls(Transform root, StringBuilder log)
    {
        float left = CenterX - DeckWidth * 0.5f;
        float right = CenterX + DeckWidth * 0.5f;
        float back = AftBackZ;
        float front = ForeFrontZ;

        float height = WallTop - WallBottom;
        float middleY = (WallTop + WallBottom) * 0.5f;
        float middleZ = (back + front) * 0.5f;
        float length = front - back;

        MakeWall(root, "Wall_Port", new Vector3(left, middleY, middleZ),
                 new Vector3(WallThickness, height, length));

        MakeWall(root, "Wall_Starboard", new Vector3(right, middleY, middleZ),
                 new Vector3(WallThickness, height, length));

        // 앞뒤 마개는 좌우 벽 사이를 메운다. 모서리가 벌어지면 거기로 빠진다.
        MakeWall(root, "Wall_Stern", new Vector3(CenterX, middleY, back),
                 new Vector3(DeckWidth, height, WallThickness));

        MakeWall(root, "Wall_Bow", new Vector3(CenterX, middleY, front),
                 new Vector3(DeckWidth, height, WallThickness));

        log.AppendLine();
        log.AppendLine($"  벽   x {left:F1} / {right:F1}, z {back:F1} / {front:F1}, 높이 {WallBottom:F1}~{WallTop:F1}");
    }

    private static void MakeWall(Transform root, string name, Vector3 center, Vector3 size)
    {
        Transform wall = FindOrCreateBox(root, name);

        wall.localPosition = center;
        wall.localScale = size;
        wall.localRotation = Quaternion.identity;

        HideButKeepCollider(wall);
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

        // 가릴 것은 여기에 적지 않습니다. `FillBlockers` 가 **카메라에서 직접 레이를
        // 쏴서** 찾습니다. 층마다 무엇이 가리는지는 배 모델에 달린 문제라,
        // 사람이 목록으로 들고 있으면 모델을 바꿀 때마다 틀립니다.

        MakeDeck(root, "Deck_Aft", AftCenterZ, AftLength, AftSurfaceY, "뒷갑판",
                 new Vector4(13f, 7f, 1.5f, 6f));

        // 중간갑판에 서면 **뒷갑판이 통째로 눈앞을 가로막습니다.**
        // 카메라를 높여도 그 사이에 있으니 소용이 없어서, 그 층에 있는 동안 감춥니다.
        MakeDeck(root, "Deck_Main", MidCenterZ, MidLength, MidSurfaceY, "중간갑판",
                 new Vector4(15f, 8.5f, 1.5f, 6f));

        // 앞갑판은 **주 돛대와 주 돛**이 코앞을 막습니다. 카메라가 돛대에서 3m 뒤라
        // 기둥 하나가 화면을 반으로 가릅니다.
        MakeDeck(root, "Deck_Fore", ForeCenterZ, ForeLength, ForeSurfaceY, "앞갑판",
                 new Vector4(13f, 8f, 1.5f, 6f));

        log.AppendLine($"  뒷갑판   y {AftSurfaceY,6:F2}  z {AftBackZ,6:F1} ~ {AftFrontZ,5:F1}   🛞 조타");
        log.AppendLine($"  중간갑판 y {MidSurfaceY,6:F2}  z {MidBackZ,6:F1} ~ {MidFrontZ,5:F1}   ⛵ 돛 · 📦 상자");
        log.AppendLine($"  앞갑판   y {ForeSurfaceY,6:F2}  z {ForeBackZ,6:F1} ~ {ForeFrontZ,5:F1}   💣 대포 · 🪣 뱃전");
    }

    private static void MakeDeck(Transform root, string name, float centerZ, float length,
                                 float surfaceY, string label, Vector4 view)
    {
        Transform deck = FindOrCreateBox(root, name);

        deck.localScale = new Vector3(DeckWidth, DeckThickness, length);
        deck.localPosition = new Vector3(CenterX, surfaceY - DeckThickness * 0.5f, centerZ);

        // 가리는 것을 레이로 찾으려면 이 갑판이 물리에 올라와 있어야 한다.
        Physics.SyncTransforms();

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

        // 여기서 카메라 값을 먼저 확정해야 FillBlockers 가 같은 구도로 레이를 쏜다.
        so.ApplyModifiedProperties();

        FillBlockers(so, mark, view);

        so.ApplyModifiedProperties();
    }
    /// <summary>
    /// 이 층에 있을 때 감출 배 부분들을 찾아 넣는다.
    ///
    /// **이름 목록으로 관리하지 않습니다.** 하나씩 적으면 반드시 빠뜨리고,
    /// 빠진 것은 화면에 덩그러니 떠서 앞을 가립니다. 배 모델을 바꾸면 또 다시 적어야 합니다.
    ///
    /// 그래서 **카메라에서 갑판으로 실제로 레이를 쏴 봅니다.** 맞는 것이 가리는 것입니다.
    /// 처음에는 "이 z 보다 뒤에 있는 것 전부" 로 잡았는데, 앞갑판에서는 그러면
    /// **선체(Hull)까지 잡혀서** 배가 통째로 사라집니다. 앞갑판을 가리는 것은
    /// 뒤에 있는 구조물이 아니라 **한가운데 서 있는 주 돛대와 돛**이었습니다.
    ///
    /// 갑판 위에 있는 것은 감추지 않습니다. 조타륜 · 대포 · 앞 돛대는 가려도
    /// **그 갑판의 물건**이라, 없으면 오히려 어디가 어딘지 모릅니다.
    /// 감추는 것은 **카메라와 갑판 사이에 끼어든 것**뿐입니다.
    /// </summary>
    private static void FillBlockers(SerializedObject so, ShipDeck deck, Vector4 view)
    {
        SerializedProperty list = so.FindProperty("hideWhenHere");
        list.arraySize = 0;

        GameObject ship = GameObject.Find(ShipName);

        if (ship == null || deck == null)
        {
            return;
        }

        // 배 콜라이더는 평소에 꺼져 있다. 재는 동안만 켠다.
        var turnedOn = new System.Collections.Generic.List<Collider>();

        foreach (Collider c in ship.GetComponentsInChildren<Collider>(true))
        {
            if (!c.enabled)
            {
                c.enabled = true;
                turnedOn.Add(c);
            }
        }

        Physics.SyncTransforms();

        Bounds area = deck.Area;
        Vector3 pivot = deck.Center;
        Vector3 camera = pivot + new Vector3(0f, view.y, -view.x);

        var found = new System.Collections.Generic.List<Renderer>();

        // 갑판을 촘촘히 훑는다. 한 점만 보면 그 각도에서만 안 가린다.
        for (int ix = 0; ix <= SightSamples; ix++)
        {
            for (int iz = 0; iz <= SightSamples; iz++)
            {
                float x = Mathf.Lerp(area.min.x + 0.5f, area.max.x - 0.5f, ix / (float)SightSamples);
                float z = Mathf.Lerp(area.min.z + 0.5f, area.max.z - 0.5f, iz / (float)SightSamples);

                // 발치와 머리끝 둘 다 본다.
                CollectBlockers(found, ship, deck, camera, new Vector3(x, area.max.y + 0.1f, z));
                CollectBlockers(found, ship, deck, camera, new Vector3(x, area.max.y + 1.6f, z));
            }
        }

        foreach (Collider c in turnedOn)
        {
            c.enabled = false;
        }

        Physics.SyncTransforms();

        list.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
    }

    /// <summary>몇 칸으로 나눠 훑을지. 촘촘할수록 확실하지만 느려진다.</summary>
    private const int SightSamples = 8;

    /// <summary>
    /// 가려도 **절대 감추지 않는 것.**
    ///
    ///   Hull      선체는 갑판 아래로도 이어져 있어서 어느 각도에서든 레이에 걸립니다.
    ///             감추면 갑판만 바다 위에 떠 있게 됩니다.
    ///
    ///   Stairs*   걸어다니는 경사로가 보이지 않기 때문에, 계단 모델이
    ///             **어디로 올라가는지 알려주는 유일한 단서**입니다.
    ///             한 번 감췄다가 "계단이 아예 없어졌다" 는 소리를 들었습니다.
    ///             계단은 두 층에 걸쳐 있어서 자동으로는 반드시 잡힙니다.
    ///
    ///   Railing*  난간은 **갑판이 어디서 끝나는지 알려주는 표시**입니다.
    ///             떨어지는 것은 보이지 않는 벽이 막고 있어서, 난간이 없으면
    ///             어디까지가 바닥인지 눈으로 읽을 수가 없습니다.
    ///
    ///             ⚠ 한 층의 난간이 **메시 하나**입니다. 앞쪽만 감출 수가 없어서
    ///               감추면 그 층 난간이 **사방 통째로** 사라집니다.
    ///               가리는 문제는 감추는 대신 **카메라를 올려서** 풉니다.
    /// </summary>
    private static readonly string[] NeverHide =
    {
        "Hull",
        "StairsUpper", "StairsFore", "StairsLower",
        "RailingAft", "RailingMid", "RailingFore",
    };

    private static void CollectBlockers(System.Collections.Generic.List<Renderer> found,
                                        GameObject ship, ShipDeck deck,
                                        Vector3 camera, Vector3 spot)
    {
        Vector3 toSpot = spot - camera;
        RaycastHit[] hits = Physics.RaycastAll(camera, toSpot.normalized, toSpot.magnitude);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].collider.transform;

            if (!t.IsChildOf(ship.transform))
            {
                continue;
            }

            // 갑판 위에 있는 것은 그 갑판의 물건이다. 가려도 감추지 않는다.
            if (hits[i].point.z >= deck.Area.min.z)
            {
                continue;
            }

            if (System.Array.IndexOf(NeverHide, t.name) >= 0)
            {
                continue;
            }

            Renderer draw = t.GetComponent<Renderer>();

            if (draw != null && !found.Contains(draw))
            {
                found.Add(draw);
            }
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

        // ⚠ **배에 있는 실물 위에 놓는다.** 갑판 한가운데 같은 "적당한" 자리에 두면
        //    보이는 조타륜·돛대·대포와 어긋나서, 화면에는 대포가 저기 있는데
        //    붙는 자리는 여기가 됩니다. 아래 숫자는 모델에서 직접 잰 값입니다.

        // 🛞 조타 — 배의 조타륜(Wheel). 선미루 위.
        Place<HelmTask>(log, "🛞 조타", WheelX, AftSurfaceY + HelmLift, WheelZ);

        // ⛵ 돛 — 배의 주 돛대(MastMid) 밑동. 중간갑판.
        Place<SailTask>(log, "⛵ 돛", MastMidX, MidSurfaceY + SailLift, MastMidZ);

        // 💣 대포 — 앞갑판 우현. 배에 있는 대포를 여기로 옮겨 온다. (MoveShipCannon)
        Place<CannonTask>(log, "💣 대포", CannonX, ForeSurfaceY + CannonLift, CannonZ);

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
                    // ⚠ 경사로 앞에 둔다. 예전 자리(MidBackZ + 3)는 경사로 **안**이었다.
                    //    좌우 계단이 z MidBackZ ~ MidBackZ + AftStairRun 을 차지한다.
                    where = new Vector3(CenterX, MidSurfaceY + BoxLift, RampEndZ + 0.8f);
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
            new Vector3(CenterX - 3.5f, MidSurfaceY + DamageLift, RampEndZ + 1.0f),
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

        // 자리 표시용 큐브는 끈다. **배에 있는 실물이 대신 보여줍니다.**
        // 조타륜 · 돛대 · 대포 위에 정확히 올려 두었으므로 큐브는 그 안에 묻혀 있고,
        // 삐져나오면 오히려 지저분합니다. 콜라이더는 살려둡니다 — 돛대와 대포는
        // 걸어서 통과하면 안 되는 물건입니다.
        foreach (Renderer draw in found[0].GetComponentsInChildren<Renderer>(true))
        {
            if (draw.enabled)
            {
                Undo.RecordObject(draw, "배 모델과 갑판 배치");
                draw.enabled = false;
            }
        }

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
        float startZ = RampEndZ + 2f;
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

    /// <summary>
    /// 바다를 흘수선으로 내린다.
    ///
    /// `VoyageSea` 하나가 **암초 · 섬 · 항로선을 전부** 자기 자리 기준으로 놓습니다.
    /// (`Place` · `Origin`) 그래서 이것만 옮기면 셋이 같이 따라옵니다.
    ///
    /// `origin` 칸을 비워야 이 오브젝트의 자리를 씁니다. 채워져 있으면 그쪽이 이깁니다.
    /// </summary>
    private static void MoveSea(StringBuilder log)
    {
        VoyageSea sea = Object.FindAnyObjectByType<VoyageSea>(FindObjectsInactive.Include);

        if (sea == null)
        {
            log.AppendLine("  ⚠ VoyageSea 를 못 찾았습니다. 암초가 갑판 위로 올라옵니다.");
            return;
        }

        Vector3 want = new Vector3(CenterX, SeaLevelY, 0f);

        Undo.RecordObject(sea.transform, "배 모델과 갑판 배치");
        sea.transform.position = want;

        SerializedObject so = new SerializedObject(sea);
        SerializedProperty origin = so.FindProperty("origin");

        if (origin.objectReferenceValue != null)
        {
            origin.objectReferenceValue = null;
            so.ApplyModifiedProperties();
            log.AppendLine("  VoyageSea 의 origin 칸을 비웠습니다. (채워져 있으면 그쪽이 이깁니다)");
        }

        log.AppendLine();
        log.AppendLine($"  바다   {Describe(want)}   (갑판보다 {MidSurfaceY - SeaLevelY:F1}m 아래)");

        BuildSeaTiles(sea, log);
        SeeFarEnough(log);
        HideResult(log);
    }

    /// <summary>
    /// 결과 화면을 **끈 채로 저장한다.**
    ///
    /// 플레이를 누르면 `ShipCoopResultView.Awake` 가 알아서 숨기므로 게임에는
    /// 지장이 없습니다. 다만 **켜진 채로 저장돼 있으면 에디터 Game 뷰에
    /// "항해 성공" 이 떠 있습니다.** 진행도 0%, 0분 0초로요.
    /// 플레이가 꺼져 있는지 켜져 있는지 헷갈리게 만듭니다.
    /// </summary>
    private static void HideResult(StringBuilder log)
    {
        ShipCoopResultView view = Object.FindAnyObjectByType<ShipCoopResultView>(FindObjectsInactive.Include);

        if (view == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(view);
        Object panel = so.FindProperty("panel").objectReferenceValue;
        GameObject go = panel as GameObject;

        if (go == null || !go.activeSelf)
        {
            return;
        }

        Undo.RecordObject(go, "배 모델과 갑판 배치");
        go.SetActive(false);

        log.AppendLine("  결과 화면이 켜져 있어서 껐습니다. (에디터에서 '항해 성공' 이 떠 있던 것)");
    }

    /// <summary>
    /// 카메라가 **목적지 섬까지 보이게** 사거리를 늘린다.
    ///
    /// ⚠ 카메라 far 가 200m 인데 섬은 **400m 앞**에 뜹니다. (`islandFarDistance`)
    ///    그래서 게임의 목표가 화면에 한 번도 안 나왔습니다.
    ///    "저기까지 가면 된다" 가 안 보이면 진행도 막대가 무슨 뜻인지도 모릅니다. (9장)
    ///
    /// 바다 판이 끝나는 곳(480m)보다 조금 더 멀리 둡니다. 그래야 바다 끝이
    /// 잘려 보이지 않고 **수평선처럼** 하늘과 맞닿습니다.
    /// </summary>
    private static void SeeFarEnough(StringBuilder log)
    {
        ShipCoopCamera rig = Object.FindAnyObjectByType<ShipCoopCamera>(FindObjectsInactive.Include);
        Camera cam = rig != null ? rig.GetComponent<Camera>() : Camera.main;

        if (cam == null)
        {
            return;
        }

        float want = SeaFrontZ + 20f;

        if (cam.farClipPlane >= want)
        {
            return;
        }

        Undo.RecordObject(cam, "배 모델과 갑판 배치");
        log.AppendLine($"  카메라 사거리 {cam.farClipPlane:F0}m → {want:F0}m (목적지 섬이 400m 앞에 뜬다)");
        cam.farClipPlane = want;
    }

    /// <summary>
    /// 물 판을 깔고 `VoyageSea` 의 "속도에 맞춰 흐를 것" 목록에 넣는다.
    ///
    /// 판은 `VoyageSea` 의 자식으로 둡니다. 바다 높이를 바꾸면 같이 따라 내려갑니다.
    /// 콜라이더는 지웁니다 — 물은 밟는 것이 아니고, 남겨두면 갑판을 재는 레이가
    /// 물에 먼저 맞아서 배치 도구가 엉뚱한 값을 읽습니다.
    /// </summary>
    private static void BuildSeaTiles(VoyageSea sea, StringBuilder log)
    {
        Material water = GetOrCopyWater(log);

        if (water == null)
        {
            return;
        }

        Transform group = FindOrCreateGroup(sea.transform, SeaGroupName);

        for (int i = group.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(group.GetChild(i).gameObject);
        }

        var scroll = new System.Collections.Generic.List<Transform>();

        int count = Mathf.CeilToInt((SeaFrontZ - SeaBackZ) / SeaTileLength);

        for (int i = 0; i < count; i++)
        {
            // 유니티 기본 Plane 은 한 변이 10m 다. 그래서 10 으로 나눈다.
            Transform tile = MakeFlat(group, $"Water_{i + 1:00}", water,
                new Vector3(SeaWidth / 10f, 1f, SeaTileLength / 10f),
                new Vector3(0f, 0f, SeaBackZ + SeaTileLength * (i + 0.5f)));

            scroll.Add(tile);
        }

        int foam = BuildFoam(group, scroll, log);

        SerializedObject so = new SerializedObject(sea);

        so.FindProperty("scrollLoopLength").floatValue = SeaTileLength;

        SerializedProperty list = so.FindProperty("scrollWithSpeed");
        list.arraySize = scroll.Count;

        for (int i = 0; i < scroll.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = scroll[i];
        }

        so.ApplyModifiedProperties();

        // 물결을 배 속도로 밀어준다. 판을 옮기는 것만으로는 무늬가 안 따라온다.
        if (group.GetComponent<ShipCoopSeaFlow>() == null)
        {
            Undo.AddComponent<ShipCoopSeaFlow>(group.gameObject);
            log.AppendLine("  물결을 배 속도로 밀도록 ShipCoopSeaFlow 를 붙였습니다");
        }

        log.AppendLine($"  물 판 {count}장 ({SeaWidth:F0} × {SeaTileLength:F0}m) · 물거품 {foam}줄 을 깔고 " +
                       $"{SeaTileLength:F0}m 마다 되돌리게 했습니다");
    }

    /// <summary>
    /// 우리 전용 물 재질을 준비한다. 없으면 로비 것을 베껴 온다.
    ///
    /// 베끼는 이유는 위 `WaterMaterialPath` 주석에 적어 두었습니다 —
    /// 원본을 밀면 로비 물까지 흘러갑니다.
    /// </summary>
    private static Material GetOrCopyWater(StringBuilder log)
    {
        Material mine = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);

        if (mine != null)
        {
            return mine;
        }

        Material lobby = AssetDatabase.LoadAssetAtPath<Material>(LobbyWaterPath);

        if (lobby == null)
        {
            log.AppendLine($"  ⚠ 로비 물 재질을 못 찾음: {LobbyWaterPath}");
            return null;
        }

        if (!AssetDatabase.CopyAsset(LobbyWaterPath, WaterMaterialPath))
        {
            log.AppendLine($"  ⚠ 물 재질을 베끼지 못했습니다: {WaterMaterialPath}");
            return null;
        }

        AssetDatabase.ImportAsset(WaterMaterialPath);
        log.AppendLine($"  로비 물을 베껴 우리 것을 만들었습니다: {WaterMaterialPath}");

        return AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
    }

    /// <summary>
    /// 물거품 줄무늬를 깐다. 몇 줄 깔았는지 돌려준다.
    ///
    /// 물 판만으로는 흐르는 것이 안 보입니다. 이유는 위 주석에 적어 두었습니다.
    /// </summary>
    private static int BuildFoam(Transform group, System.Collections.Generic.List<Transform> scroll,
                                 StringBuilder log)
    {
        if (!UseFoam)
        {
            return 0;
        }

        Material foamMaterial = AssetDatabase.LoadAssetAtPath<Material>(FoamMaterialPath);

        if (foamMaterial == null)
        {
            log.AppendLine($"  ⚠ 물거품 재질을 못 찾음: {FoamMaterialPath}. 흐르는 것이 안 보입니다.");
            return 0;
        }

        int bands = Mathf.CeilToInt((FoamFrontZ - FoamBackZ) / SeaTileLength);
        int spots = FoamSpots.GetLength(0);
        int made = 0;

        for (int band = 0; band < bands; band++)
        {
            for (int i = 0; i < spots; i++)
            {
                float x = FoamSpots[i, 0];
                float z = FoamBackZ + SeaTileLength * (band + FoamSpots[i, 1]);
                float length = FoamSpots[i, 2];

                MakeFlat(group, $"Foam_{band + 1:00}_{i + 1}", foamMaterial,
                    new Vector3(length / 10f, 1f, FoamWidth / 10f),
                    new Vector3(x, FoamLift, z));

                scroll.Add(group.GetChild(group.childCount - 1));
                made++;
            }
        }

        return made;
    }

    /// <summary>물 위에 눕히는 납작한 판 하나. 콜라이더는 지운다.</summary>
    private static Transform MakeFlat(Transform group, string name, Material paint,
                                      Vector3 scale, Vector3 at)
    {
        GameObject made = GameObject.CreatePrimitive(PrimitiveType.Plane);
        made.name = name;
        Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

        Collider bump = made.GetComponent<Collider>();

        if (bump != null)
        {
            // 물은 밟는 것이 아니다. 남겨두면 갑판을 재는 레이가 물에 먼저 맞는다.
            Object.DestroyImmediate(bump);
        }

        made.GetComponent<Renderer>().sharedMaterial = paint;

        made.transform.SetParent(group, false);
        made.transform.localScale = scale;
        made.transform.localPosition = at;

        return made.transform;
    }

    // ------------------------------------------------------------
    // 캐릭터 — 큐브 대신 진짜 몸을 얹는다
    // ------------------------------------------------------------

    private const string CharacterPrefabPath = "Assets/Game/Prefabs/Characters/P_JaeYoung.prefab";

    /// <summary>플레이어 밑에 붙는 몸의 이름. 이 이름으로 찾아서 다시 안 만든다.</summary>
    private const string CharacterChildName = "Body";

    // ------------------------------------------------------------
    // 캐릭터를 얼마나 키울지
    //
    // **이 배는 키 3m 사람에 맞춰 만들어져 있습니다.** 모델에서 역산한 값입니다.
    //
    //   문 3.39m       (보통 2.0m)  →  사람 키 2.97m
    //   조타륜 2.08m   (보통 1.2m)  →  사람 키 3.04m
    //
    // 캐릭터(`P_JaeYoung`)는 1.2m 라, 2.25배로 키우면 2.7m 가 됩니다.
    //
    // ⚠ **프리팹이 아니라 씬에 놓인 것만 키웁니다.** 프리팹을 키우면
    //    로비에 서 있는 캐릭터까지 거인이 됩니다.
    //
    // ⚠ 키우면 **같은 속도가 느려 보입니다.** 화면에서 차지하는 크기가 커지는데
    //    초당 움직이는 거리는 그대로라서 그렇습니다. 밸런스(일 하나 10초)는
    //    거리로 잡혀 있으니 숫자는 건드리지 않습니다. 느리게 **느껴지면**
    //    속도가 아니라 카메라를 먼저 보세요. (DebugPlayerMover 주석)
    // ------------------------------------------------------------

    /// <summary>모델을 그대로 뒀을 때의 키. 메시를 재서 나온 값이다.</summary>
    private const float CharacterHeight = 1.27f;

    /// <summary>1.27m × 2.25 = 2.86m</summary>
    private const float CharacterScale = 2.25f;

    /// <summary>몸통 캡슐의 반지름. 계단 폭 1.9m 보다 충분히 좁아야 한다.</summary>
    private const float BodyRadius = 0.5f;

    /// <summary>
    /// 모델을 이만큼 **아래로 내려서 붙인다.**
    ///
    /// `CharacterController` 는 `skinWidth` 만큼 바닥에서 띄운 채로 멈춥니다.
    /// 벽에 파묻히지 않으려고 남기는 여유라 없앨 수 없습니다. 줄이면 갑판을 뚫습니다.
    ///
    /// 그래서 **캡슐은 그대로 두고 보이는 몸만** 그만큼 내립니다.
    /// 발이 갑판에 닿아 보이고, 부딪히는 것은 원래대로 돕니다.
    /// </summary>
    private static float CharacterSink => BodyRadius * 0.1f;

    /// <summary>
    /// 플레이어 큐브 밑에 **캐릭터 모델을 자식으로 얹는다.**
    ///
    /// 큐브를 캐릭터로 **바꾸지 않습니다.** 큐브에는 `TaskWorker` ·
    /// `DebugPlayerMover` · `KeyboardPlayerController` 가 붙어 있고,
    /// 카메라와 HUD 가 그것들을 찾아 씁니다. 바꾸면 연결이 다 끊어집니다.
    /// 큐브는 그대로 두고 **보이는 몫만** 캐릭터에게 넘깁니다.
    ///
    /// 모델 쪽에서 꺼야 하는 것
    /// <code>
    ///   MovePlayerInput     키보드를 직접 읽는다. 그대로 두면 한 번 누를 때
    ///                       우리 이동과 둘 다 반응해서 두 배로 걷는다
    ///   CharacterMover      저쪽 이동. 걷기 1 / 달리기 4 로 우리 밸런스와 다르다
    ///   CharacterController 두 번째 캡슐. 부모 캡슐과 서로 밀어낸다
    /// </code>
    ///
    /// `Animator` 는 살려둡니다. `ShipCoopCharacter` 가 **움직인 거리를 재서** 굴립니다.
    /// </summary>
    private static void AttachCharacters(StringBuilder log)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);

        if (prefab == null)
        {
            log.AppendLine($"  ⚠ 캐릭터 프리팹을 못 찾음: {CharacterPrefabPath}");
            return;
        }

        TaskWorker[] players = Object.FindObjectsByType<TaskWorker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        log.AppendLine();
        log.AppendLine("  캐릭터:");

        for (int i = 0; i < players.Length; i++)
        {
            Transform player = players[i].transform;

            // ⚠ **큐브의 찌그러진 크기를 없앤다.**
            //
            //    사람 모양처럼 보이려고 큐브를 (0.8, 1.2, 0.8) 로 눌러 놨습니다.
            //    자식으로 캐릭터를 넣으면 **그 찌그러짐이 그대로 딸려가서**
            //    옆으로 눌리고 위로 늘어난 사람이 됩니다.
            //    큐브는 어차피 안 보이므로 1 로 돌립니다.
            if (player.localScale != Vector3.one)
            {
                Undo.RecordObject(player, "배 모델과 갑판 배치");
                player.localScale = Vector3.one;
            }

            Transform body = player.Find(CharacterChildName);

            if (body == null)
            {
                GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                made.name = CharacterChildName;
                Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

                body = made.transform;
                body.SetParent(player, false);
            }

            Undo.RecordObject(body, "배 모델과 갑판 배치");
            body.localPosition = new Vector3(0f, -CharacterSink, 0f);
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one * CharacterScale;

            int off = TurnOffRivalMovers(body);

            // 큐브는 이제 안 보인다. 콜라이더도 CharacterController 가 대신한다.
            foreach (Renderer draw in player.GetComponents<Renderer>())
            {
                if (draw.enabled)
                {
                    Undo.RecordObject(draw, "배 모델과 갑판 배치");
                    draw.enabled = false;
                }
            }

            ShipCoopCharacter view = player.GetComponent<ShipCoopCharacter>();

            if (view == null)
            {
                view = Undo.AddComponent<ShipCoopCharacter>(player.gameObject);
            }

            AttachFootShadow(player, view);

            // 캡슐을 캐릭터 키에 맞춘다. 안 맞추면 보이는 몸과 부딪히는 몸이 따로 논다.
            DebugPlayerMover mover = player.GetComponent<DebugPlayerMover>();

            if (mover != null)
            {
                SerializedObject so = new SerializedObject(mover);
                so.FindProperty("bodyHeight").floatValue = CharacterHeight * CharacterScale;
                so.FindProperty("bodyRadius").floatValue = BodyRadius;
                so.ApplyModifiedProperties();
            }

            log.AppendLine($"    {players[i].name,-12}  키 {CharacterHeight * CharacterScale:F2}m, 저쪽 이동 {off}개 껐습니다");
        }
    }

    /// <summary>발밑 그림자 지름. 캐릭터 몸통보다 조금 넓게.</summary>
    private const float FootShadowSize = 1.6f;

    /// <summary>
    /// 발밑에 둥근 그림자를 깔고 `ShipCoopCharacter` 에 연결한다.
    ///
    /// 캐릭터의 자식이 **아닙니다.** 자식으로 두면 몸이 돌 때 같이 돌고,
    /// 계단을 오를 때 몸을 따라 기울어집니다. 바닥에 눕혀 따로 두고
    /// `ShipCoopCharacter` 가 매 프레임 바닥에 붙여 줍니다.
    /// </summary>
    private static void AttachFootShadow(Transform player, ShipCoopCharacter view)
    {
        Material paint = ShipCoopBlobShadow.GetOrCreate();

        if (paint == null)
        {
            return;
        }

        string name = player.name + "_FootShadow";
        Transform shadow = null;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
            {
                shadow = t;
                break;
            }
        }

        if (shadow == null)
        {
            GameObject made = GameObject.CreatePrimitive(PrimitiveType.Quad);
            made.name = name;
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

            Collider bump = made.GetComponent<Collider>();

            if (bump != null)
            {
                Object.DestroyImmediate(bump);
            }

            shadow = made.transform;
        }

        Renderer draw = shadow.GetComponent<Renderer>();
        draw.sharedMaterial = paint;

        // 그림자가 또 그림자를 드리우면 안 된다.
        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        draw.receiveShadows = false;

        Undo.RecordObject(shadow, "배 모델과 갑판 배치");
        shadow.SetParent(player.parent, false);

        // Quad 는 서 있으므로 눕힌다.
        shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.localScale = Vector3.one * FootShadowSize;
        shadow.position = player.position;

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("footShadow").objectReferenceValue = shadow;
        so.ApplyModifiedProperties();
    }

    /// <summary>모델에 딸려온 이동·입력·캡슐을 끈다. 몇 개 껐는지 돌려준다.</summary>
    private static int TurnOffRivalMovers(Transform body)
    {
        int off = 0;

        foreach (MonoBehaviour script in body.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string kind = script.GetType().Name;

            if (kind != "CharacterMover" && kind != "MovePlayerInput")
            {
                continue;
            }

            if (script.enabled)
            {
                Undo.RecordObject(script, "배 모델과 갑판 배치");
                script.enabled = false;
                off++;
            }
        }

        foreach (CharacterController capsule in body.GetComponentsInChildren<CharacterController>(true))
        {
            if (capsule.enabled)
            {
                Undo.RecordObject(capsule, "배 모델과 갑판 배치");
                capsule.enabled = false;
                off++;
            }
        }

        return off;
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

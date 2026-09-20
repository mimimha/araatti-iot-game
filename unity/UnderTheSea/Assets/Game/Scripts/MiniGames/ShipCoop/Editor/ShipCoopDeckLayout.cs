using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    /// <summary>배가 끝나는 자리. 도는 축을 잡는 데 쓴다. (선체에서 잰 값)</summary>
    private const float ShipBowZ = 22.8f;

    private const float ShipSternZ = -19.7f;

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

    // ------------------------------------------------------------
    // 어느 물을 쓸지
    //
    // ⚠ **거품은 물 셰이더에 붙어 있습니다.** 떼어 쓸 수가 없습니다.
    //
    //    로비의 Synty 물에도 해안 거품 기능이 있는데(`_Enable_Shore_Foam`)
    //    화면에 나오지 않았습니다. 진짜 스위치는 `_ENABLE_TERRAIN_SHORELINE`
    //    이라는 셰이더 키워드인데, 이름대로 **지형 기준**이라 배에는 안 맞습니다.
    //
    //    WaterWorks 의 `SSR_Water` 는 **물에 닿은 물체의 가장자리**를 재서
    //    거품을 만듭니다. (Edge_Distance) 그래서 선체를 따라 생깁니다.
    //
    // ⚠ WaterWorks 는 **Depth · Opaque 텍스처**가 켜져 있어야 합니다.
    //    PC_RPAsset 은 켜져 있고 Mobile_RPAsset 은 꺼져 있습니다.
    //    지금 품질 레벨은 PC 라 괜찮지만, 모바일로 내릴 일이 생기면 거기도 켜야 합니다.
    //
    // ⚠ **로비와 바다 모양이 달라집니다.** 로비는 Synty 물을 그대로 씁니다.
    //    같아야 한다면 로비도 바꾸거나, 여기를 Synty 로 되돌리면 됩니다.
    // ------------------------------------------------------------

    // ⚠ **WaterWorks 를 안 씁니다. 로비와 같은 물을 씁니다.**
    //
    //    WaterWorks 를 가져온 이유는 파도 하나였습니다. 그 파도를 뺐으니
    //    남을 이유가 없습니다. 로비가 쓰는 Synty 물을 그대로 베껴 씁니다.
    //    바다 색이 로비와 어긋나지 않는 것이 이 게임에 더 중요합니다.
    private const bool UseWaterWorks = false;

    // ------------------------------------------------------------
    // ⛔ **물결은 껐습니다. 다시 켜지 마세요.**
    //
    //    파도 · 잔결 · 거품을 넣어보려고 한참 매달렸는데, 어느 조합으로도
    //    **멀리서 지글거리는 것**을 못 없앴습니다. 화면에서는 물결이 아니라
    //    렌더링 오류처럼 보입니다.
    //
    //    뿌리는 셰이더에 있습니다. 꼭짓점만 밀고 **법선을 다시 계산하지 않아서**
    //    (`VertexDescription.Normal` 이 안 연결됨) 파도가 빛을 안 받습니다.
    //    그래서 파도를 보이게 하려면 잔결(`_NormalStrength`)을 올려야 하는데,
    //    올리면 멀리서 무늬가 한 픽셀보다 작아져 반짝임이 지글거립니다.
    //    **둘 다 만족하는 값이 없습니다.**
    //
    //    그래서 잔잔한 물로 둡니다. 배가 나아가는 느낌은 물 무늬가 아니라
    //    수평선의 섬과 흘러오는 장애물이 냅니다.
    //
    //    되살리려면 셰이더 그래프에서 법선을 먼저 이어야 합니다.
    //    그 전에는 값만 만져서는 안 됩니다. 안 해봐서 모르는 게 아니라
    //    해보고 안 된 것입니다. (SHIPCOOP.md 5장)
    // ------------------------------------------------------------

    private const bool UseWaves = false;

    private const string WaterWorksPath = "Assets/WaterWorks/Materials/SSR_Water.mat";

    /// <summary>로비가 쓰는 물. WaterWorks 를 안 쓸 때 여기서 베껴 온다.</summary>
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

    // ------------------------------------------------------------
    // 바다는 **한 장**입니다. 여러 장으로 나누면 안 됩니다.
    //
    // ⚠ 물 무늬는 판에 그려지고(오브젝트 좌표) 판이 뒤로 흐릅니다.
    //    (`WSUV_Water` 서브그래프. 5장 "배가 나아가 보이게" 참고)
    //
    //    그래서 판을 여러 장으로 나누면 **장마다 같은 무늬가 처음부터** 그려집니다.
    //    이음새마다 파도가 꺾이고, 그 금이 배 쪽으로 밀려옵니다.
    //    한 장이면 이음새가 아예 없습니다.
    //
    // ⚠ 그리고 **되돌리지 않습니다.** 항해 거리보다 판이 훨씬 길어서
    //    끝까지 가도 판이 모자라지 않습니다. 되돌리는 순간 무늬가 튑니다.
    // ------------------------------------------------------------

    /// <summary>
    /// 판의 좌우 폭 (m).
    ///
    /// ⚠ **하늘돔보다 넓어야 합니다.** (`SkyRadius` 480)
    ///    400 으로 뒀더니 옆을 볼 때 200m 에서 바다가 끝나고 **그 너머로 돔
    ///    밑동이 보였습니다.** 수평선에 흰 띠가 생기고 바다가 잘려 보입니다.
    ///    1150 이면 좌우 575m 라 돔 안쪽을 덮습니다.
    /// </summary>
    private const float SeaWidth = 1150f;

    /// <summary>
    /// 배 뒤로 이만큼부터 깐다.
    ///
    /// ⚠ 이것도 하늘돔 반지름보다 뒤여야 합니다. 뒤를 돌아보면 바다가 끝나 있습니다.
    /// </summary>
    private const float SeaBackZ = -550f;

    /// <summary>배 앞으로 이만큼까지 보인다. 목적지 섬(400m)보다 멀어야 한다.</summary>
    private const float SeaFrontZ = 480f;

    /// <summary>
    /// 판의 앞뒤 길이 (m).
    ///
    /// 보이는 만큼(`SeaBackZ`~`SeaFrontZ` = 1030m) + 항해 거리 + 여유.
    /// `ShipVoyage.totalDistance` 를 늘리면 여기도 같이 늘려야 합니다.
    /// 모자라면 항해 끝에 **배 앞의 바다가 사라집니다.**
    /// 도구가 끝날 때 덮는지 계산해서 로그에 적어줍니다.
    /// </summary>
    private const float SeaLength = 1750f;

    /// <summary>
    /// 물 판을 이만큼 돌린다. **물이 흐르는 방향을 정하는 값입니다.**
    ///
    /// 무늬가 흐르는 방향은 셰이더 그래프에 박혀 있어서 값으로 못 바꿉니다.
    /// 대신 무늬가 판을 따라 도니까, 판을 돌려서 방향을 맞춥니다.
    ///
    ///   0    받아온 그대로. 오른쪽에서 왼쪽으로 흐른다 (배가 옆으로 미끄러져 보임)
    ///   -90  뱃머리 쪽에서 고물 쪽으로 흐른다 ← 이게 "배가 앞으로 간다"
    ///   90   반대. 물이 배 쪽으로 밀려온다 (뒤로 가는 것처럼 보임)
    ///
    /// ⚠ -90 과 90 중 어느 쪽인지는 **눈으로 보고 정해야 합니다.** 돌려보고
    ///    물이 배 뒤쪽으로 흘러가면 맞고, 앞으로 밀려오면 부호를 뒤집으세요.
    /// </summary>
    private const float SeaFlowYaw = -90f;


    /// <summary>물거품 줄이 되돌아오는 간격 (m). 지금은 물거품을 안 씁니다.</summary>
    private const float FoamLoopLength = 60f;

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

    /// <summary>앞계단이 시작되는 z. 앞갑판으로 올라가는 경사로의 아래 끝.</summary>
    private static float ForeStairZ => MidFrontZ - ForeStairRun;

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
        BuildAftBulkhead(root, log);
        CoverTheHold(root, log);
        MoveSea(log);
        MatchLobbySky(log);
        BuildSky(log);
        MoveStations(log);
        EnsureCargoVisuals(log);
        DressBoxes(log);
        DressDumps(log);
        DressHullDamagePrefab(log);
        MovePlayers(log);
        AttachCharacters(log);
        SizeHeldVisual(log);
        SetUpCarryPose(log);
        SetUpPortraits(log);
        SetUpReefRocks(log);
        ShipCoopSeaProps.SetUp(log);

        EditorSceneManager.MarkAllScenesDirty();

        log.AppendLine();
        log.AppendLine("씬을 저장해야 남습니다. (Ctrl+S)");

        Debug.Log(log.ToString());
    }

    /// <summary>배치 도구가 다루는 씬. 네트워크 씬(ShipCoop.unity)은 이 씬을 복사해 만든다. (ShipCoopSceneSetup)</summary>
    private const string TestScenePath = "Assets/Game/Scenes/Develop/MinHwa/ShipCoopTest.unity";

    /// <summary>
    /// 커맨드라인용. 씬을 열어 배치하고 **저장까지** 한다. 실패하면 종료 코드 1.
    ///
    /// 에디터를 열지 않고 배치를 다시 돌릴 때 쓴다.
    /// <code>
    ///   Unity.exe -batchmode -quit -projectPath &lt;경로&gt; -executeMethod ShipCoopDeckLayout.BuildFromCommandLine
    /// </code>
    /// 이어서 네트워크 씬을 다시 만들려면 <c>ShipCoopSceneSetup.BuildAllFromCommandLine</c> 을 돌린다.
    /// </summary>
    public static void BuildFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);

        Build();

        // 도구가 만들거나 고친 에셋(재질 · 설정 · 프리팹)도 함께 저장한다. 씬만 저장하면 SetDirty 한 것이 날아간다.
        AssetDatabase.SaveAssets();

        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log(saved
            ? $"[배 모델과 갑판 배치] 저장했습니다 — {TestScenePath}"
            : $"[배 모델과 갑판 배치] ⚠ 저장에 실패했습니다 — {TestScenePath}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(saved ? 0 : 1);
        }
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

        MakeShipMeshesReadable(ship, log);

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
        SpinTheWheel(ship, log);
    }

    /// <summary>
    /// 🪨 배 메시의 **Read/Write 를 켠다.**
    ///
    /// 암초(<c>Reef.MeasureShip</c>)는 선체 메시의 정점을 읽어 z 마다 실제 반폭을 잽니다. 에디터에서는
    /// 모든 메시가 읽히지만 **빌드에서는 Read/Write 가 꺼진 메시의 정점이 빈 배열**로 오고, 콘솔에
    /// "Not allowed to access vertices on mesh 'StylShip_*'" 가 찍힙니다. 반폭이 전부 0 이 되어 필요 간격이
    /// 바위 반폭만 남으니 옆구리를 긁고 지나가는 바위도 "안 닿음" 이었습니다 — **빌드(멀티)에서만 암초가
    /// 쉬웠던 이유**입니다.
    ///
    /// FBX 의 ModelImporter 설정이라 씬이 아니라 그 FBX 의 .meta 가 바뀝니다. 한 번 켜지면 다음부터는 건드리지 않습니다.
    /// </summary>
    private static void MakeShipMeshesReadable(GameObject ship, StringBuilder log)
    {
        var done = new System.Collections.Generic.HashSet<string>();

        foreach (MeshFilter part in ship.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = part.sharedMesh;

            if (mesh == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(path) || !done.Add(path))
            {
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null || importer.isReadable)
            {
                continue;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
            log.AppendLine($"  🪨 배 메시의 Read/Write 를 켰습니다 — {path} (암초가 빌드에서도 선체 폭을 읽는다)");
        }
    }

    /// <summary>
    /// 🛞 배의 조타륜이 **조타한 만큼 돌게** 만든다.
    ///
    /// 배에 달린 `Wheel` 에 `ShipCoopHelmWheel` 을 붙일 뿐입니다.
    /// 도는 규칙은 그쪽에 적혀 있습니다 — 값을 따로 계산하지 않고
    /// `HelmTask.Heading` 을 그대로 씁니다.
    /// </summary>
    private static void SpinTheWheel(GameObject ship, StringBuilder log)
    {
        Transform wheel = null;

        foreach (Transform t in ship.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Wheel")
            {
                wheel = t;
                break;
            }
        }

        if (wheel == null)
        {
            log.AppendLine("  ⚠ 배에서 조타륜(Wheel)을 못 찾았습니다.");
            return;
        }

        ShipCoopHelmWheel spin = wheel.GetComponent<ShipCoopHelmWheel>();

        if (spin == null)
        {
            spin = Undo.AddComponent<ShipCoopHelmWheel>(wheel.gameObject);
        }

        // ⚠ **-1 입니다.** 조타 판정은 맞는데 화면에서 보는 방향이 뒤집힙니다.
        //    카메라가 배 뒤(-z)에서 보므로, +z 축 양의 회전이 화면에서는 반시계입니다.
        //    우로 꺾는데 바퀴가 왼쪽으로 도는 셈이라 부호를 뒤집습니다.
        SerializedObject so = new SerializedObject(spin);

        // 3배로 돌린다. 1배(조타한 만큼 그대로)는 너무 안 돌아서 도는지 모르겠다.
        // 진짜 배도 키를 여러 바퀴 돌리므로 이쪽이 배답기도 하다.
        so.FindProperty("degreesPerHeading").floatValue = -3f;
        so.FindProperty("spinAxis").vector3Value = Vector3.forward;
        so.ApplyModifiedProperties();

        log.AppendLine("  조타륜이 조타의 3배로 (화면 기준 같은 방향으로) 돌도록 했습니다");

        TurnTheShip(ship, log);
    }

    /// <summary>
    /// 조타한 만큼 **뱃머리가 틀어지게** 한다.
    ///
    /// 배가 옆으로 평행이동하면 안 됩니다. 제자리에서 돌아서 뱃머리와 고물이
    /// 반대로 가야 배처럼 보입니다. 자세한 이유와 한계는 `ShipCoopShipTurn` 에 적혀 있습니다.
    /// </summary>
    private static void TurnTheShip(GameObject ship, StringBuilder log)
    {
        ShipCoopShipTurn turn = ship.GetComponent<ShipCoopShipTurn>();

        if (turn == null)
        {
            turn = Undo.AddComponent<ShipCoopShipTurn>(ship);
        }

        SerializedObject so = new SerializedObject(turn);

        // ⚠ 뱃머리는 조타를 **돌리는 속도**가 아니라 배가 **옆으로 가는 속도**를
        //    따라 틉니다. 돌리는 속도로 하면 휠에서 손을 멈추는 순간 뱃머리만
        //    펴지고 배는 3초를 더 미끄러져서, 그 구간이 전부 평행이 됩니다.
        //    (`ShipCoopShipTurn` 주석에 잰 값이 있습니다)
        so.FindProperty("degreesPerSlideSpeed").floatValue = 2.2f;
        so.FindProperty("mostDegrees").floatValue = 14f;
        so.FindProperty("slideDeadZone").floatValue = 0.15f;

        // ⛔ 조타 각도에 비례하는 성분은 **없습니다.** 넣으면 조타륜을 잡고 있는
        //    내내 배가 비스듬한 채로 굳습니다. 시간이 지나면 일자여야 합니다.

        // ⚠ **축은 고정이 아니라 고물에서 뱃머리 쪽으로 미끄러집니다.**
        //
        //    고정해두면 고물이 가장 긴 팔로 휘둘려서 **뱃머리보다 먼저, 10배 크게**
        //    움직입니다. 고물이 따라가는 게 아니라 앞장서 버립니다.
        //    돌기 시작할 때 축을 고물에 두면 뱃머리만 먼저 나갑니다.
        so.FindProperty("sternPivotZ").floatValue = ShipSternZ;
        so.FindProperty("pivotZ").floatValue = ShipBowZ - (ShipBowZ - ShipSternZ) / 3f;

        // 고물이 따라오는 속도. 키우면 더 늦게 따라온다.
        so.FindProperty("sternFollowSeconds").floatValue = 1.2f;

        // ⚠ 기울기는 **뱃머리가 튼 만큼만** 따라갑니다. 혼자 기울 수 없습니다.
        //    조타 각도로 몰았더니 배는 일자인데 기울기만 남았습니다.
        so.FindProperty("bankDegrees").floatValue = 3f;

        // ⚠ 0.8 이면 뱃머리가 옆이동보다 0.8초 늦어서 끝에 평행 구간이 남습니다.
        so.FindProperty("followSeconds").floatValue = 0.4f;

        // ⚠ **같이 돌 것들을 모은다.** 배만 돌리면 갑판 · 자리 · 사람이
        //    제자리에 남아서 사람이 허공을 걷습니다.
        var carried = new System.Collections.Generic.List<Transform>();

        carried.Add(ship.transform);

        Transform decks = GameObject.Find(RootName) != null
            ? GameObject.Find(RootName).transform
            : null;

        if (decks != null)
        {
            carried.Add(decks);   // 걷는 바닥 · 경사로 · 벽 · 선창 바닥
        }

        // 작업 자리와 물건들. 배 밖(씬 루트)에 놓여 있어서 따로 챙겨야 한다.
        foreach (TaskBase t in Object.FindObjectsByType<TaskBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            carried.Add(t.transform);
        }

        foreach (AmmoBox t in Object.FindObjectsByType<AmmoBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            carried.Add(t.transform);
        }

        foreach (WaterDumpPoint t in Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            carried.Add(t.transform);
        }

        // ------------------------------------------------------------
        // ⚠ **파손 지점이 생기는 자리도 챙겨야 합니다.**
        //
        //    안 챙기면 두 가지가 깨집니다.
        //      ① 자리 자체가 제자리에 남아서, 배가 돌수록 구멍이 배 밖에 뚫린다
        //      ② 거기 생긴 빨간 수리 큐브가 **갑판 위를 미끄러져 다닌다**
        //
        //    `HullDamage.Spawn` 이 파손 지점을 **그 자리의 부모 밑에** 넣으므로,
        //    부모만 여기 넣어두면 게임 중에 생긴 것도 저절로 같이 돕니다.
        //    이 목록은 에디터에서 만들어지니 실행 중에 생긴 것은 넣을 수가 없습니다.
        // ------------------------------------------------------------

        foreach (HullDamage hull in Object.FindObjectsByType<HullDamage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty spots = new SerializedObject(hull).FindProperty("spawnPoints");

            for (int i = 0; i < spots.arraySize; i++)
            {
                Transform spot = spots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                if (spot == null)
                {
                    continue;
                }

                // 부모를 넣는다. 자리 하나하나가 아니라 묶음째 돌아야 서로 안 어긋난다.
                Transform group = spot.parent != null ? spot.parent : spot;

                if (!carried.Contains(group))
                {
                    carried.Add(group);
                }
            }
        }

        foreach (TaskWorker t in Object.FindObjectsByType<TaskWorker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            carried.Add(t.transform);
        }

        SerializedProperty list = so.FindProperty("carried");
        list.arraySize = carried.Count;

        for (int i = 0; i < carried.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = carried[i];
        }

        so.ApplyModifiedProperties();

        log.AppendLine($"  조타하면 뱃머리가 좌우로 틀어지게 했습니다 " +
                       $"(꺾는 순간 최대 14도, 기울기 3도, 시간이 지나면 일자로 돌아옴, " +
                       $"축 z {ShipBowZ - (ShipBowZ - ShipSternZ) / 3f:F1}, 같이 도는 것 {carried.Count}개)");
    }

    // ------------------------------------------------------------
    // 선창 바닥 — **선체 안으로 바다가 비치는 것**을 막는다
    //
    // ⚠ 중간갑판 한가운데 격자창(화물칸 뚜껑)은 아래가 비칩니다. 뱃머리 · 배 뒤도 위가 트여 있습니다.
    //    그 아래에 아무것도 없으면 **바다 판이 그대로 보여서, 배 안에 물이
    //    차 있는 것처럼** 보입니다. 침수 게이지가 0인데도요.
    //
    //    진짜 배에는 그 아래에 선창 바닥이 있습니다. 모델에는 없으니 깔아줍니다.
    //    배 안쪽이라 밖에서는 선체에 가려 안 보입니다.
    //
    // ⚠ **네모 상자가 아니라 선체 모양대로 잰 판입니다.** (ShipCoopHoldFloorMesh)
    //    폭 10.3 · 길이 35 상자는 뱃머리 쪽 9.5m 와 배 뒤 2.2m, 양옆 0.45m 를 못 덮어 **배 앞부분에 물이 찬 것처럼**
    //    보였습니다. 선체보다 크게 하면 좁아지는 뱃머리에서 배 밖으로 튀어나옵니다. 그래서 그 높이에서 레이로 선체를
    //    재서 폭을 맞춥니다. 선체를 못 재면(배가 없거나 콜라이더가 꺼짐) 예전 상자로 돌아갑니다.
    // ------------------------------------------------------------

    private const string HoldFloorName = "Hold_Floor";

    /// <summary>선창 바닥 높이. 갑판(-3.49)보다 한참 아래, 흘수선(-8.0)보다는 위.</summary>
    private const float HoldFloorY = -6.2f;

    private static void CoverTheHold(Transform root, StringBuilder log)
    {
        GameObject ship = GameObject.Find(ShipName);

        if (ship == null)
        {
            return;
        }

        // 배와 같은 나무색을 쓴다. 따로 재질을 만들면 혼자 겉돈다.
        Material wood = FindShipWood();

        Transform floor = FindOrCreateBox(root, HoldFloorName);
        Undo.RecordObject(floor, "배 모델과 갑판 배치");
        floor.localRotation = Quaternion.identity;

        // 선체를 재서 그 모양대로 만든다. 못 재면 예전 상자.
        Mesh shaped = ShipCoopHoldFloorMesh.CreateOrOverwrite(ship, HoldFloorY, CenterX, out ShipCoopHoldFloorMesh.Report measured);
        MeshFilter filter = floor.GetComponent<MeshFilter>();

        if (shaped != null && filter != null)
        {
            // 메시 점이 월드 좌표다. 루트는 원점에 있으니(FindOrCreateRoot) 자리 · 배율을 1 로 둔다.
            Undo.RecordObject(filter, "배 모델과 갑판 배치");
            filter.sharedMesh = shaped;
            floor.localPosition = Vector3.zero;
            floor.localScale = Vector3.one;
        }
        else
        {
            floor.localScale = new Vector3(DeckWidth, DeckThickness, AftBackZ * -1f + ForeFrontZ);
            floor.localPosition = new Vector3(CenterX, HoldFloorY, (AftBackZ + ForeFrontZ) * 0.5f);
            log.AppendLine("  ⚠ 선체를 레이로 못 재서 선창 바닥을 예전 상자로 깝니다 (뱃머리 · 배 뒤가 안 가려집니다)");
        }

        if (wood != null)
        {
            Renderer draw = floor.GetComponent<Renderer>();
            Undo.RecordObject(draw, "배 모델과 갑판 배치");
            draw.sharedMaterial = wood;
            draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            draw.enabled = true;
        }

        // 걸어다닐 데가 아니다. 콜라이더를 끄지 않으면 발 높이를 재는 레이가
        // 여기에 걸릴 수 있다.
        Collider bump = floor.GetComponent<Collider>();

        if (bump != null && bump.enabled)
        {
            Undo.RecordObject(bump, "배 모델과 갑판 배치");
            bump.enabled = false;
        }

        log.AppendLine(shaped != null
            ? $"  선창 바닥을 y {HoldFloorY:F1} 에 선체 모양대로 깔았습니다 — z {measured.MinZ:F1}~{measured.MaxZ:F1}, " +
              $"{measured.Slices}구간(그중 {measured.Filled}칸은 레이가 빗나가 앞뒤에서 이어 그림), " +
              $"가장 넓은 곳 {measured.Widest:F1}m ({shaped.name}). 선체 안으로 바다가 비치는 것을 막음"
            : $"  선창 바닥을 y {HoldFloorY:F1} 에 상자로 깔았습니다. (격자창 아래로 바다가 비치는 것을 막음)");
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

        // ⚠ 뒷계단 사이에 놓인 장식입니다. **계단 통행을 막습니다.**
        //    밑에 깔린 `Pallet` 은 납작해서(높이 0.24m) 걸리지 않으니 남겨둡니다.
        "Barrel", "Box",
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

    // ------------------------------------------------------------
    // 뒷계단 사이를 막는 격벽 — 뒷갑판 밑으로 들어가 바다에 빠지는 길
    // ------------------------------------------------------------

    private const string BulkheadName = "Wall_AftBulkhead";
    private const string UnderAftFloorName = "Floor_UnderAft";
    private const float BulkheadThickness = 0.3f;

    /// <summary>
    /// 격벽을 뒷갑판 가장자리(<see cref="MidBackZ"/>)에서 뱃머리 쪽으로 이만큼 뺀다.
    ///
    /// 가장자리에 딱 붙이면 배 모델의 선미루 앞벽과 겹쳐 무늬가 깨지고, 움푹한 곳에 들어가
    /// 잘 보이지 않았다. 0.5m 빼면 겹침에서 벗어나 판자 무늬가 제대로 보이고, 중간갑판은 거의 그대로다.
    /// (1.5m 로 해 봤더니 너무 앞으로 나와 보여 0.5m 로 줄였다)
    /// 계단이 중간갑판에 닿는 끝(<see cref="AftStairRun"/> = 4.0)까지 뺄 수 있지만
    /// 그만큼 중간갑판이 좁아진다.
    /// </summary>
    private const float BulkheadForward = 0.5f;

    /// <summary>
    /// 격벽 재질 — **선체 안쪽 판자 무늬.**
    ///
    /// 선체 재질(<c>M_Ship_Hull_01</c>)의 텍스처는 여러 부위가 한 장에 모인 아틀라스라
    /// 그대로 입히면 창문·쇠테까지 다 나온다. 그래서 선체 재질을 복사한 뒤 타일링·오프셋으로
    /// **창문 없는 가로 판자 구역**(<see cref="BulkheadUvRect"/>)만 보이게 한다.
    /// 갑판 바닥 무늬(DeckMid)와 단색도 해 봤지만, 벽은 벽 무늬여야 배와 한 덩어리로 읽혔다.
    /// </summary>
    private const string HullMaterialPath = "Assets/Game/Prefabs/PirateShip/M_Ship_Hull_01.mat";
    private const string BulkheadMaterialPath = "Assets/Game/Art/Materials/ShipCoop/BulkheadPlanks.mat";

    /// <summary>
    /// 선체 텍스처(1024²) 안에서 창문이 없는 가로 판자 구역. UV 기준(왼쪽 아래 원점).
    /// 픽셀로는 x 195~365, y 96~164 (왼쪽 위 원점). 가로세로 비 2.5 : 1.
    /// </summary>
    private static readonly Rect BulkheadUvRect = new Rect(0.190f, 0.840f, 0.166f, 0.066f);

    /// <summary>
    /// 겉면을 위아래 몇 장으로 나눌지. 큐브 한 면에 구역이 한 번 늘어나 붙으므로,
    /// 5.8 × 4.8m 한 장이면 판자가 세로로 2배 늘어난다. 2장(각 5.8 × 2.4m, 비 2.4)이면
    /// 구역 비율(2.5)과 거의 같아 왜곡이 없다. 막는 콜라이더는 이와 별개로 한 장이다.
    /// </summary>
    private const int BulkheadSlabs = 2;

    /// <summary>
    /// 뒷계단 두 개 사이, **뒷갑판 밑으로 들어가는 입구를 보이는 나무 벽으로 막는다.**
    ///
    /// 중간갑판 바닥은 z <see cref="MidBackZ"/> 에서 끝나고 뒷갑판 바닥은 그보다 4.5m 위다.
    /// 두 계단 사이(폭 5.8m)로 걸어 들어가면 밟을 것이 없어 선체 안으로 떨어지고,
    /// 선체 안에는 바닥이 없어 **바다까지 떨어진다.** 바다에 빠지면 돌아올 길이 없다. (BuildWalls 참고)
    ///
    /// 보이지 않는 벽으로만 막으면 "왜 안 가지지?" 가 된다. 배와 같은 나무 재질을 입혀
    /// 선미루 격벽처럼 보이게 한다. 배 모델의 이 자리에는 원래 상자·통 장식이 있었지만
    /// 계단 통행을 막아 꺼 두었다. (<see cref="RemoveFromShip"/>)
    ///
    /// 그래도 어떤 틈으로든 들어간 사람이 바다로 떨어지지는 않도록,
    /// 뒷갑판 밑 전체에 **보이지 않는 바닥**도 한 장 깐다. 걸어서 돌아올 수 있으면 벌이 아니다.
    /// </summary>
    private static void BuildAftBulkhead(Transform root, StringBuilder log)
    {
        // 계단 안쪽 가장자리 사이를 막는다. 캡슐 반지름이 0.5m 라 틈이 몇 cm 남아도 못 지나간다.
        float innerHalf = StairOffsetX - StairWidth * 0.5f;
        float width = innerHalf * 2f;

        // 중간갑판 바닥 밑에서 뒷갑판 바닥 높이까지. 아래로 갑판 두께만큼 더 내려 발밑 틈을 없앤다.
        float bottom = MidSurfaceY - DeckThickness;
        float top = AftSurfaceY;

        float wallZ = MidBackZ + BulkheadForward;

        // 예전 버전 정리 — 보이지 않는 큐브 한 장 + 콜라이더 없는 겉면 그룹.
        Transform oldWall = root.Find(BulkheadName);
        if (oldWall != null && oldWall.GetComponent<Collider>() != null)
        {
            Undo.DestroyObjectImmediate(oldWall.gameObject);
        }
        RemoveChild(root, BulkheadName + "_Skin");

        // 격벽은 위아래 몇 장의 판으로 만든다. **판마다 콜라이더를 둔다.** 막는 것도 이 판들이 한다.
        //
        // ⚠ 콜라이더가 있어야 카메라가 가릴 때 숨겨 준다. ShipCoopCamera 는 카메라와 사람 사이에
        //    걸린 **콜라이더**를 찾아 그 렌더러를 끄기 때문에, 콜라이더 없는 판은 화면을 통째로 막았다.
        //    이제 사람 뒤에서 뱃머리를 볼 때는 사라지고, 카메라를 돌려 고물 쪽을 보면 다시 나온다.
        Material planks = GetOrCreateBulkheadPlanks(log);
        Transform skin = FindOrCreateGroup(root, BulkheadName);
        float slabHeight = (top - bottom) / BulkheadSlabs;

        for (int i = 0; i < BulkheadSlabs; i++)
        {
            Transform slab = FindOrCreateBox(skin, $"Slab_{i}");
            slab.localScale = new Vector3(width, slabHeight, BulkheadThickness);
            slab.localPosition = new Vector3(CenterX, bottom + slabHeight * (i + 0.5f), wallZ);
            slab.localRotation = Quaternion.identity;

            Renderer draw = slab.GetComponent<Renderer>();
            if (planks != null && draw != null)
            {
                Undo.RecordObject(draw, "배 모델과 갑판 배치");
                draw.sharedMaterial = planks;
                draw.enabled = true;
            }
        }

        // 예전에 slab 수가 더 많았다면 남는 것을 치운다.
        for (int i = skin.childCount - 1; i >= BulkheadSlabs; i--)
        {
            Undo.DestroyObjectImmediate(skin.GetChild(i).gameObject);
        }

        // 카메라가 벽 뒤(고물 쪽)에서 벽 앞의 사람을 볼 때 벽을 감추는 규칙. (ShipCoopBulkhead 참고)
        if (skin.GetComponent<ShipCoopBulkhead>() == null)
        {
            Undo.AddComponent<ShipCoopBulkhead>(skin.gameObject);
        }

        if (planks == null)
        {
            log.AppendLine("  ⚠ 선체 재질을 못 찾아 격벽이 기본 회색으로 남습니다.");
        }

        // 안전 바닥. 뒷갑판 밑 전체를 중간갑판 높이로 덮는다. 보이지 않고 밟히기만 한다.
        Transform floor = FindOrCreateBox(root, UnderAftFloorName);
        floor.localScale = new Vector3(DeckWidth, DeckThickness, AftLength);
        floor.localPosition = new Vector3(CenterX, MidSurfaceY - DeckThickness * 0.5f, AftCenterZ);
        floor.localRotation = Quaternion.identity;
        HideButKeepCollider(floor);

        log.AppendLine($"  격벽 뒷계단 사이 x {CenterX - width * 0.5f:F1} ~ {CenterX + width * 0.5f:F1}, " +
                       $"y {bottom:F1} ~ {top:F1}, z {wallZ:F1}  (선체 판자 무늬 {BulkheadSlabs}장) · 뒷갑판 밑 안전 바닥 y {MidSurfaceY:F2}");
    }

    /// <summary>
    /// 선체 재질을 복사해 창문 없는 판자 구역만 보이게 타일링·오프셋을 건 재질.
    /// 없으면 만들고, 있으면 구역 값만 다시 맞춘다. (상수를 고치고 다시 돌려도 반영되게)
    /// </summary>
    private static Material GetOrCreateBulkheadPlanks(StringBuilder log)
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(BulkheadMaterialPath);

        if (made == null)
        {
            Material hull = AssetDatabase.LoadAssetAtPath<Material>(HullMaterialPath);

            if (hull == null)
            {
                log.AppendLine($"  ⚠ 선체 재질을 못 찾음: {HullMaterialPath}");
                return null;
            }

            made = new Material(hull);
            AssetDatabase.CreateAsset(made, BulkheadMaterialPath);
            log.AppendLine($"  격벽 재질을 선체 재질에서 복사해 만들었습니다 — {BulkheadMaterialPath}");
        }

        // URP/Lit 의 _BaseMap 이 [MainTexture] 라 mainTextureScale/Offset 이 그대로 먹는다.
        made.mainTextureScale = new Vector2(BulkheadUvRect.width, BulkheadUvRect.height);
        made.mainTextureOffset = new Vector2(BulkheadUvRect.x, BulkheadUvRect.y);
        EditorUtility.SetDirty(made);

        return made;
    }

    /// <summary>배와 같은 나무 판자 재질. 중간갑판(DeckMid) 것을 그대로 쓴다. 따로 만들면 혼자 겉돈다.</summary>
    private static Material FindShipWood()
    {
        GameObject ship = GameObject.Find(ShipName);

        if (ship == null)
        {
            return null;
        }

        foreach (Renderer r in ship.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "DeckMid" && r.sharedMaterial != null)
            {
                return r.sharedMaterial;
            }
        }

        return null;
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

        // 뒷갑판은 13 / 7 이 기본이었는데 멀어 보여서 **20% 당겼습니다.**
        // 뒤에 걸릴 것이 선체(꼭대기 1.92)와 난간(2.93)뿐이라 여유가 많습니다.
        MakeDeck(root, "Deck_Aft", AftCenterZ, AftLength, AftSurfaceY, "뒷갑판",
                 new Vector4(10.4f, 5.6f, 1.5f, 4.8f));

        // 중간갑판에 서면 **뒷갑판이 통째로 눈앞을 가로막습니다.**
        // 카메라를 높여도 그 사이에 있으니 소용이 없어서, 그 층에 있는 동안 감춥니다.
        //
        // ⚠ 가리는 것을 피하려고 15 / 8.5 까지 멀리 올려뒀는데, 감추기 시작한
        //    뒤로는 필요가 없어졌습니다. 멀면 **캐릭터가 작아져서** 발이 닿았는지
        //    읽기 어렵고, 그 층만 다른 게임처럼 보입니다.
        //
        // 얼마나 당길 수 있나 — **뒤에 있는 것에 걸립니다.**
        //
        //   9 면 카메라가 z -6.9 에 선다. 뒤쪽 구조물(조타륜·선실·뒷갑판 바닥)은
        //   이 층에서 감추므로 통과해도 된다. 뒷갑판 **난간도 감춘다** —
        //   내가 걸어다닐 데가 아니라서 지킬 이유가 없다. (OwnRailing)
        //
        //   높이 6 이면 y 2.51 로, 계단(꼭대기 1.20)과 선체(1.92) 위를 지난다.
        //   여기서 더 당기거나 낮추면 계단을 뚫는다.
        //
        // 15 → 9 로 당겼더니 훅 들어온 느낌이라 **10분의 1 만큼 도로 물렸습니다.**
        MakeDeck(root, "Deck_Main", MidCenterZ, MidLength, MidSurfaceY, "중간갑판",
                 new Vector4(9.6f, 6.25f, 1.5f, 4.2f));

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

        AddPartners(ship, found);
        AddAlsoHide(ship, deck, found);

        list.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
    }

    // ------------------------------------------------------------
    // 같이 숨어야 하는 짝
    //
    // ⚠ **조타 기둥만 사라지고 바퀴가 허공에 떴습니다.**
    //
    //    가림 판정은 오브젝트마다 따로 합니다. `WheelStand` 는 크고 넓어서
    //    (4.5 × 2.2 × 3.9m) 레이에 잘 걸리는데, `Wheel` 은 얇아서
    //    (2.1 × 2.1 × 0.45m) 같은 각도에서도 안 걸릴 때가 있습니다.
    //    그래서 기둥만 사라지고 바퀴가 남습니다.
    //
    //    눈으로는 **한 물건**이므로 같이 숨어야 합니다.
    // ------------------------------------------------------------

    private static readonly string[,] HideTogether =
    {
        { "WheelStand", "Wheel" },
    };


    // ------------------------------------------------------------
    // 층마다 **무조건 더 감추는 것**
    //
    // ⚠ 자동 판정은 **카메라와 그 층 사이**를 막는 것만 찾습니다.
    //    그런데 조타석에서는 그 너머, **수평선을 가리는 것**이 문제입니다.
    //
    //    돛(`SailMid_01`)이 화면 한가운데를 덮어서 **앞바다가 안 보입니다.**
    //    암초는 멀수록 화면 중앙으로 모이므로 옆으로 빼도 소용이 없습니다.
    //    조타를 보는 사람이 앞을 못 보면 피할 방법 자체가 없습니다.
    //
    //    앞갑판은 이미 같은 이유로 돛을 감추고 있었습니다. 조타석도 같습니다.
    // ------------------------------------------------------------

    private static readonly (string Deck, string[] Hide)[] AlsoHide =
    {
        ("뒷갑판", new[] { "SailMid_01" }),
    };

    // 그 층에서 무조건 감출 것들을 목록에 더한다.
    private static void AddAlsoHide(GameObject ship, ShipDeck deck,
                                    System.Collections.Generic.List<Renderer> found)
    {
        for (int i = 0; i < AlsoHide.Length; i++)
        {
            if (deck.DeckName != AlsoHide[i].Deck)
            {
                continue;
            }

            foreach (string want in AlsoHide[i].Hide)
            {
                foreach (Transform t in ship.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != want)
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
        }
    }
    // 숨기기로 한 것의 짝도 같이 넣는다.
    private static void AddPartners(GameObject ship, System.Collections.Generic.List<Renderer> found)
    {
        for (int pair = 0; pair < HideTogether.GetLength(0); pair++)
        {
            string one = HideTogether[pair, 0];
            string other = HideTogether[pair, 1];

            bool hasOne = false;
            bool hasOther = false;

            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].name == one) hasOne = true;
                if (found[i].name == other) hasOther = true;
            }

            if (hasOne == hasOther)
            {
                continue;
            }

            string missing = hasOne ? other : one;

            foreach (Transform t in ship.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != missing)
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
    /// 난간은 여기에 없습니다. **자기 층 난간만** 따로 지킵니다. (OwnRailing)
    /// </summary>
    private static readonly string[] NeverHide =
    {
        "Hull",
        "StairsUpper", "StairsFore", "StairsLower",
    };

    /// <summary>
    /// 그 층에 서 있을 때 **지켜야 하는 난간.**
    ///
    /// 난간은 갑판이 어디서 끝나는지 알려주는 표시입니다. 떨어지는 것은 보이지 않는
    /// 벽이 막고 있어서, 난간이 없으면 **어디까지가 바닥인지 눈으로 읽을 수가 없습니다.**
    ///
    /// 다만 그건 **자기가 서 있는 층** 이야기입니다. 중간갑판에 있는데 뒷갑판 난간이
    /// 눈앞을 가리면, 그건 지킬 이유가 없습니다. 내가 걸어다닐 데가 아니니까요.
    ///
    /// ⚠ 한 층의 난간이 **메시 하나**라 앞쪽만 감출 수가 없습니다.
    ///    그래서 층 단위로 "지킨다 / 감춘다" 를 정합니다.
    /// </summary>
    private static string OwnRailing(string deckName)
    {
        if (deckName == "뒷갑판") { return "RailingAft"; }
        if (deckName == "중간갑판") { return "RailingMid"; }
        if (deckName == "앞갑판") { return "RailingFore"; }

        return null;
    }

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

            // 자기 층 난간은 지킨다. 다른 층 난간은 가리면 감춘다.
            if (t.name == OwnRailing(deck.DeckName))
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
    /// 🪣 뱃전 — **층마다 좌 · 우현 하나씩.** 배의 모든 난간에서 버릴 수 있어야 한다.
    ///
    /// 한쪽 난간만 두면 반대편에서 퍼낸 물을 배를 가로질러 날라야 해서, 가까운 난간에
    /// 바로 버린다는 실제 감각과도 어긋나고 왕복이 길어집니다. 4장은 **왕복 4초**를 전제로
    /// 침수 속도(초당 2.5%)를 정했으므로, 반대편까지 나르게 두면 아무리 퍼내도 안 줄어듭니다.
    ///
    /// 좌현(-x)은 우현(+x) 자리를 그대로 뒤집은 자리입니다. 계단(x <see cref="StairOffsetX"/>)과
    /// 겹치지 않게, 계단이 없는 각 층 가운데(z)에 둡니다.
    /// </summary>
    private static void PlaceDumps(StringBuilder log)
    {
        float starboard = CenterX + 4.0f;
        float port = CenterX - 4.0f;

        Vector3[] spots =
        {
            new Vector3(starboard, MidSurfaceY + DumpLift, MidCenterZ),
            new Vector3(starboard, AftSurfaceY + DumpLift, AftCenterZ),
            new Vector3(starboard, ForeSurfaceY + DumpLift, ForeCenterZ),
            new Vector3(port, MidSurfaceY + DumpLift, MidCenterZ),
            new Vector3(port, AftSurfaceY + DumpLift, AftCenterZ),
            new Vector3(port, ForeSurfaceY + DumpLift, ForeCenterZ),
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
        log.AppendLine($"    {"🪣 뱃전",-14}  층마다 좌 · 우현 1개씩 {Mathf.Min(found.Length, spots.Length)}개{extra}");
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
                    // ⚠ **앞계단 두 개 사이, 한가운데에 둡니다.**
                    //
                    //    x -1.95 에 있었더니 좌현 계단 입구를 막아서 지나갈 때마다
                    //    걸렸습니다. 계단은 x -2.79 와 +4.89 에 폭 1.9m 로 있으니,
                    //    그 사이(x -1.84 ~ 3.94)의 한가운데가 x 1.05 입니다.
                    //
                    // ⚠ 앞뒤로는 **앞갑판 벽에 붙입니다.** (MidFrontZ 에서 AmmoNookDepth 만큼 앞)
                    //    예전 자리(z 6.0)에 상자 + 화약통 한 쌍을 놓으니 뒤쪽 통이 주 돛대(z 4.4)와
                    //    10cm 차이로 붙어서 돛대 밧줄이 통을 관통해 보였습니다.
                    //    계단 사이 한가운데는 앞갑판 벽으로 막힌 막다른 자리라 벽에 붙여도
                    //    통행을 막지 않고, 돛대와는 2m 넘게 떨어집니다.
                    where = new Vector3(CenterX, MidSurfaceY + BoxLift, MidFrontZ - AmmoNookDepth);
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

            // ⚠ 큐브 크기는 1 로 둡니다. 예전에는 포탄 상자만 (2.2, 0.7, 0.9) 로 넓적하게 늘렸는데,
            //    이제 큐브 위에 배 소품을 얹으므로(DressBoxes) 그 값이 자식 모델에 곱해져 찌그러집니다.
            //    "넓적하게" 는 상자와 화약통 두 개를 나란히 놓는 것으로 대신합니다.
            if (boxes[i].transform.localScale != Vector3.one)
            {
                boxes[i].transform.localScale = Vector3.one;
            }

            log.AppendLine($"    {label,-14}  {Describe(where)}");
        }
    }

    // ------------------------------------------------------------
    // 보급 상자 — 큐브 위에 배 소품을 얹는다
    //
    // ⚠ 큐브를 지우거나 갈아끼우지 않습니다. AmmoBox 컴포넌트와 콜라이더가 붙어 있고
    //    PlaceBoxes 가 FindObjectsByType<AmmoBox> 로 찾습니다. 캐릭터를 큐브 위에
    //    씌운 것과 같은 방식으로 — 큐브는 두고 **자식으로 모델을 얹고 겉만 감춥니다.**
    //    (AttachCharacters, SHIPCOOP.md 4장 "캐릭터는 큐브 위에 씌웁니다")
    //
    //    세 상자 모두 Synty PolygonGeneric 소품입니다. 같은 아틀라스(Generic_01_A)라 서로 색이 맞고,
    //    URP 에서 정상이라 **자기 재질을 그대로 둡니다.** (Synty/PolygonGeneric 은 ASSETS.md 의 Nature Biomes 줄이 덮습니다)
    //    문법은 하나입니다 — **안에 든 것을 보여 준다.** 포탄 궤짝은 뚜껑을 열어, 자재 궤짝은 격자 사이로,
    //    물 자리는 웅덩이 그 자체로.
    //
    //    처음에는 StylShip(배와 같은 FBX) 상자 · 통 · 팰릿을 얹었는데, 뚜껑이 없고 안이 안 보여 바꿨습니다.
    //    StylShip 재질은 Built-in RP 라 URP 에서 마젠타로 나오니 다시 쓸 때는 M_Ship_Props_01 로 덮어써야 합니다.
    // ------------------------------------------------------------

    private const string ChestPath = "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Chest_01.prefab";

    /// <summary>궤짝 프리팹 안의 뚜껑. 원점이 힌지에 있어 로컬 X 로 -100° 돌리면 열린다. (SupplyChestLid)</summary>
    private const string ChestLidName = "SM_Gen_Prop_Chest_01_Lid_01";

    /// <summary>
    /// 포탄 궤짝 배율. 원본은 0.97 × 0.76 × 0.60m(닫힘). 2배면 가로 1.9m, 닫힘 높이 1.5m 로
    /// 키 3m 캐릭터의 허리쯤이고, 앞계단 두 개 사이 5.8m 틈을 혼자 채운다.
    /// 예전에 큐브를 2.2m 로 넓적하게 늘린 이유(작은 상자는 그 틈에서 파묻혀 보임)를 이 배율이 대신한다.
    /// 열리면 뚜껑까지 높이 2.06m · 깊이 1.94m 가 된다.
    /// </summary>
    private const float ChestScale = 2.0f;

    /// <summary>궤짝의 y 회전. 열린 뚜껑이 고물(-z)을 향하는 값. 넣어 보고 정했다. (0 이면 반대로 열렸다)</summary>
    private const float ChestYaw = 180f;

    // ------------------------------------------------------------
    // 🪵 자재 상자 — 옆·위가 뚫린 격자 궤짝 안에 판자 묶음을 세운다
    //
    //    포탄 궤짝(뚜껑을 열면 포탄이 보인다)과 같은 문법이다: **안에 든 것을 보여 준다.**
    //    격자 궤짝은 뚜껑이 없어 늘 보이므로 SupplyChestLid 는 붙이지 않는다.
    //    Crate_02 · Plank_02 는 포탄 궤짝과 같은 Synty 아틀라스라 색이 맞고, 자기 재질을 그대로 쓴다.
    // ------------------------------------------------------------

    private const string PlankCratePath = "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_02.prefab";
    private const string PlankModelPath = "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Plank_02.prefab";

    /// <summary>
    /// 격자 궤짝 배율. 원본 0.90 × 0.88 × 1.37m 는 키 3m 사람에게 무릎 높이라 파묻혀 보인다.
    /// 1.3배(높이 1.14m)면 허리 아래로 보이고, 예전 팰릿(발자국 2.36m)보다는 작다.
    /// </summary>
    private const float PlankCrateScale = 1.3f;

    // ------------------------------------------------------------
    // 🪵 궤짝은 원본 메시 그대로 — 판자는 **위에 쌓는다**
    //
    //    한때 판자를 궤짝 **안에 세웠다.** Crate_02 는 옆이 격자여도 윗면까지 한 덩어리 메시라 판자가
    //    뚜껑을 뚫고 나와서, 메시를 읽어 윗면 삼각형을 뺀 사본(…_Open.asset)을 만들어 씌웠다.
    //    그런데 윗모서리 테두리까지 같이 잘려 벽이 낮은 어정쩡한 상자가 됐고, 보는 사람도
    //    "상자 이상하다" 고 했다. 그 옆에 기대 세우는 것도 해 봤는데 그건 더 어색했다.
    //
    //    지금은 뚜껑을 그대로 두고 판자를 **위에 눕혀 쌓는다.** 부두에 자재를 쌓아 둔 그림이고,
    //    메시 사본이 없으니 도구 밖에서 관리할 파일도 없다. 예전 사본이 씬에 남아 있으면 되돌린다. (CloseTheCrate)
    // ------------------------------------------------------------

    /// <summary>자재 궤짝의 메시를 프리팹 원본으로 되돌린다. 예전 도구가 남긴 "뚜껑 없는 사본" 오버라이드를 지운다.</summary>
    private static void CloseTheCrate(Transform crate)
    {
        MeshFilter filter = crate.GetComponentInChildren<MeshFilter>(true);
        MeshFilter source = filter != null ? PrefabUtility.GetCorrespondingObjectFromSource(filter) : null;

        if (filter == null || source == null || filter.sharedMesh == source.sharedMesh)
        {
            return;
        }

        Undo.RecordObject(filter, "배 모델과 갑판 배치");
        filter.sharedMesh = source.sharedMesh;
    }

    /// <summary>
    /// 궤짝 위에 눕혀 쌓는 판자. (x 오프셋, z 오프셋, y 회전, 층) — 궤짝 중심 기준.
    ///
    /// 판자는 긴 축이 x 인 1.77m 메시(두께 0.11m)다. y 로 90° 쯤 돌려 궤짝의 긴 축(z)과 나란히 눕힌다.
    /// 궤짝(1.3배, z 1.37m)보다 길어 앞뒤로 0.2m 씩 삐져나오는데, 그게 "쌓아 둔 자재" 로 읽힌다.
    /// 아래층 둘을 나란히, 그 위 하나를 가운데에. 각도를 몇 도씩 다르게 두는 이유 — 딱 맞추면 복사해 붙인 것처럼 보인다.
    /// </summary>
    private static readonly Vector4[] PlankBundle =
    {
        new Vector4(-0.20f,  0.03f, 94f, 0f),
        new Vector4( 0.20f, -0.04f, 87f, 0f),
        new Vector4( 0.00f,  0.00f, 99f, 1f),
    };

    private static void DressPlankBundle(Transform cube, Transform crate, StringBuilder log)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankModelPath);

        if (prefab == null)
        {
            log.AppendLine($"    ⚠ 판자 프리팹을 못 찾음: {PlankModelPath}");
            return;
        }

        Transform bundle = FindOrCreateGroup(cube, "Prop_Planks");

        // 궤짝 윗면. 궤짝을 먼저 앉힌(Seat) 뒤에 재야 맞는다.
        float top = BoundsOf(crate).max.y;
        float layerHeight = 0f;

        for (int i = 0; i < PlankBundle.Length; i++)
        {
            string name = $"Plank_{i}";
            Transform plank = bundle.Find(name);

            if (plank == null)
            {
                GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                made.name = name;
                Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
                plank = made.transform;
                plank.SetParent(bundle, false);
            }

            foreach (Collider bump in plank.GetComponentsInChildren<Collider>(true))
            {
                Undo.DestroyObjectImmediate(bump);
            }

            Vector4 spec = PlankBundle[i];

            Undo.RecordObject(plank, "배 모델과 갑판 배치");
            plank.localScale = Vector3.one;
            plank.localRotation = Quaternion.Euler(0f, spec.z, 0f);

            // 밑면이 궤짝 윗면(또는 아래층 판자 위)에 닿고, 가운데가 궤짝 중심에서 조금씩 벗어난 자리에 오게.
            Seat(plank, cube, new Vector2(spec.x, spec.y), top);

            if (layerHeight <= 0f)
            {
                // 눕힌 판자 한 장의 두께. y 회전은 두께를 바꾸지 않으니 한 번만 잰다.
                layerHeight = BoundsOf(plank).size.y;
            }

            if (spec.w > 0f)
            {
                Seat(plank, cube, new Vector2(spec.x, spec.y), top + spec.w * layerHeight);
            }
        }

        for (int i = bundle.childCount - 1; i >= PlankBundle.Length; i--)
        {
            Undo.DestroyObjectImmediate(bundle.GetChild(i).gameObject);
        }
    }


    // ------------------------------------------------------------
    // 궤짝 안의 포탄 — 장식이다. 들고 다니는 포탄(DroppedCargo 의 Cargo.Ammo)과 같은 물건으로 보여야 한다.
    //   색   (0.15, 0.15, 0.18)  DroppedCargo 와 같다
    //   크기 0.7m 가 원본이지만 궤짝 안폭이 2배에서 1.9m 라 셋을 나란히 두려면 0.6m 로 줄인다
    //   개수 4 — 아랫줄 3 + 그 위 1. 재고(stock)에 따라 줄이지 않는다. 기본이 무한(-1)이라 의미가 없다
    //   뚜껑이 닫히면 가려지고 열리면 보인다. 이것이 "집었다" 는 피드백이다
    // ------------------------------------------------------------

    private const string AmmoBallMaterialPath = "Assets/Game/Art/Materials/ShipCoop/AmmoBall.mat";
    private static readonly Color AmmoBallColor = new Color(0.15f, 0.15f, 0.18f);
    private const float AmmoBallDiameter = 0.6f;

    /// <summary>궤짝 바닥판 두께만큼 띄운다. 원본 약 10cm × 2배.</summary>
    private const float AmmoBallFloor = 0.2f;

    /// <summary>포탄 자리. (x, 높이층, z) — 궤짝 중심 기준, 높이층은 0 이 바닥줄 · 1 이 그 위.</summary>
    private static readonly Vector3[] AmmoBallSpots =
    {
        new Vector3(-0.62f, 0f, 0f),
        new Vector3(0f, 0f, 0f),
        new Vector3(0.62f, 0f, 0f),
        new Vector3(0f, 1f, 0f),
    };

    /// <summary>
    /// 포탄 상자 큐브(= 상자 + 화약통 한 쌍의 가운데)를 앞갑판 벽(<see cref="MidFrontZ"/>)에서
    /// 이만큼 앞에 둔다. 한 쌍의 앞뒤 길이가 약 2.9m 라 1.75 면 상자 앞면이 벽에서 약 0.3m,
    /// 통 뒷면이 주 돛대에서 약 2.2m 떨어진다.
    /// </summary>
    private const float AmmoNookDepth = 1.75f;

    /// <summary>
    /// 보급 상자 3종의 큐브 위에 배 소품을 얹는다. PlaceBoxes 뒤에 돈다.
    ///
    /// 여러 번 돌려도 같은 결과다 — 이미 얹혀 있으면 그것을 다시 맞추고, 새로 만들지 않는다.
    /// 상자 3개 모두 중간갑판에 있어 발 높이는 <see cref="MidSurfaceY"/> 다.
    /// </summary>
    private static void DressBoxes(StringBuilder log)
    {
        AmmoBox[] boxes = Object.FindObjectsByType<AmmoBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        log.AppendLine();
        log.AppendLine("  상자 모델:");

        for (int i = 0; i < boxes.Length; i++)
        {
            Transform cube = boxes[i].transform;

            // 큐브는 두고 겉만 감춘다. 콜라이더와 AmmoBox 는 그대로 살아서 집기 판정을 맡는다.
            HideButKeepCollider(cube);

            switch (boxes[i].Kind)
            {
                case Cargo.Ammo:
                {
                    // 예전 버전(상자 + 화약통)이 남아 있으면 치운다.
                    RemoveChild(cube, "Prop_Box");
                    RemoveChild(cube, "Prop_Barrel");

                    // 궤짝은 자기 재질을 그대로 쓴다. (paint 를 넘기지 않는다)
                    Transform chest = DressWith(cube, "Prop_Chest", ChestPath, null, ChestScale, log);

                    if (chest == null)
                    {
                        break;
                    }

                    Transform lid = chest.Find(ChestLidName);

                    // ⚠ 열린 뚜껑이 **고물(-z) 쪽**을 향해야 한다. 궤짝이 앞갑판 벽에 붙어 있어서
                    //    뱃머리 쪽으로 열리면 뚜껑이 앞갑판 바닥을 뚫고, 계단에서 내려오는 사람 시야도 가린다.
                    //    힌지 위치(로컬 -z)로 추론했더니 반대로 열렸다. 실제로 넣어 보고 맞춘 값이다.
                    chest.localRotation = Quaternion.Euler(0f, ChestYaw, 0f);

                    Seat(chest, cube, Vector2.zero, MidSurfaceY);
                    DressAmmoBalls(cube, log);
                    AttachLid(cube, lid, log);

                    log.AppendLine($"    📦 포탄 상자      궤짝(뚜껑 {(lid != null ? "연결" : "⚠ 없음")}), 배율 {ChestScale:F1}, 포탄 {AmmoBallSpots.Length}개");
                    break;
                }

                case Cargo.Plank:
                {
                    // 예전 팰릿은 치우고, 격자 궤짝 + 위에 쌓은 판자로.
                    RemoveChild(cube, "Prop_Pallet");

                    Transform crate = DressWith(cube, "Prop_Crate2", PlankCratePath, null, PlankCrateScale, log);

                    if (crate == null)
                    {
                        break;
                    }

                    // 긴 축(z 1.37m)이 통로와 나란해야 통로 폭을 덜 먹는다. 우현 통로는 z 방향이라 돌리지 않는다.
                    crate.localRotation = Quaternion.identity;
                    CloseTheCrate(crate);
                    Seat(crate, cube, Vector2.zero, MidSurfaceY);
                    DressPlankBundle(cube, crate, log);

                    log.AppendLine($"    🪵 자재 상자      격자 궤짝 {PlankCrateScale:F1}배 + 위에 쌓은 판자 {PlankBundle.Length}장");
                    break;
                }

                default:
                {
                    // 🌊 물 고인 곳. 예전에 얹은 통은 치우고 갑판에 물 웅덩이를 깐다.
                    RemoveChild(cube, "Prop_Barrel");
                    DressPuddle(cube, log);
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------
    // 🌊 물 고인 곳 — 양동이 상자 자리에 깐 물 웅덩이
    //
    //    "양동이 상자" 는 이름과 달리 상자가 아니라 **갑판에 고인 바닷물**이다. 여기서 물을 퍼 담는다.
    //    바다와 같은 물 셰이더를 써야 "새어 들어온 바닷물" 로 읽힌다. 파란 판을 깔면 페인트로 보인다.
    //    콜라이더는 없다 — 집기는 부모 큐브 기준 거리다.
    //
    //    ⚠ **Quad 를 안 쓴다.** 네모는 끝이 직선으로 잘려 물이 아니라 타일처럼 보였다. 두 장을 어긋나게 겹쳐 봤지만
    //       모서리가 그대로 남았다. 그래서 가장자리가 울퉁불퉁한 둥근 메시(ShipCoopPuddleMesh, Puddle_01.asset) 한 장으로 깐다.
    //       지름은 예전 Quad 와 같은 1.6m 다. 셰이더가 가장자리에서 이상하면(거품 · 투명도) 재질을 그때 정한다 — 지금은 SeaWater.
    // ------------------------------------------------------------

    private const string PuddleMaterialPath = "Assets/Game/Art/Materials/ShipCoop/SeaWater.mat";

    /// <summary>바다 물 셰이더가 작은 판에서 이상하면(가장자리 거품 · 투명도) 이걸로 바꾼다.</summary>
    private const string PuddleFallbackMaterialPath = "Assets/Synty/PNB_Core/Materials/Water_Mat_Basic.mat";

    /// <summary>true 면 대체 재질을 쓴다. 바다 셰이더가 웅덩이에서 깨질 때 켠다.</summary>
    private const bool PuddleUseFallback = false;

    /// <summary>갑판 윗면에서 이만큼 띄운다. 같은 높이면 z-fighting 으로 깜빡인다.</summary>
    private const float PuddleLift = 0.02f;

    /// <summary>예전 Quad 웅덩이의 자식 이름. 남아 있으면 치운다.</summary>
    private static readonly string[] RetiredPuddleSheets = { "Sheet_0", "Sheet_1" };

    private static void DressPuddle(Transform cube, StringBuilder log)
    {
        string path = PuddleUseFallback ? PuddleFallbackMaterialPath : PuddleMaterialPath;
        Material water = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (water == null)
        {
            log.AppendLine($"    ⚠ 웅덩이 재질을 못 찾음: {path}");
            return;
        }

        // 메시는 매번 다시 만들어 같은 파일에 덮어쓴다. 시드가 고정이라 모양은 같고, 파일이 늘어나지 않는다.
        Mesh shape = ShipCoopPuddleMesh.CreateOrOverwrite();

        if (shape == null)
        {
            log.AppendLine("    ⚠ 웅덩이 메시를 못 만들었습니다");
            return;
        }

        Transform group = FindOrCreateGroup(cube, "Prop_Puddle");

        // 예전 Quad 두 장은 치운다.
        for (int i = 0; i < RetiredPuddleSheets.Length; i++)
        {
            RemoveChild(group, RetiredPuddleSheets[i]);
        }

        Transform puddle = group.Find("Puddle");

        if (puddle == null)
        {
            GameObject made = new GameObject("Puddle", typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
            puddle = made.transform;
            puddle.SetParent(group, false);
        }

        Undo.RecordObject(puddle, "배 모델과 갑판 배치");
        // 메시가 이미 위(+y)를 보는 xz 평면이라 돌리지 않는다. 큐브 중심은 갑판에서 BoxLift 위에 있다.
        puddle.localRotation = Quaternion.identity;
        puddle.localPosition = new Vector3(0f, PuddleLift - BoxLift, 0f);
        puddle.localScale = Vector3.one;

        MeshFilter filter = puddle.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = Undo.AddComponent<MeshFilter>(puddle.gameObject);
        }

        Undo.RecordObject(filter, "배 모델과 갑판 배치");
        filter.sharedMesh = shape;

        MeshRenderer draw = puddle.GetComponent<MeshRenderer>();
        if (draw == null)
        {
            draw = Undo.AddComponent<MeshRenderer>(puddle.gameObject);
        }

        Undo.RecordObject(draw, "배 모델과 갑판 배치");
        draw.sharedMaterial = water;
        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        draw.enabled = true;

        log.AppendLine($"    🌊 물 고인 곳      둥근 웅덩이 지름 {ShipCoopPuddleMesh.Diameter:F1}m ({shape.name}), 재질 {System.IO.Path.GetFileNameWithoutExtension(path)}");
    }

    // ------------------------------------------------------------
    // 🌊 뱃전 — 물 버리는 곳. **난간에 바로 버린다. 소품이 없다.**
    //
    //    ⚠ 한때 긴 상자(Crate_01)를 난간을 따라 놓았는데 뺐다. 물을 "상자에" 버리는 것으로 읽혔다 —
    //       뱃전은 난간 너머 바다로 버리는 곳이다. 배 난간이 이미 있으니 그게 표식이다.
    //       예전 씬에 남은 Prop_Crate 는 치운다. 버리는 판정은 부모 큐브 기준 ReachRange 그대로.
    //    스플래시(💦)는 큐브 바깥(+x) 난간 높이에서 배 밖 위로 뿜는다.
    // ------------------------------------------------------------

    /// <summary>스플래시 자리 — 큐브 중심에서 바깥(+x) · 위로 이만큼(m). 난간 윗단 높이(키 3m 배)쯤.</summary>
    private static readonly Vector3 SplashOffset = new Vector3(0.9f, 0.7f, 0f);

    // 💦 물 튀는 파티클. 세 팩 어디에도 없어서 직접 만든다. 연출 전용.
    private const string SplashPrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/FX_WaterSplash.prefab";
    private const string SplashMaterialPath = "Assets/Game/Art/Materials/ShipCoop/WaterSplash.mat";
    private static readonly Color SplashColorA = new Color(0.35f, 0.65f, 0.90f, 0.85f);
    private static readonly Color SplashColorB = new Color(0.60f, 0.85f, 1.00f, 0.55f);

    private static void DressDumps(StringBuilder log)
    {
        WaterDumpPoint[] rails = Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (rails.Length == 0)
        {
            return;
        }

        GameObject splashPrefab = EnsureSplashPrefab(log);

        log.AppendLine();
        log.AppendLine("  뱃전:");

        for (int i = 0; i < rails.Length; i++)
        {
            Transform cube = rails[i].transform;
            HideButKeepCollider(cube);

            // 예전 상자 소품. 남아 있으면 치운다 — 난간에 바로 버린다.
            RemoveChild(cube, "Prop_Crate");

            // 좌현(-x)인지 우현(+x)인지에 따라 스플래시 · 표식이 바깥을 보게 방향을 뒤집는다.
            float side = cube.position.x >= CenterX ? 1f : -1f;

            AttachSplash(cube, splashPrefab, side, log);
            AttachMarker(rails[i], cube, side, log);

            log.AppendLine($"    🌊 {cube.name,-12}  소품 없음(난간에 버림), 스플래시 · 노란 표식 붙임, {Describe(cube.position)}");
        }
    }

    /// <summary>💦 파티클을 큐브 바깥쪽(±x) 난간 높이에 자식으로 넣고 WaterDumpSplash 에 연결한다.</summary>
    private static void AttachSplash(Transform cube, GameObject splashPrefab, float side, StringBuilder log)
    {
        WaterDumpSplash fx = cube.GetComponent<WaterDumpSplash>();

        if (fx == null)
        {
            fx = Undo.AddComponent<WaterDumpSplash>(cube.gameObject);
        }

        if (splashPrefab == null)
        {
            return;
        }

        Transform burst = cube.Find("FX_Splash");

        if (burst == null)
        {
            GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(splashPrefab);
            made.name = "FX_Splash";
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
            burst = made.transform;
            burst.SetParent(cube, false);
        }

        // 큐브 중심에서 바깥(±x) · 난간 높이. 여기서 배 밖 위로 뿜는다.
        Undo.RecordObject(burst, "배 모델과 갑판 배치");
        burst.position = cube.position + new Vector3(SplashOffset.x * side, SplashOffset.y, SplashOffset.z);
        burst.rotation = Quaternion.LookRotation(new Vector3(side, 1f, 0f).normalized, Vector3.back);

        ParticleSystem system = burst.GetComponent<ParticleSystem>();

        if (system == null)
        {
            log.AppendLine($"    ⚠ {cube.name} 의 FX_Splash 에 ParticleSystem 이 없습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(fx);
        so.FindProperty("splash").objectReferenceValue = system;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 🟨 여기가 버리는 곳이라고 알려주는 노란 오버레이 박스. 물이 찼을 때만 <see cref="WaterDumpPoint"/> 가 깜박인다.
    ///
    /// 배 난간이 이미 있다고 표식 없이 뒀더니(DressDumps 위 주석) 실제로는 어디에 버릴 수 있는지
    /// 안 보였다 — 난간 자체는 배 전체를 두르고 있어 "여기" 를 가리키지 못한다. 그래서 각 뱃전
    /// 위에 반투명 노란 박스를 얹고 물이 찼을 때만 깜박이게 한다.
    /// </summary>
    private static readonly Vector3 MarkerOffset = new Vector3(0.6f, 0.9f, 0f);
    private static readonly Vector3 MarkerSize = new Vector3(1.6f, 1.6f, 1.6f);
    private const string MarkerMaterialPath = "Assets/Game/Art/Materials/ShipCoop/DumpMarker.mat";

    private static void AttachMarker(WaterDumpPoint dump, Transform cube, float side, StringBuilder log)
    {
        Transform marker = cube.Find("Marker_DumpZone");

        if (marker == null)
        {
            GameObject made = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.name = "Marker_DumpZone";
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
            Object.DestroyImmediate(made.GetComponent<Collider>());
            marker = made.transform;
            marker.SetParent(cube, false);
        }

        Undo.RecordObject(marker, "배 모델과 갑판 배치");
        marker.localPosition = new Vector3(MarkerOffset.x * side, MarkerOffset.y, MarkerOffset.z);
        marker.localRotation = Quaternion.identity;
        marker.localScale = MarkerSize;

        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = GetOrCreateMarkerMaterial(log);
        renderer.enabled = false;

        SerializedObject so = new SerializedObject(dump);
        so.FindProperty("marker").objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>노란 반투명 오버레이 재질. URP Lit/Unlit 이 없을 때만 새로 만든다.</summary>
    private static Material GetOrCreateMarkerMaterial(StringBuilder log)
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);

        if (made != null)
        {
            return made;
        }

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        if (unlit == null)
        {
            log.AppendLine("  ⚠ URP Unlit 셰이더를 못 찾아 뱃전 표식이 기본 재질로 남습니다.");
            return null;
        }

        made = new Material(unlit);
        made.SetFloat("_Surface", 1f);
        made.SetFloat("_Blend", 0f);
        made.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        made.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        made.SetFloat("_ZWrite", 0f);
        made.SetFloat("_Cull", 0f);
        made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        made.SetOverrideTag("RenderType", "Transparent");
        made.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        made.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f, 0.18f));

        AssetDatabase.CreateAsset(made, MarkerMaterialPath);
        log.AppendLine($"  🟨 뱃전 표식 재질을 만들었습니다 — {MarkerMaterialPath}");
        return made;
    }

    /// <summary>
    /// 💦 물 튀는 파티클 프리팹. 없으면 만든다.
    ///
    /// 한 번에 30~40개, 크기 0.15~0.35m (키 3m 세계라 작지 않게), 0.5~0.7초, 중력으로 떨어진다.
    /// 뿜는 방향은 오브젝트의 +z 라, 붙이는 쪽(AttachSplash)이 배 바깥 + 위를 보게 돌린다. 루프 없음.
    /// </summary>
    private static GameObject EnsureSplashPrefab(StringBuilder log)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SplashPrefabPath);

        if (existing != null)
        {
            return existing;
        }

        Material spray = GetOrCreateSplashMaterial(log);

        GameObject go = new GameObject("FX_WaterSplash");
        ParticleSystem system = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.7f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(SplashColorA, SplashColorB);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 40) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.25f;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer draw = go.GetComponent<ParticleSystemRenderer>();
        draw.renderMode = ParticleSystemRenderMode.Billboard;
        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (spray != null)
        {
            draw.sharedMaterial = spray;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, SplashPrefabPath);
        Object.DestroyImmediate(go);

        log.AppendLine($"  💦 물 튀는 파티클 프리팹을 만들었습니다 — {SplashPrefabPath}");
        return prefab;
    }

    /// <summary>URP Particles/Unlit + 유니티 기본 원형 파티클 텍스처. 새 텍스처는 만들지 않는다.</summary>
    private static Material GetOrCreateSplashMaterial(StringBuilder log)
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(SplashMaterialPath);

        if (made != null)
        {
            return made;
        }

        Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (particles == null)
        {
            log.AppendLine("  ⚠ URP Particles/Unlit 셰이더를 못 찾아 스플래시가 기본 재질로 남습니다.");
            return null;
        }

        made = new Material(particles);
        made.SetFloat("_Surface", 1f);
        made.SetFloat("_Blend", 0f);
        made.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        made.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        made.SetFloat("_ZWrite", 0f);
        made.SetFloat("_Cull", 0f);
        made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        made.SetOverrideTag("RenderType", "Transparent");
        made.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        made.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        made.SetColor("_BaseColor", Color.white);

        AssetDatabase.CreateAsset(made, SplashMaterialPath);
        log.AppendLine($"  💦 스플래시 재질을 만들었습니다 — {SplashMaterialPath}");
        return made;
    }

    // ------------------------------------------------------------
    // 🔧 수리 지점(파손 지점) 프리팹 — 빨간 큐브 대신 **세 겹**
    //
    //    HullDamage.Spawn 이 사건 때 Instantiate 하는 프리팹이라 씬에는 없다. 프리팹을 열어 자식을 얹는다.
    //
    //      🪵 판자 덮기   Plank_02 (들고 다니는 것과 같은 프리팹 · 같은 배율) hitsToRepair 장. 망치질마다 한 장
    //      🪵 조각        Plank_01 ×5, 0.42배(≈0.7m). 구멍 가장자리 반지름 0.55 에서 바깥으로 15~35° 들림. 망치질마다 하나씩 눕는다
    //      🕳 구멍        Quad + HullHole.mat   (Grunge_04 흰 마스크를 검게 틴트. 지름 1.5m)
    //
    //    ⚠ **붉은 표식(Circle_Soft_01 · HullMarker.mat)은 뺐다.** 붉게 칠한 원이 갑판 위에 떠 있어 게임 그림과 겉돌았다.
    //       솟은 조각이 멀리서도 "여기 부서졌다" 를 말한다. 예전 "Marker" 자식이 남아 있으면 치우고 HullMarker.mat 은 지운다.
    //    ⚠ **균열 데칼(Decal_Crack_01)은 뺐다.** 바닥에 붙은 데칼은 위에서 내려보는 카메라에 얼룩으로 보였다.
    //       조각이 위로 솟아야 "부서졌다" 가 보인다. 예전 "Crack" 자식이 남아 있으면 치우고 HullCrack.mat 은 지운다.
    //    ⚠ **물 자국 겹(Quad + HullLeak.mat)은 뺐다.** 웅덩이(퍼는 곳)와 같이 보여서 퍼야 할 곳과 막아야 할 곳이 헷갈렸다.
    //       예전에 만든 프리팹에 "Leak" 자식이 남아 있으면 치우고, HullLeak.mat 파일이 있으면 지운다.
    //    ⚠ 조각의 축(빈 부모 Shard_i)은 **안쪽 끝**에 둔다. 조각 원점이 가운데면 들 때 안쪽 끝이 갑판에 묻힌다.
    //       흔들림 · 들림은 HullDamageVisual.ShardYaw/ShardLift — Random 이 아니라 번호로. 네 명이 같은 모양을 본다.
    //    ⚠ 루트 · 콜라이더 · RepairTask 는 건드리지 않는다. 큐브 MeshRenderer 만 끈다. HullDamagePoint.mat 도 그대로 둔다.
    //    ⚠ 루트 배율이 (0.7, 0.6, 0.7) 이다. 자식 "Visual" 에 역배율을 넣어 그 아래는 m 단위로 잡는다.
    //       루트는 갑판에서 DamageLift 위에 놓이므로 Visual 을 그만큼 내려 갑판 윗면에 맞춘다. (런타임에 한 번 더 맞춘다)
    //    ⚠ **Synty 데칼 재질은 그대로 못 쓴다.** 텍스처로 URP 투명 재질을 새로 만든다. Synty 원본 재질은 고치지 않는다.
    //       (셰이더그래프 URP 타깃이 Decal 이라 Decal Renderer Feature 가 없는 PC_Renderer 에서 마젠타. 틴트도 바꿔야 한다)
    //    여러 번 돌려도 같은 결과다 — 있는 자식은 값만 다시 맞춘다.
    // ------------------------------------------------------------

    private const string HullDamagePrefabPath = "Assets/Game/Prefabs/MiniGames/ShipCoop/HullDamagePoint.prefab";
    private const string HoleTexturePath = "Assets/Synty/PolygonGeneric/Textures/Decals/Generic_Decal_Grunge_04.png";
    private const string HoleMaterialPath = "Assets/Game/Art/Materials/ShipCoop/HullHole.mat";
    private const string ShardModelPath = "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Plank_01.prefab";

    /// <summary>예전 재질들. 이제 안 쓴다 — 남아 있으면 지운다.</summary>
    private const string RetiredLeakMaterialPath = "Assets/Game/Art/Materials/ShipCoop/HullLeak.mat";
    private const string RetiredCrackMaterialPath = "Assets/Game/Art/Materials/ShipCoop/HullCrack.mat";
    private const string RetiredMarkerMaterialPath = "Assets/Game/Art/Materials/ShipCoop/HullMarker.mat";

    /// <summary>예전 자식들. 프리팹에 남아 있으면 치운다.</summary>
    private static readonly string[] RetiredDamageChildren = { "Leak", "Crack", "Marker" };

    // 3m 사람 기준 크기(m). 조각은 구멍 가장자리(반지름 0.55)에서 바깥으로.
    private const float HoleSize = 1.5f;

    // 갑판 윗면 기준 높이(m). Z-파이팅이 보이면 0.01 씩 더 벌린다.
    // (표식 +0.02 · 물 자국 +0.04 자리는 비워 둔다 — 간격을 메우려고 옮기지 않는다)
    private const float HoleLift = 0.03f;

    /// <summary>구멍 색. 거의 검정 — 갑판 아래 어둠. 순검정은 데칼 마스크의 부드러운 가장자리가 죽는다.</summary>
    private static readonly Color HoleTint = new Color(0.05f, 0.04f, 0.04f, 1f);

    /// <summary>판자 밑면을 균열보다 이만큼 위에. (판자 두께는 모델 것 그대로 — Plank_02 는 0.11m)</summary>
    private const float PatchPlankGap = 0.01f;

    /// <summary>판자 사이 간격(m). 폭 0.28 짜리를 0.27 마다 놓아 살짝 겹친다. 5장이면 1.36m — 균열 1.2m 를 덮는다.</summary>
    private const float PatchPlankSpacing = 0.27f;

    /// <summary>판자마다 다른 각도(°)와 x 밀림(m). 똑같이 놓으면 복사해 붙인 것처럼 보인다. 장수가 더 많으면 돌려 쓴다.</summary>
    private static readonly float[] PatchPlankYaws = { 4f, -7f, 6f, -8f, 5f, -5f, 7f, -4f };
    private static readonly float[] PatchPlankNudges = { 0.03f, -0.06f, 0.05f, -0.04f, 0.07f, -0.03f, 0.04f, -0.07f };


    private static void DressHullDamagePrefab(StringBuilder log)
    {
        GameObject plankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlankModelPath);

        if (plankPrefab == null)
        {
            log.AppendLine($"  ⚠ 판자 프리팹을 못 찾아 수리 지점을 건너뜁니다 — {PlankModelPath}");
            return;
        }

        Material holePaint = GetOrCreateDecalMaterial(HoleMaterialPath, HoleTexturePath, "Universal Render Pipeline/Unlit", HoleTint, 3001, log);

        // 물 자국 · 균열 · 표식 재질은 은퇴했다. 남아 있으면 지운다 — 프리팹이 더 참조하지 않으니 안전하다.
        foreach (string retired in new[] { RetiredLeakMaterialPath, RetiredCrackMaterialPath, RetiredMarkerMaterialPath })
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(retired) != null)
            {
                AssetDatabase.DeleteAsset(retired);
                log.AppendLine($"  🗑 예전 수리 지점 재질을 지웠습니다 — {retired}");
            }
        }

        GameObject shardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShardModelPath);

        if (shardPrefab == null)
        {
            log.AppendLine($"  ⚠ 조각 프리팹을 못 찾음: {ShardModelPath} — 조각 없이 만듭니다");
        }

        ShipCoopCargoVisuals visuals = AssetDatabase.LoadAssetAtPath<ShipCoopCargoVisuals>(CargoVisualsPath);
        float plankScale = visuals != null ? visuals.plankScale : 1f;

        GameObject root = PrefabUtility.LoadPrefabContents(HullDamagePrefabPath);

        if (root == null)
        {
            log.AppendLine($"  ⚠ 수리 지점 프리팹을 못 열었습니다: {HullDamagePrefabPath}");
            return;
        }

        try
        {
            // 빨간 큐브는 끈다. 지우지 않는다 — 콜라이더 · 메시 필터와 한 몸이라 루트를 건드리게 된다.
            MeshRenderer cube = root.GetComponent<MeshRenderer>();
            if (cube != null)
            {
                cube.enabled = false;
            }

            RepairTask task = root.GetComponent<RepairTask>();
            int hits = 5;

            if (task != null)
            {
                SerializedProperty hitsProperty = new SerializedObject(task).FindProperty("hitsToRepair");
                if (hitsProperty != null)
                {
                    hits = Mathf.Max(1, hitsProperty.intValue);
                }
            }

            Vector3 rootScale = root.transform.localScale;
            Transform visual = ChildOf(root.transform, "Visual");
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(1f / rootScale.x, 1f / rootScale.y, 1f / rootScale.z);
            visual.localPosition = new Vector3(0f, -DamageLift / rootScale.y, 0f);

            Renderer hole = MakeDecal(visual, "Hole", holePaint, HoleSize, HoleLift);

            // 예전 겹들. 프리팹에 남아 있으면 치운다. (Undo 없이 — 프리팹 내용물 안이다)
            foreach (string retiredName in RetiredDamageChildren)
            {
                Transform retired = visual.Find(retiredName);
                if (retired != null)
                {
                    Object.DestroyImmediate(retired.gameObject);
                    log.AppendLine($"  🗑 수리 지점의 예전 겹({retiredName})을 치웠습니다");
                }
            }

            Transform pile = ChildOf(visual, "Planks");
            pile.localPosition = Vector3.zero;
            pile.localRotation = Quaternion.identity;
            pile.localScale = Vector3.one;

            GameObject[] planks = new GameObject[hits];

            for (int i = 0; i < hits; i++)
            {
                string name = $"Plank_{i}";
                Transform plank = pile.Find(name);

                if (plank == null)
                {
                    GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(plankPrefab, pile);
                    made.name = name;
                    plank = made.transform;
                }

                foreach (Collider bump in plank.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(bump);
                }

                // 가운데부터 바깥으로: z 0, +1, -1, +2, -2 … × 간격. 켜지는 순서도 이 순서라 가운데가 먼저 덮인다.
                int ring = (i + 1) / 2;
                float side = i % 2 == 0 ? 1f : -1f;
                float z = ring * side * PatchPlankSpacing;
                float x = PatchPlankNudges[i % PatchPlankNudges.Length];
                float yaw = PatchPlankYaws[i % PatchPlankYaws.Length];

                plank.localScale = Vector3.one * plankScale;
                plank.localRotation = Quaternion.Euler(0f, yaw, 0f);
                plank.localPosition = Vector3.zero;

                // 밑면이 균열 위, 가운데가 (x, z) 에. 프리팹 원점이 어디든 bounds 로 맞추면 맞는다.
                Bounds box = BoundsOf(plank);
                Vector3 want = visual.TransformPoint(new Vector3(x, HoleLift + PatchPlankGap, z));
                plank.position += new Vector3(want.x - box.center.x, want.y - box.min.y, want.z - box.center.z);

                // 처음엔 전부 꺼져 있다. HullDamageVisual 이 망치질 수만큼 켠다.
                plank.gameObject.SetActive(false);
                planks[i] = plank.gameObject;
            }

            // hitsToRepair 가 줄었으면 남는 장은 치운다.
            for (int i = hits; ; i++)
            {
                Transform extra = pile.Find($"Plank_{i}");
                if (extra == null)
                {
                    break;
                }
                Object.DestroyImmediate(extra.gameObject);
            }

            // 🪵 조각 — 구멍 가장자리에서 바깥으로 들린 판자 조각. 축(빈 부모)은 안쪽 끝에.
            Transform shardGroup = ChildOf(visual, "Shards");
            shardGroup.localPosition = Vector3.zero;
            shardGroup.localRotation = Quaternion.identity;
            shardGroup.localScale = Vector3.one;

            Transform[] shards = new Transform[HullDamageVisual.ShardCount];

            for (int i = 0; i < HullDamageVisual.ShardCount; i++)
            {
                Transform pivot = ChildOf(shardGroup, $"Shard_{i}");
                float yaw = HullDamageVisual.ShardYaw(i);

                // 축을 구멍 가장자리에, 먼저 돌리지 않은 채로 둔다. 조각을 축에 맞춘 뒤에 돌린다.
                Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                pivot.localRotation = Quaternion.identity;
                pivot.localScale = Vector3.one;
                pivot.localPosition = outward * HullDamageVisual.ShardRadius + Vector3.up * HoleLift;

                Transform piece = pivot.Find("Piece");

                if (piece == null && shardPrefab != null)
                {
                    GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(shardPrefab, pivot);
                    made.name = "Piece";
                    piece = made.transform;
                }

                if (piece != null)
                {
                    foreach (Collider bump in piece.GetComponentsInChildren<Collider>(true))
                    {
                        Object.DestroyImmediate(bump);
                    }

                    piece.localRotation = Quaternion.identity;
                    piece.localScale = Vector3.one * HullDamageVisual.ShardScale;
                    piece.localPosition = Vector3.zero;

                    // 조각의 **안쪽 끝(x 최소)** 이 축에, 밑면이 축 높이에, z 가운데가 축 선에. 그래야 들 때 안쪽 끝이 갑판 위에 남는다.
                    // (축이 아직 안 돌아 있어 월드 축 = 축 로컬 축이다)
                    Bounds box = BoundsOf(piece);
                    piece.position += pivot.position - new Vector3(box.min.x, box.min.y, box.center.z);
                }

                // 이제 돈다 — Ry(yaw) · Rz(lift). 런타임(HullDamageVisual.SetShard)과 같은 식. 프리팹은 "다 들린" 상태로 저장한다.
                pivot.localRotation = Quaternion.Euler(0f, yaw, HullDamageVisual.ShardLift(i));
                shards[i] = pivot;
            }

            HullDamageVisual view = root.GetComponent<HullDamageVisual>();
            if (view == null)
            {
                view = root.AddComponent<HullDamageVisual>();
            }

            SerializedObject data = new SerializedObject(view);
            data.FindProperty("repair").objectReferenceValue = task;
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("hole").objectReferenceValue = hole;

            SerializedProperty list = data.FindProperty("planks");
            list.arraySize = hits;
            for (int i = 0; i < hits; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = planks[i];
            }

            SerializedProperty shardList = data.FindProperty("shards");
            shardList.arraySize = shards.Length;
            for (int i = 0; i < shards.Length; i++)
            {
                shardList.GetArrayElementAtIndex(i).objectReferenceValue = shards[i];
            }

            data.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HullDamagePrefabPath);
            log.AppendLine($"  🔧 수리 지점 프리팹 — 구멍 {HoleSize:F1}m · " +
                           $"조각 {shards.Length}개(배율 {HullDamageVisual.ShardScale:F2}, 반지름 {HullDamageVisual.ShardRadius:F2}, 들림 {HullDamageVisual.ShardLiftMin:F0}~{HullDamageVisual.ShardLiftMax:F0}°) · " +
                           $"판자 {hits}장(배율 {plankScale:F1}), 큐브 렌더러 끔 → {HullDamagePrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>위를 보는 Quad 하나. 있으면 값만 다시 맞춘다.</summary>
    private static Renderer MakeDecal(Transform parent, string name, Material paint, float size, float lift)
    {
        Transform quad = parent.Find(name);

        if (quad == null)
        {
            GameObject made = GameObject.CreatePrimitive(PrimitiveType.Quad);
            made.name = name;
            quad = made.transform;
            quad.SetParent(parent, false);
        }

        foreach (Collider bump in quad.GetComponents<Collider>())
        {
            Object.DestroyImmediate(bump);
        }

        // 유니티 Quad 는 -z 를 보는 판이다. x 로 90° 돌리면 위(+y)를 본다.
        quad.localPosition = new Vector3(0f, lift, 0f);
        quad.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.localScale = new Vector3(size, size, 1f);

        Renderer draw = quad.GetComponent<Renderer>();

        if (paint != null)
        {
            draw.sharedMaterial = paint;
        }

        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return draw;
    }

    /// <summary>이름으로 자식을 찾고, 없으면 빈 것을 만든다. (Undo 없이 — 프리팹 내용물 안에서 쓴다)</summary>
    private static Transform ChildOf(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null)
        {
            return found;
        }

        GameObject made = new GameObject(name);
        made.transform.SetParent(parent, false);
        return made.transform;
    }

    /// <summary>
    /// 텍스처 하나를 얹은 URP 투명 재질. 있으면 그대로 쓴다 (손으로 고친 값을 존중한다).
    /// 균열은 Lit(갑판 조명을 받는다), 표식은 Unlit(어디서나 같은 붉은색).
    /// </summary>
    private static Material GetOrCreateDecalMaterial(string path, string texturePath, string shaderName, Color tint, int queue, StringBuilder log)
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (made != null)
        {
            return made;
        }

        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            log.AppendLine($"  ⚠ '{shaderName}' 셰이더를 못 찾아 {path} 를 만들지 못했습니다.");
            return null;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        if (texture == null)
        {
            log.AppendLine($"  ⚠ 텍스처를 못 찾음: {texturePath}");
        }

        made = new Material(shader);
        made.SetFloat("_Surface", 1f);
        made.SetFloat("_Blend", 0f);
        made.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        made.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        made.SetFloat("_ZWrite", 0f);
        made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        made.SetOverrideTag("RenderType", "Transparent");
        made.renderQueue = queue;
        made.SetTexture("_BaseMap", texture);
        made.SetColor("_BaseColor", tint);

        AssetDatabase.CreateAsset(made, path);
        log.AppendLine($"  🎨 수리 지점 재질을 만들었습니다 — {path}");
        return made;
    }

    // ------------------------------------------------------------
    // 🎒 들고 다니는 물건의 모양 설정 — Resources 에 하나
    //
    //    CarryTask(들고 있을 때)와 DroppedCargo(놓았을 때)가 런타임에 Resources 로 읽는다.
    //    씬 플레이어와 네트워크 프리팹이 같은 설정을 쓰므로 씬에 손댈 것이 없다.
    // ------------------------------------------------------------

    private const string CargoVisualsPath = "Assets/Game/Resources/" + ShipCoopCargoVisuals.ResourceName + ".asset";
    private const string BucketModelPath = "Assets/QuaterniusPirateKit/Prop_Bucket.fbx";
    private const string BucketMaterialPath = "Assets/Game/Art/Materials/Fishing/QuaterniusPirateURP.mat";

    private static void EnsureCargoVisuals(StringBuilder log)
    {
        ShipCoopCargoVisuals visuals = AssetDatabase.LoadAssetAtPath<ShipCoopCargoVisuals>(CargoVisualsPath);
        bool fresh = visuals == null;

        if (fresh)
        {
            visuals = ScriptableObject.CreateInstance<ShipCoopCargoVisuals>();
            AssetDatabase.CreateAsset(visuals, CargoVisualsPath);
        }

        GameObject bucket = AssetDatabase.LoadAssetAtPath<GameObject>(BucketModelPath);
        Material paint = AssetDatabase.LoadAssetAtPath<Material>(BucketMaterialPath);
        GameObject plank = AssetDatabase.LoadAssetAtPath<GameObject>(PlankModelPath);

        if (bucket == null) log.AppendLine($"  ⚠ 양동이 모델을 못 찾음: {BucketModelPath}");
        else log.AppendLine($"  🪣 양동이 원본 — 루트 회전 {bucket.transform.localEulerAngles}, 루트 배율 {bucket.transform.localScale}, " +
                            $"크기 {BoundsOf(bucket.transform).size} (런타임은 루트 회전을 그대로 두고 높이만 맞춘다)");
        if (paint == null) log.AppendLine($"  ⚠ 양동이 재질을 못 찾음: {BucketMaterialPath}");
        if (plank == null) log.AppendLine($"  ⚠ 판자 모델을 못 찾음: {PlankModelPath}");

        Undo.RecordObject(visuals, "배 모델과 갑판 배치");
        visuals.bucketModel = bucket;
        visuals.bucketMaterial = paint;
        visuals.plankModel = plank;
        EditorUtility.SetDirty(visuals);

        log.AppendLine($"  🎒 물건 모양 설정 {(fresh ? "을 만들고" : "을")} 채웠습니다 — {CargoVisualsPath} " +
                       $"(양동이 높이 {visuals.bucketHeight:F2}m × 배율 {visuals.bucketScale:F1}, 판자 배율 {visuals.plankScale:F1})");
    }

    /// <summary>궤짝 안에 포탄 구를 놓는다. 콜라이더 없이, 큐브의 자식으로. 여러 번 돌려도 같은 자리다.</summary>
    private static void DressAmmoBalls(Transform cube, StringBuilder log)
    {
        Material ink = GetOrCreateAmmoBallMaterial(log);
        Transform tray = FindOrCreateGroup(cube, "Prop_Ammo");
        float radius = AmmoBallDiameter * 0.5f;

        for (int i = 0; i < AmmoBallSpots.Length; i++)
        {
            string name = $"Ball_{i}";
            Transform ball = tray.Find(name);

            if (ball == null)
            {
                GameObject made = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                made.name = name;
                Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");
                ball = made.transform;
                ball.SetParent(tray, false);
            }

            Collider bump = ball.GetComponent<Collider>();
            if (bump != null)
            {
                Undo.DestroyObjectImmediate(bump);
            }

            Vector3 spot = AmmoBallSpots[i];

            // 큐브 중심은 갑판에서 BoxLift 만큼 위다. 공은 궤짝 바닥판 위에 놓고, 윗층은 한 개 지름만큼 올린다.
            float centreFromDeck = AmmoBallFloor + radius + spot.y * AmmoBallDiameter;

            Undo.RecordObject(ball, "배 모델과 갑판 배치");
            ball.localScale = Vector3.one * AmmoBallDiameter;
            ball.localPosition = new Vector3(spot.x, centreFromDeck - BoxLift, spot.z);
            ball.localRotation = Quaternion.identity;

            Renderer draw = ball.GetComponent<Renderer>();
            if (ink != null && draw != null)
            {
                Undo.RecordObject(draw, "배 모델과 갑판 배치");
                draw.sharedMaterial = ink;
            }
        }

        // 개수를 줄였으면 남는 공을 치운다.
        for (int i = tray.childCount - 1; i >= AmmoBallSpots.Length; i--)
        {
            Undo.DestroyObjectImmediate(tray.GetChild(i).gameObject);
        }
    }

    /// <summary>포탄 구의 재질. DroppedCargo 는 런타임에 색만 칠하지만, 씬에 놓는 것은 에셋이 있어야 한다.</summary>
    private static Material GetOrCreateAmmoBallMaterial(StringBuilder log)
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(AmmoBallMaterialPath);

        if (made != null)
        {
            return made;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        if (lit == null)
        {
            log.AppendLine("  ⚠ URP/Lit 셰이더를 못 찾아 포탄 구가 기본 재질로 남습니다.");
            return null;
        }

        made = new Material(lit);
        made.SetColor("_BaseColor", AmmoBallColor);
        made.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(made, AmmoBallMaterialPath);
        log.AppendLine($"  포탄 구 재질을 만들었습니다 — {AmmoBallMaterialPath}");
        return made;
    }

    /// <summary>뚜껑 표현 컴포넌트를 큐브(AmmoBox)에 붙이고 뚜껑 참조를 채운다. 이미 있으면 참조만 다시 맞춘다.</summary>
    private static void AttachLid(Transform cube, Transform lid, StringBuilder log)
    {
        SupplyChestLid flap = cube.GetComponent<SupplyChestLid>();

        if (flap == null)
        {
            flap = Undo.AddComponent<SupplyChestLid>(cube.gameObject);
        }

        if (lid == null)
        {
            log.AppendLine($"    ⚠ 궤짝에서 뚜껑({ChestLidName})을 못 찾았습니다. 열고 닫히지 않습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(flap);
        so.FindProperty("lid").objectReferenceValue = lid;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveChild(Transform parent, string name)
    {
        Transform old = parent.Find(name);

        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
        }
    }

    /// <summary>
    /// 프리팹 하나를 큐브의 자식으로 얹는다. 같은 이름의 자식이 이미 있으면 그것을 쓴다.
    ///
    /// <code>
    ///   콜라이더   뗀다. 집기 판정은 거리로 하고, 걸려 넘어지면 안 된다. (DroppedCargo 와 같은 이유)
    ///   재질       URP 것으로 덮어쓴다. 프리팹 기본 재질은 URP 에서 마젠타다.
    /// </code>
    /// </summary>
    private static Transform DressWith(Transform cube, string childName, string prefabPath,
                                       Material paint, float scale, StringBuilder log)
    {
        Transform child = cube.Find(childName);

        if (child == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                log.AppendLine($"    ⚠ 소품 프리팹을 못 찾음: {prefabPath}");
                return null;
            }

            GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            made.name = childName;
            Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

            child = made.transform;
            child.SetParent(cube, false);
        }

        Undo.RecordObject(child, "배 모델과 갑판 배치");
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one * scale;

        foreach (Collider bump in child.GetComponentsInChildren<Collider>(true))
        {
            Undo.DestroyObjectImmediate(bump);
        }

        foreach (Renderer draw in child.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(draw, "배 모델과 갑판 배치");

            // paint 가 null 이면 프리팹 재질을 그대로 둔다. (Synty 궤짝)
            if (paint != null)
            {
                Material[] coats = draw.sharedMaterials;

                for (int i = 0; i < coats.Length; i++)
                {
                    coats[i] = paint;
                }

                draw.sharedMaterials = coats;
            }

            draw.enabled = true;
        }

        return child;
    }

    /// <summary>
    /// 소품을 제자리에 앉힌다.
    ///
    /// ⚠ 프리팹 루트가 메시 한가운데가 아니다. 그냥 자식으로 넣으면 큐브 옆 허공에 뜬다.
    ///    렌더러 bounds 를 재서 **밑면이 갑판에 닿고**, 가운데가 큐브 중심에서
    ///    <paramref name="offsetXZ"/> 만큼 떨어진 곳에 오도록 옮긴다.
    /// </summary>
    private static void Seat(Transform prop, Transform cube, Vector2 offsetXZ, float deckY)
    {
        if (prop == null)
        {
            return;
        }

        // 매번 0 에서 다시 재야 여러 번 돌려도 같은 자리에 온다.
        prop.localPosition = Vector3.zero;

        Bounds box = BoundsOf(prop);
        Vector3 want = new Vector3(cube.position.x + offsetXZ.x, deckY + box.extents.y, cube.position.z + offsetXZ.y);

        prop.position += want - box.center;
    }

    /// <summary>자식 렌더러 전체를 감싸는 월드 bounds.</summary>
    private static Bounds BoundsOf(Transform t)
    {
        Renderer[] draws = t.GetComponentsInChildren<Renderer>(true);

        if (draws.Length == 0)
        {
            return new Bounds(t.position, Vector3.zero);
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        return box;
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
        BuildWake(sea, log);
        SeeFarEnough(log);
        HideResult(log);
    }

    // ------------------------------------------------------------
    // 🌊 뱃머리가 가르는 물살
    //
    // ⚠ 물결을 미는 것만으로는 **속도가 변한 것이 잘 안 읽힙니다.**
    //    돛을 당겨 빨라졌는데 화면이 그대로면 돛을 당길 이유가 없습니다.
    //    물살은 **빠르면 많이, 느리면 적게** 생기므로 속도가 그림의 양으로 바뀝니다.
    //
    // 알갱이는 **둥글고 가장자리가 흐려야** 합니다. 납작한 사각형을 얹었더니
    // 바다 위에 흰 판때기가 둥둥 뜬 것으로 보였습니다. (ShipCoopBlobShadow)
    // ------------------------------------------------------------

    private const string WakeName = "Wake";

    /// <summary>
    /// 물살이 생기는 범위. **배 둘레 전체**입니다.
    ///
    /// ⚠ 처음에는 뱃머리에만 뿌렸는데 **하나도 안 보였습니다.**
    ///    거기는 선체와 앞갑판에 가려서 갑판 카메라에서 보이지 않습니다.
    ///    보이는 것은 **배 옆의 바다**라, 거기에 뿌려야 합니다.
    ///    배 밑에 생기는 것은 선체에 가려 안 보이니 그냥 둡니다.
    /// </summary>
    private const float WakeWidth = 30f;

    private const float WakeLength = 44f;

    /// <summary>물 위로 살짝. 물에 묻히면 안 보인다.</summary>
    private const float WakeLift = 0.15f;

    /// <summary>
    /// 물살은 **이펙트 에셋으로 넣을 예정**이라 그때까지 꺼 둡니다.
    ///
    /// 여기 있는 것은 둥근 알갱이를 뿌리는 임시 구현입니다.
    /// 에셋이 안 맞을 때 견줘볼 것이 있게 남겨 둡니다.
    /// </summary>
    private const bool UseWake = false;

    private static void BuildWake(VoyageSea sea, StringBuilder log)
    {
        if (!UseWake)
        {
            Transform old = sea.transform.Find(WakeName);

            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
                log.AppendLine("  알갱이 물살을 걷어냈습니다. (셰이더 거품을 씁니다)");
            }

            return;
        }

        Material foam = ShipCoopBlobShadow.GetOrCreateFoam();

        if (foam == null)
        {
            log.AppendLine("  ⚠ 물살 재질을 못 만들었습니다.");
            return;
        }

        Transform found = sea.transform.Find(WakeName);
        GameObject go;

        if (found == null)
        {
            go = new GameObject(WakeName);
            Undo.RegisterCreatedObjectUndo(go, "배 모델과 갑판 배치");
            go.transform.SetParent(sea.transform, false);
        }
        else
        {
            go = found.gameObject;
        }

        // ⚠ **뒤로 흘러가야 한다.** 배는 +z 를 보고 있으므로 물은 -z 로 간다.
        //    파티클은 자기 앞쪽(+z)으로 뿌리므로, 오브젝트를 180도 돌려 둔다.
        //    (모양을 90도 눕혀서 뿌렸더니 **물속으로 가라앉아 안 보였습니다.**)
        go.transform.localPosition = new Vector3(0f, WakeLift, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        ParticleSystem spray = go.GetComponent<ParticleSystem>();

        if (spray == null)
        {
            spray = Undo.AddComponent<ParticleSystem>(go);
        }

        // ⚠ **잘고 많아야 거품으로 보입니다.**
        //
        //    처음에는 3m 짜리를 초당 40개 뿌렸는데 **띄엄띄엄한 흰 얼룩**이 되어
        //    인위적으로 보였습니다. 거품은 크기가 제각각인 작은 알갱이가
        //    잔뜩 있는 것입니다. 크기를 줄이고 개수를 크게 늘립니다.
        ParticleSystem.MainModule main = spray.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4f);
        main.startSpeed = 3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.4f);

        // 알갱이마다 돌려 둔다. 같은 그림이 나란히 있으면 무늬가 눈에 띈다.
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // 알갱이마다 돌려 둔다. 같은 그림이 나란히 있으면 무늬가 눈에 띈다.
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // 더하기로 섞으므로 하나하나는 옅어야 한다. 겹치는 데서 밝아진다.
        main.startColor = new Color(1f, 1f, 1f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;
        main.playOnAwake = true;

        // 배 둘레 넓게 뿌린다. 배 밑에 생기는 것은 선체에 가려 안 보인다.
        ParticleSystem.ShapeModule shape = spray.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(WakeWidth, 0.1f, WakeLength);
        shape.rotation = Vector3.zero;

        ParticleSystem.EmissionModule emission = spray.emission;
        emission.enabled = true;
        emission.rateOverTime = 240f;

        // 알갱이마다 흐르는 속도를 조금씩 다르게. 다 같은 속도로 가면
        // **판때기 하나가 통째로 미끄러지는 것**처럼 보인다.
        ParticleSystem.VelocityOverLifetimeModule drift = spray.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.World;
        // ⚠ x · y · z 를 **전부 같은 방식으로** 줘야 합니다.
        //    하나만 빼면 "Particle Velocity curves must all be in the same mode" 가 납니다.
        drift.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        drift.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        // 생겼다가 사라진다. 갑자기 툭 사라지면 눈에 걸린다.
        ParticleSystem.ColorOverLifetimeModule fade = spray.colorOverLifetime;
        fade.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1f),
            });

        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        // 퍼지면서 옅어진다. 물살이 번지는 모양이다.
        ParticleSystem.SizeOverLifetimeModule grow = spray.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));

        ParticleSystemRenderer draw = go.GetComponent<ParticleSystemRenderer>();
        draw.sharedMaterial = foam;
        draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        draw.receiveShadows = false;

        // ⚠ **물 위에 눕혀서 그려야 합니다.**
        //    보통 빌보드는 카메라를 향해 **서 있어서** 안개 기둥처럼 보입니다.
        //    HorizontalBillboard 가 바닥에 눕혀 그립니다. 물거품은 수면에 붙어 있어야 합니다.
        draw.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        draw.sortingFudge = -10f;   // 물 판보다 앞에 그린다

        ShipCoopWake driver = go.GetComponent<ShipCoopWake>();

        if (driver == null)
        {
            driver = Undo.AddComponent<ShipCoopWake>(go);
        }

        // ⚠ **값도 덮어씁니다.** 컴포넌트가 이미 씬에 있으면 코드의 기본값이
        //    반영되지 않습니다. 붙일 때 한 번 정해진 값이 그대로 남습니다.
        SerializedObject wso = new SerializedObject(driver);
        wso.FindProperty("mostPerSecond").floatValue = 420f;
        wso.FindProperty("leastPerSecond").floatValue = 90f;
        wso.FindProperty("flowRatio").floatValue = 2.2f;
        wso.FindProperty("leastFlow").floatValue = 2f;
        wso.ApplyModifiedProperties();

        log.AppendLine($"  물살을 배 둘레 {WakeWidth:F0} × {WakeLength:F0}m 에 뿌리게 했습니다");
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

        if (UseWaterWorks)
        {
            TuneWaterWorks(water, log);
        }
        else
        {
            ShowHullFoam(water, log);
        }

        Transform group = FindOrCreateGroup(sea.transform, SeaGroupName);

        for (int i = group.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(group.GetChild(i).gameObject);
        }

        var scroll = new System.Collections.Generic.List<Transform>();

        // 한 장. 가운데를 보이는 범위 뒤끝에서 판 길이의 절반만큼 앞에 둔다.
        // 그러면 앞쪽에 넉넉히 남아서 끝까지 흘러도 바다가 안 모자란다.
        Transform sheet = MakeSeaTile(group, "Water", water,
            new Vector3(0f, 0f, SeaBackZ + SeaLength * 0.5f));

        scroll.Add(sheet);

        int foam = BuildFoam(group, scroll, log);

        SerializedObject so = new SerializedObject(sea);

        // ⚠ **되돌리지 않습니다.** `VoyageSea` 는 간 거리를 이 값으로 나눈
        //    나머지만큼 판을 뒤로 밉니다. 항해 거리보다 크게 두면
        //    나머지가 곧 간 거리라서 판이 **한 번도 안 튀고** 끝까지 흐릅니다.
        so.FindProperty("scrollLoopLength").floatValue = SeaLength;

        SerializedProperty list = so.FindProperty("scrollWithSpeed");
        list.arraySize = scroll.Count;

        for (int i = 0; i < scroll.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = scroll[i];
        }

        so.ApplyModifiedProperties();

        // ⚠ **물 판을 옮기는 것만으로는 배가 나아가 보이지 않습니다.**
        //    무늬가 판이 아니라 시간과 월드 좌표로 그려져서, 판을 아무리 뒤로
        //    밀어도 무늬는 제자리에 있습니다. 무늬 자체를 밀어야 합니다.
        //
        //    미는 값은 `_Normal_Pan_Speed` 입니다. 이름이 비슷한
        //    `_Animation_Offset` 은 해안가 파도용이라 먼바다에는 아무 상관이
        //    없습니다. 그걸로 한참 돌아갔습니다. (5장)
        ShipCoopSeaFlow flow = group.GetComponent<ShipCoopSeaFlow>();

        if (flow == null)
        {
            flow = Undo.AddComponent<ShipCoopSeaFlow>(group.gameObject);
            log.AppendLine("  물 무늬를 배 속도로 밀도록 ShipCoopSeaFlow 를 붙였습니다");
        }

        // ⚠ 이미 붙어 있으면 기본값이 안 들어갑니다. 씬에 박힌 값을 여기서 맞춥니다.
        //    스크립트만 고치고 씬을 안 고쳐서 "흐르는 게 안 느껴진다" 를
        //    한 번 더 들었습니다.
        SerializedObject flowSo = new SerializedObject(flow);
        flowSo.FindProperty("flowPerShipSpeed").floatValue = 4f;
        flowSo.FindProperty("leastFlow").floatValue = 1.5f;
        flowSo.ApplyModifiedProperties();

        log.AppendLine($"  물 판 한 장 ({SeaWidth:F0} × {SeaLength:F0}m) · 물거품 {foam}줄 을 깔았습니다");
    }

    /// <summary>
    /// 우리 전용 물 재질을 준비한다. 없으면 로비 것을 베껴 온다.
    ///
    /// 베끼는 이유는 위 `WaterMaterialPath` 주석에 적어 두었습니다 —
    /// 원본을 밀면 로비 물까지 흘러갑니다.
    /// </summary>
    private static Material GetOrCopyWater(StringBuilder log)
    {
        string from = UseWaterWorks ? WaterWorksPath : LobbyWaterPath;
        string what = UseWaterWorks ? "WaterWorks" : "로비";

        Material mine = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);

        // 쓰기로 한 셰이더와 지금 사본의 셰이더가 다르면 다시 베낀다.
        Material source = AssetDatabase.LoadAssetAtPath<Material>(from);

        if (source == null)
        {
            log.AppendLine($"  ⚠ 물 재질을 못 찾음: {from}");
            return mine;
        }

        if (mine != null && mine.shader == source.shader)
        {
            return mine;
        }

        if (mine != null)
        {
            AssetDatabase.DeleteAsset(WaterMaterialPath);
            log.AppendLine($"  물을 {what} 것으로 바꿉니다. 예전 사본을 지웠습니다.");
        }

        if (!AssetDatabase.CopyAsset(from, WaterMaterialPath))
        {
            log.AppendLine($"  ⚠ 물 재질을 베끼지 못했습니다: {WaterMaterialPath}");
            return null;
        }

        AssetDatabase.ImportAsset(WaterMaterialPath);
        log.AppendLine($"  {what} 물을 베껴 우리 것을 만들었습니다: {WaterMaterialPath}");

        return AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
    }

    /// <summary>
    /// 🌊 물이 배에 닿는 자리에 **거품**이 생기게 한다.
    ///
    /// ⚠ 이 셰이더에는 원래 그 기능이 있습니다. `_Enable_Shore_Foam` 이 켜져 있는데도
    ///    안 보였던 것은 **거품 색의 알파가 0** 이었기 때문입니다. 그리긴 그리는데
    ///    투명해서 안 보이던 것입니다.
    ///
    /// 파티클로 흉내낸 것보다 이쪽이 낫습니다. **선체를 따라 저절로 생기고**,
    /// 물결과 같은 셰이더라 따로 놀지 않습니다.
    ///
    /// 로비 원본이 아니라 **우리 사본**만 고칩니다. (WaterMaterialPath)
    /// </summary>
    private static void ShowHullFoam(Material water, StringBuilder log)
    {
        if (water == null)
        {
            return;
        }

        Undo.RecordObject(water, "배 모델과 갑판 배치");

        SetAlpha(water, "_Foam_Color", 1f);
        SetAlpha(water, "_Shore_Foam_Color_Tint", 1f);
        SetAlpha(water, "_Ocean_Wave_Foam_Color", 1f);

        if (water.HasProperty("_Shore_Foam_Intensity"))
        {
            water.SetFloat("_Shore_Foam_Intensity", 1f);
        }

        SetIfHas(water, "_Shore_Edge_Opacity", 0.6f);

        // ------------------------------------------------------------
        // ⚠ **무늬가 작아야 흐르는 것이 보입니다.**
        //
        //    받아온 값은 0.04 — 무늬 하나가 25m 입니다. 강물용이라 그렇습니다.
        //    배 속도가 1.25m/s 니까 무늬 한 칸 지나가는 데 **20초**입니다.
        //    무늬를 아무리 밀어도 "흐르는 게 하나도 안 느껴진다" 가 됩니다.
        //
        //    0.15 면 7m 라 5~6초에 한 칸입니다. 이건 눈에 보입니다.
        //
        //    ⚠ 더 잘게 쪼개면 멀리서 지글거립니다. 전에 그걸로 한참 고생했습니다.
        //      (`UseWaves` 주석) 흐르는 게 부족하면 무늬를 더 쪼개지 말고
        //      **`ShipCoopSeaFlow.flowPerShipSpeed` 를 올리세요.**
        SetIfHas(water, "_Normal_Tiling", 0.15f);

        EditorUtility.SetDirty(water);
        AssetDatabase.SaveAssets();

        log.AppendLine("  물이 배에 닿는 자리에 거품이 보이게 했습니다 (셰이더 기능, 알파가 0 이었음)");
    }

    /// <summary>
    /// WaterWorks 물을 **우리 배 크기에 맞춘다.**
    ///
    /// ⚠ 받아온 값은 **연못 크기**에 맞춰져 있습니다. 우리 바다는 400 × 600m 라
    ///    무늬가 너무 촘촘해서 화면이 **자잘한 흰 점으로 지글거립니다.**
    ///    무늬를 크게 늘리고 잔결을 줄여야 바다로 보입니다.
    ///
    /// 색도 회색이라 이 게임의 그림체와 안 맞습니다. 로비 바다에 가까운
    /// 청록으로 맞춰 둡니다.
    ///
    /// 이 값들은 **눈으로 보고 맞추는 것**입니다. `SeaWater.mat` 을 인스펙터에서
    /// 바로 만지면 됩니다. 우리 사본이라 WaterWorks 원본은 안 바뀝니다.
    /// </summary>
    private static void TuneWaterWorks(Material water, StringBuilder log)
    {
        Undo.RecordObject(water, "배 모델과 갑판 배치");

        // ⚠ **이 셰이더에 있는 값만 만집니다.**
        //
        //    한동안 `_Frequency` · `_Foam_Cutoff` · `_Edge_Offset` 을 같이 넣고
        //    있었습니다. **셰이더에 없는 이름이라 전부 무시됐습니다.** 재질
        //    파일에는 남아 있어서 고치고 있는 줄 알았습니다. 실제로 있는 값은
        //    `SSR_Water.shadergraph` 의 `m_DefaultReferenceName` 열일곱 개뿐입니다.

        // 무늬 간격. 낮출수록 무늬가 크다. 0.03 이면 33m 마다 한 번.
        SetIfHas(water, "_Tiling", 0.03f);

        // 그림체에 맞는 청록. 회색 바다는 배와 따로 논다.
        // 하늘돔이 밝아서 물은 진해야 바다로 읽힙니다.
        SetColorIfHas(water, "_Color", new Color(0.05f, 0.28f, 0.34f, 1f));
        SetColorIfHas(water, "_EdgeColor", new Color(0.25f, 0.62f, 0.66f, 1f));

        // 화면 공간 반사. 물 위에 픽셀 단위 잡음을 얹는다. 무겁기도 하다.
        SetIfHas(water, "_ScreenSpaceReflections", 0f);

        // 코스틱은 **바닥이 있어야** 의미가 있다. 망망대해엔 바닥이 없다.
        SetIfHas(water, "_Caustic_Strength", 0f);

        if (UseWaves)
        {
            // 켜는 법은 위 `UseWaves` 주석을 먼저 읽으세요.
            // 셰이더 법선을 잇기 전에는 지글거림이 반드시 따라옵니다.
            SetIfHas(water, "_Displacement_Amount", 2.5f);
            SetIfHas(water, "_Displacement_Scale", 0.04f);
            SetIfHas(water, "_Displacement_Speed", 0.2f);
            SetIfHas(water, "_NormalStrength", 0.18f);
            SetIfHas(water, "_Speed", 0.15f);
            SetIfHas(water, "_UseFoam", 1f);
            SetColorIfHas(water, "_FoamColor", new Color(0.62f, 0.82f, 0.86f, 1f));
            SetIfHas(water, "_WaveFrequency", 1.2f);
            SetIfHas(water, "_WaveSpeed", 0.4f);
            SetIfHas(water, "_WaveDist", 12f);
            SetIfHas(water, "_MaxWaveDist", 120f);
        }
        else
        {
            // 잔잔한 물. 지글거리게 만드는 것을 전부 0 으로 둔다.
            SetIfHas(water, "_Displacement_Amount", 0f);
            SetIfHas(water, "_Speed", 0.05f);

            // ⚠ **0 으로 두면 물이 거울이 됩니다.**
            //    완전히 평평한 물은 하늘을 그대로 비춰서 **허연 판때기**로 보입니다.
            //    하늘돔을 씌우고 나서 더 심해졌습니다. 하늘이 밝아졌으니까요.
            //    지글거리지 않을 만큼만 남깁니다.
            SetIfHas(water, "_NormalStrength", 0.08f);
            SetIfHas(water, "_Transparency", 0.55f);
            SetIfHas(water, "_UseFoam", 0f);
            SetIfHas(water, "_WaveFrequency", 0f);
            SetIfHas(water, "_WaveSpeed", 0f);
        }

        EditorUtility.SetDirty(water);
        AssetDatabase.SaveAssets();

        log.AppendLine(UseWaves
            ? "  WaterWorks 물에 물결을 넣었습니다"
            : "  WaterWorks 물을 잔잔하게 뒀습니다 (물결 꺼짐)");
    }

    private static void SetIfHas(Material material, string name, float value)
    {
        if (material.HasProperty(name))
        {
            material.SetFloat(name, value);
        }
    }

    private static void SetColorIfHas(Material material, string name, Color value)
    {
        if (material.HasProperty(name))
        {
            material.SetColor(name, value);
        }
    }

    private static void SetAlpha(Material material, string name, float alpha)
    {
        if (!material.HasProperty(name))
        {
            return;
        }

        Color color = material.GetColor(name);
        color.a = alpha;
        material.SetColor(name, color);
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

        int bands = Mathf.CeilToInt((FoamFrontZ - FoamBackZ) / FoamLoopLength);
        int spots = FoamSpots.GetLength(0);
        int made = 0;

        for (int band = 0; band < bands; band++)
        {
            for (int i = 0; i < spots; i++)
            {
                float x = FoamSpots[i, 0];
                float z = FoamBackZ + FoamLoopLength * (band + FoamSpots[i, 1]);
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

    // ------------------------------------------------------------
    // 하늘 — 로비와 같은 하늘을 쓴다
    //
    // ⚠ **하늘 재질이 문제가 아니었습니다.**
    //
    //    로비도 이 씬도 하늘 재질은 똑같은 유니티 기본 것입니다.
    //    (`Default-Skybox`, fileID 10304) 그런데 로비는 환하고 여기는 칙칙했습니다.
    //
    //    다른 것은 **빛 설정**이었습니다.
    //      · 로비  주변광 = 하늘색 그라데이션, 세기 1.19, 안개 켬
    //      · 여기  주변광 = 회색 계열 기본값,  세기 1,    안개 끔
    //
    //    그래서 재질을 옮기는 대신 **빛 설정을 옮겨옵니다.**
    //    아래 값은 전부 `Lobby.unity` 에서 그대로 읽어온 것입니다.
    //
    // ⚠ 안개는 **수평선을 만들어 줍니다.** 끄면 바다가 허공에서 뚝 끊깁니다.
    //    로비 값(밀도 0.001, 지수제곱)이면 500m 앞이 살짝 뿌예지는 정도입니다.
    // ------------------------------------------------------------

    /// <summary>로비와 같은 하늘·빛을 이 씬에 옮겨온다.</summary>
    private static void MatchLobbySky(StringBuilder log)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.514151f, 0.9568135f, 1f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.8301887f, 0.75861585f, 0.68529725f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.3962264f, 0.3395333f, 0.25978997f, 1f);
        RenderSettings.ambientIntensity = 1.19f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.603f, 0.9192912f, 1f, 1f);
        RenderSettings.fogDensity = 0.001f;

        log.AppendLine("  하늘 빛을 로비와 같게 맞췄습니다 (주변광 하늘색 1.19 · 안개 켬)");

        MatchLobbySun(log);
    }

    // ------------------------------------------------------------
    // 하늘 — **돔과 구름 고리를 씌웁니다**
    //
    // ⚠ **유니티의 Skybox 칸으로는 안 됩니다.**
    //
    //    처음에는 로비처럼 빛 설정만 옮기고 끝냈습니다. 그런데 로비도 이 씬도
    //    하늘 재질이 유니티 기본 것(`Default-Skybox`)이라, **하늘이 그냥 파란
    //    그라데이션**입니다. 구름도 없고 볼 것이 없습니다.
    //
    //    Synty 는 하늘을 Skybox 로 안 만듭니다. **큰 돔 메시**를 씌우고 그 안쪽에
    //    그라데이션을 칠합니다. 그래서 `Skybox_Tropical_Day` 는 이름과 달리
    //    Skybox 셰이더가 아니라 **돔 메시용**입니다. (`SkyDome.shadergraph`)
    //    RenderSettings 의 Skybox 칸에 넣으면 아무 일도 안 일어납니다.
    //
    //    구름도 따로입니다. 고리 모양 메시(`SM_Env_Cloud_Ring_01/02`)를
    //    돔 안쪽에 두 겹 띄웁니다.
    //
    // ⚠ **카메라 사거리 안에 들어와야 보입니다.** 사거리가 500m 라
    //    돔 반지름을 480 으로 둡니다. 더 키우면 잘려서 안 보입니다.
    //
    // ⚠ **콜라이더를 지웁니다.** 배치 도구가 갑판 높이를 레이로 재는데,
    //    하늘이 콜라이더를 갖고 있으면 레이가 하늘에 먼저 맞습니다.
    // ------------------------------------------------------------

    private const string SkyGroupName = "Sky";

    private const string SkydomePath = "Assets/Synty/PNB_Core/Prefabs/SM_Env_Skydome_01.prefab";
    private const string CloudRing1Path = "Assets/Synty/PNB_Core/Prefabs/SM_Env_Cloud_Ring_01.prefab";
    private const string CloudRing2Path = "Assets/Synty/PNB_Core/Prefabs/SM_Env_Cloud_Ring_02.prefab";

    /// <summary>돔에 입힐 낮 하늘. 받아온 기본값은 노을이라 이 게임과 안 맞는다.</summary>
    private const string SkyMaterialPath =
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Materials/Skybox_Tropical_Day.mat";

    /// <summary>구름 색. 기본값은 노을용이라 같이 바꾼다.</summary>
    private const string CloudMaterialPath =
        "Assets/Synty/PNB_Core/Materials/Synty_Clouds_Tropical.mat";

    /// <summary>돔의 반지름 (m). 카메라 사거리(500)보다 작아야 한다.</summary>
    private const float SkyRadius = 480f;

    /// <summary>
    /// 구름 고리의 반지름 (m). 돔 안쪽이어야 한다.
    ///
    /// ⚠ **낮고 멀어야 바다 위 구름으로 보입니다.** 330m · 높이 110m 로 뒀더니
    ///    올려본 각도가 18° 라 **화면 위로 벗어나서** 갑판에서는 거의 안 보였습니다.
    ///    수평선 가까이 두면 "바다 끝에 낀 구름" 이 되어 훨씬 잘 읽힙니다.
    /// </summary>
    private const float CloudRadius = 440f;

    /// <summary>구름이 뜨는 높이 (해수면 기준, m).</summary>
    private const float CloudHeight = 45f;

    // 씬 맨 위에 있는 빈 오브젝트. 없으면 만든다. (하늘처럼 부모가 없는 것용)
    private static Transform FindOrCreateRootGroup(string name)
    {
        GameObject found = GameObject.Find(name);

        if (found == null)
        {
            found = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(found, "배 모델과 갑판 배치");
        }

        found.transform.position = Vector3.zero;
        return found.transform;
    }

    /// <summary>하늘돔과 구름을 씌운다.</summary>
    private static void BuildSky(StringBuilder log)
    {
        Transform group = FindOrCreateRootGroup(SkyGroupName);

        group.gameObject.SetActive(true);

        for (int i = group.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(group.GetChild(i).gameObject);
        }

        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        Material cloud = AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);

        // ⚠ 돔의 밑동을 해수면보다 **아래**로 내립니다. 딱 맞추면 수평선에서
        //    바다와 돔이 겹쳐 지글거립니다.
        Transform dome = PutSkyPiece(group, SkydomePath, sky, SkyRadius,
            new Vector3(CenterX, SeaLevelY - 6f, 0f), 0f, log);

        // 구름은 세 겹입니다. 각도와 높이를 전부 다르게 둡니다.
        // 같은 각도로 겹치면 고리 두 개가 한 겹으로 보여서 넣은 값이 없어집니다.
        Transform ring1 = PutSkyPiece(group, CloudRing1Path, cloud, CloudRadius,
            new Vector3(CenterX, SeaLevelY + CloudHeight, 0f), 0f, log);

        Transform ring2 = PutSkyPiece(group, CloudRing2Path, cloud, CloudRadius * 0.82f,
            new Vector3(CenterX, SeaLevelY + CloudHeight * 2.4f, 0f), 55f, log);

        Transform ring3 = PutSkyPiece(group, CloudRing1Path, cloud, CloudRadius * 0.6f,
            new Vector3(CenterX, SeaLevelY + CloudHeight * 4.2f, 0f), 140f, log);

        int made = (dome != null ? 1 : 0) + (ring1 != null ? 1 : 0)
                 + (ring2 != null ? 1 : 0) + (ring3 != null ? 1 : 0);

        log.AppendLine($"  하늘 {made}겹 (돔 반지름 {SkyRadius:F0}m · 구름 {CloudRadius:F0}m)");
    }

    // 하늘 조각 하나를 놓는다. 반지름에 맞춰 키우고, 콜라이더와 그림자를 지운다.
    private static Transform PutSkyPiece(Transform group, string path, Material paint,
                                         float wantRadius, Vector3 at, float yaw,
                                         StringBuilder log)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            log.AppendLine($"  ⚠ 하늘 조각을 못 찾음: {path}");
            return null;
        }

        GameObject made = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

        made.transform.SetParent(group, false);
        made.transform.localScale = Vector3.one;
        made.transform.position = Vector3.zero;

        // 원래 크기를 재서 원하는 반지름이 되도록 키운다.
        Renderer[] draws = made.GetComponentsInChildren<Renderer>();

        if (draws.Length == 0)
        {
            log.AppendLine($"  ⚠ {prefab.name} 에 그릴 것이 없습니다");
            Object.DestroyImmediate(made);
            return null;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        float nowRadius = Mathf.Max(box.size.x, box.size.z) * 0.5f;
        float grow = nowRadius > 0.01f ? wantRadius / nowRadius : 1f;

        made.transform.localScale = Vector3.one * grow;
        made.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        made.transform.position = at;

        // ⚠ **원점이 아니라 아랫면을 기준으로 놓습니다.**
        //
        //    프리팹의 원점이 메시 한가운데에 있다는 보장이 없습니다. 구름 고리는
        //    원점이 메시보다 20m 아래에 있어서, 반지름에 맞춰 3배로 키우자
        //    그 간격도 3배가 됐습니다. **45m 에 놓으라고 했는데 100m 에 떴습니다.**
        //    올려보는 각이 12도가 되어 화면 위로 벗어났고, 그래서 구름이
        //    하나도 안 보였습니다.
        //
        //    키운 뒤에 다시 재서, 아랫면이 원하는 높이에 오도록 내려줍니다.
        Bounds after = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            after.Encapsulate(draws[i].bounds);
        }

        made.transform.position += new Vector3(0f, at.y - after.min.y, 0f);

        for (int i = 0; i < draws.Length; i++)
        {
            if (paint != null)
            {
                draws[i].sharedMaterial = paint;
            }

            // 하늘은 그림자를 주고받지 않는다. 480m 짜리 껍데기가 그림자를
            // 드리우면 그림자 지도가 그만큼 늘어나 해상도만 깎아먹는다.
            draws[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            draws[i].receiveShadows = false;
        }

        // ⚠ 콜라이더는 반드시 지운다. 갑판을 재는 레이가 하늘에 맞으면
        //    배치 도구가 엉뚱한 높이를 읽는다.
        Collider[] bumps = made.GetComponentsInChildren<Collider>();

        for (int i = 0; i < bumps.Length; i++)
        {
            Object.DestroyImmediate(bumps[i]);
        }

        made.name = prefab.name;

        return made.transform;
    }

    // 로비의 Light_Sun 과 같은 해를 만든다. 없으면 씬의 방향광을 고친다.
    private static void MatchLobbySun(StringBuilder log)
    {
        Light sun = null;

        foreach (Light found in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
        {
            if (found.type == LightType.Directional)
            {
                sun = found;
                break;
            }
        }

        if (sun == null)
        {
            log.AppendLine("  ⚠ 방향광이 없어서 해를 못 맞췄습니다");
            return;
        }

        Undo.RecordObject(sun, "배 모델과 갑판 배치");
        Undo.RecordObject(sun.transform, "배 모델과 갑판 배치");

        sun.color = new Color(1f, 0.9436667f, 0.87f, 1f);
        sun.intensity = 2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(27.938f, -46.273f, -89.244f);

        RenderSettings.sun = sun;

        log.AppendLine($"  해({sun.name})를 로비와 같은 각도·세기로 맞췄습니다");
    }

    /// <summary>
    /// 바다 판 한 장.
    ///
    /// ⚠ **판을 돌려서 물이 흐르는 방향을 정합니다.** (`SeaFlowYaw`)
    ///
    ///    물 무늬가 흐르는 방향은 셰이더 그래프에 박혀 있어서 값으로 못 바꿉니다.
    ///    그냥 두면 **오른쪽에서 왼쪽으로** 흘러서, 배가 앞으로 가는 게 아니라
    ///    옆으로 미끄러지는 것처럼 보입니다.
    ///
    ///    그런데 무늬는 **판을 따라 돕니다.** (얼려놓고 90도 돌려서 확인했습니다)
    ///    그래서 판을 돌리면 흐르는 방향도 같이 돕니다.
    ///
    /// ⚠ **격자는 가로세로를 바꿔서 만듭니다.** 판을 90도 돌릴 거라,
    ///    돌리고 나서 월드에서 1150 × 1750m 가 되려면 격자는 1750 × 1150 이어야
    ///    합니다. 안 그러면 앞뒤가 모자라서 항해 끝에 바다가 사라집니다.
    /// </summary>
    private static Transform MakeSeaTile(Transform group, string name, Material paint, Vector3 at)
    {
        bool turned = Mathf.Abs(Mathf.Sin(SeaFlowYaw * Mathf.Deg2Rad)) > 0.5f;

        Mesh grid = turned
            ? ShipCoopSeaMesh.GetOrCreate(SeaLength, SeaWidth)
            : ShipCoopSeaMesh.GetOrCreate(SeaWidth, SeaLength);

        GameObject made = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        Undo.RegisterCreatedObjectUndo(made, "배 모델과 갑판 배치");

        made.GetComponent<MeshFilter>().sharedMesh = grid;
        made.GetComponent<MeshRenderer>().sharedMaterial = paint;

        made.transform.SetParent(group, false);
        made.transform.localPosition = at;
        made.transform.localRotation = Quaternion.Euler(0f, SeaFlowYaw, 0f);
        made.transform.localScale = Vector3.one;

        return made.transform;
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

    // ------------------------------------------------------------
    // 드는 자세 — **머리 위가 아니라 두 팔로 안게** 한다
    //
    // ⚠ 캐릭터 컨트롤러에 **드는 동작 클립이 없습니다.** 걷기 · 달리기 ·
    //    점프 · 공격뿐입니다. 그래서 클립 대신 IK 로 손을 끌어다 씁니다.
    //
    // ⚠ **애니메이터 레이어의 IK Pass 를 켜야** `OnAnimatorIK` 가 불립니다.
    //    꺼져 있으면 스크립트가 붙어 있어도 아무 일도 안 일어납니다.
    //    ithappy 컨트롤러를 건드리는 것이라 `ASSETS.md` 에 적어 두었습니다.
    //    IK Pass 만으로는 화면이 안 바뀝니다. 쓰는 스크립트가 있어야 바뀝니다.
    // ------------------------------------------------------------

    /// <summary>드는 자세를 붙이고, 애니메이터의 IK Pass 를 켠다.</summary>
    private static void SetUpCarryPose(StringBuilder log)
    {
        int posed = 0;
        int opened = 0;

        foreach (CarryTask carry in Object.FindObjectsByType<CarryTask>(FindObjectsInactive.Include,
                                                                       FindObjectsSortMode.None))
        {
            Animator animator = carry.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                log.AppendLine($"  ⚠ {carry.name} 에 Animator 가 없어 드는 자세를 못 만듭니다");
                continue;
            }

            if (animator.GetComponent<ShipCoopCarryPose>() == null)
            {
                Undo.AddComponent<ShipCoopCarryPose>(animator.gameObject);
                posed++;
            }

            if (TurnOnIkPass(animator))
            {
                opened++;
            }
        }

        log.AppendLine($"  드는 자세를 {posed}명에게 붙였습니다 (IK Pass {opened}개 켬)");
    }

    // 애니메이터 레이어의 IK Pass 를 켠다. 이미 켜져 있으면 아무 일도 안 한다.
    private static bool TurnOnIkPass(Animator animator)
    {
        var controller = animator.runtimeAnimatorController
            as UnityEditor.Animations.AnimatorController;

        if (controller == null)
        {
            return false;
        }

        bool changed = false;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].iKPass)
            {
                continue;
            }

            // ⚠ layers 는 복사본을 돌려줍니다. 고쳐서 **다시 넣어야** 남습니다.
            var layers = controller.layers;
            layers[i].iKPass = true;
            controller.layers = layers;

            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        return changed;
    }

    // ------------------------------------------------------------
    // 프로필 사진 — **각자의 캐릭터를 찍어서 씁니다**
    //
    // ⚠ 그림 한 장을 박아두면 4명이 전부 같은 얼굴이 됩니다. 사람을 구분하라고
    //    만든 칸인데 구분이 안 됩니다. 자세한 것은 `ShipCoopPortrait` 참고.
    //
    // ⚠ **전용 레이어가 필요합니다.** 촬영용 카메라가 그 사람만 찍어야 하는데,
    //    레이어가 없으면 촬영장 전체가 한 장에 들어옵니다.
    //    없으면 여기서 만들어 줍니다. (비어 있는 첫 칸을 씁니다)
    // ------------------------------------------------------------

    /// <summary>프로필 사진 찍는 것을 붙이고, 전용 레이어를 만든다.</summary>
    private static void SetUpPortraits(StringBuilder log)
    {
        int layer = MakeLayerIfMissing(ShipCoopPortrait.PortraitLayerName, log);

        if (layer < 0)
        {
            log.AppendLine("  ⚠ 레이어가 꽉 차서 프로필 사진을 못 찍습니다");
            return;
        }

        ShipCoopHud hud = Object.FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);

        if (hud == null)
        {
            log.AppendLine("  ⚠ HUD 를 못 찾아서 프로필 사진을 못 붙였습니다");
            return;
        }

        if (hud.GetComponent<ShipCoopPortrait>() == null)
        {
            Undo.AddComponent<ShipCoopPortrait>(hud.gameObject);
            log.AppendLine($"  프로필 사진을 찍도록 ShipCoopPortrait 를 붙였습니다 " +
                           $"(레이어 {layer} {ShipCoopPortrait.PortraitLayerName})");
        }
    }

    // 이름이 같은 레이어가 있으면 그 번호를, 없으면 빈 칸에 만들어서 돌려준다.
    private static int MakeLayerIfMissing(string name, StringBuilder log)
    {
        int found = LayerMask.NameToLayer(name);

        if (found >= 0)
        {
            return found;
        }

        SerializedObject tags = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layers = tags.FindProperty("layers");

        // 0~7 은 유니티가 쓰는 자리다. 8 부터 본다.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);

            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                continue;
            }

            slot.stringValue = name;
            tags.ApplyModifiedProperties();

            log.AppendLine($"  레이어 {i} 를 '{name}' 으로 만들었습니다");
            return i;
        }

        return -1;
    }

    // ------------------------------------------------------------
    // 암초 — **로비가 쓰는 진짜 바위**로 띄운다
    //
    // ⚠ 회색 큐브였습니다. 바다에 네모가 떠 있으면 암초로 안 읽힙니다.
    //
    // 네 개를 넣고 매번 다른 것을 띄웁니다. 하나만 쓰면 두 번째부터
    // "아, 그거" 가 되어 긴장이 사라집니다.
    //
    // ⚠ **크기가 다른 것으로 고릅니다.** 아픈 정도가 폭에 비례하므로
    //    (`Reef.normalWidth`), 크기가 비슷하면 그 장치가 아무 일도 안 합니다.
    // ------------------------------------------------------------

    private static readonly string[] ReefRockPaths =
    {
        // 16.2m — 뾰족하게 솟은 것. 제일 크고 제일 아프다
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Rock_Spikes_02.prefab",

        // 11.8m — 덩어리
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Rock_Cliff_02.prefab",

        // 9.9m — 낮게 쌓인 것
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Rock_Pile_07.prefab",

        // 9.6m — 작게 뾰족한 것
        "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Env_Rock_Spikes_01.prefab",
    };

    /// <summary>
    /// 조타 1도당 배가 옆으로 비켜서는 거리 (m).
    ///
    /// ⚠ **`safeGap` 과 짝입니다.** 한쪽만 바꾸면 난이도가 소리 없이 달라집니다.
    ///
    /// 0.08 이었을 때는 끝까지 꺾어도 4.8m 라, 조타수 화면에서 항로선이
    /// 67px(화면의 3.5%)밖에 안 움직였습니다. **배가 옆으로 가는 것이
    /// 안 보였습니다.** 갑판 폭이 8m 인데 그 절반도 안 비켜선 것입니다.
    /// </summary>
    private const float LateralPerDegree = 0.2f;

    /// <summary>암초에 진짜 바위들을 넣고, 뱃머리 자리를 재어 넣는다.</summary>
    private static void SetUpReefRocks(StringBuilder log)
    {
        Reef reef = Object.FindAnyObjectByType<Reef>(FindObjectsInactive.Include);

        if (reef == null)
        {
            log.AppendLine("  ⚠ 암초 사건을 못 찾았습니다");
            return;
        }

        SerializedObject so = new SerializedObject(reef);
        SerializedProperty list = so.FindProperty("rockPrefabs");

        list.arraySize = ReefRockPaths.Length;
        int found = 0;

        for (int i = 0; i < ReefRockPaths.Length; i++)
        {
            GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>(ReefRockPaths[i]);

            if (rock == null)
            {
                log.AppendLine($"  ⚠ 바위를 못 찾음: {ReefRockPaths[i]}");
            }
            else
            {
                found++;
            }

            list.GetArrayElementAtIndex(i).objectReferenceValue = rock;
        }

        // ⚠ 배의 앞뒤 끝은 **재서** 넣습니다. 배 모델을 바꿔도 따라가야 합니다.
        //    (Reef 는 실행 중에도 스스로 재지만, 배를 못 찾을 때 쓸 값입니다)
        //
        // ⚠ **선체 기준입니다.** 배 전체를 재면 z 34.3 인데 그건 앞으로 튀어나온
        //    삭구와 이물장식 끝입니다. 선체는 27 에서 끝납니다. 그 차이만큼
        //    바위가 뱃머리 한참 앞에서 "닿았다" 가 났습니다.
        Vector2 ends = MeasureHullEnds();
        so.FindProperty("fallbackBowZ").floatValue = ends.y;
        so.FindProperty("fallbackSternZ").floatValue = ends.x;

        // ⚠ 암초는 배를 **다 지나가야** 판정이 끝납니다. 지속 시간이 짧으면
        //    바위가 배 옆구리에 걸친 채 사건이 끝납니다. (`VoyageSea.passDistance`)
        so.FindProperty("duration").floatValue = 10.5f;

        // ⛔ **판정 범위는 보이는 범위입니다.** 고정 간격(`safeGap`)을 쓰던 것을
        //    선체 반폭 + 바위 반폭으로 바꿨습니다. 12m 옆을 지나가는데 부딪혔다고
        //    뜨던 것이 그 고정값 때문이었습니다. 자세한 것은 `Reef` 주석 참고.
        //
        //    가만히 있을 때 바위 폭의 30% 만큼 선체를 파고들게 놓습니다.
        //    그만큼 비키면 피합니다. **바위가 클수록 더 비켜야 합니다.**
        //
        //      바위폭   선체에 닿는 간격   놓는 자리   비켜야 하는 거리   조타각
        //       9.6m         10.4m          7.5m          2.9m          14도
        //      16.2m         13.7m          8.9m          4.9m          24도
        //      19.0m         15.1m          9.4m          5.7m          29도
        so.FindProperty("grazeShare").floatValue = 0.3f;
        so.FindProperty("fallbackShipHalfWidth").floatValue = MeasureShipHalfWidth();

        so.ApplyModifiedProperties();

        // ⚠ **짝이 되는 값이라 같은 자리에서 함께 씁니다.** 따로 두면 한쪽만
        //    바뀌어서 난이도가 소리 없이 달라집니다. (위 계산 참고)
        VoyageSea sea = Object.FindAnyObjectByType<VoyageSea>(FindObjectsInactive.Include);

        if (sea != null)
        {
            SerializedObject seaSo = new SerializedObject(sea);
            seaSo.FindProperty("lateralPerDegree").floatValue = LateralPerDegree;

            // ⚠ 항로 폭도 같이 키웁니다. **안 키우면 항로가 2.5배 좁아집니다.**
            //    노란 선은 "이 안에 있으면 제대로 가고 있다" 는 표시입니다.
            //    0.08 일 때 ±2m 는 조타 ±25도였는데, 0.2 로 바꾸면 ±10도가
            //    됩니다. 조타를 조금만 건드려도 항로 밖으로 나가 버립니다.
            //    ±5m 로 두면 다시 ±25도가 됩니다.
            seaSo.FindProperty("courseHalfWidth").floatValue = LateralPerDegree * 25f;

            // ⚠ 0 이면 휠을 돌리는 즉시 배가 **평행으로 미끄러집니다.**
            //    뱃머리가 도는 시간(followSeconds 0.8)보다 커야 회전이 먼저 보입니다.
            seaSo.FindProperty("lateralLagSeconds").floatValue = 1.2f;

            // ⚠ 바위가 **배 뒤끝보다 더 뒤까지** 가야 판정이 끝납니다.
            //    12m 면 큰 바위 뒷면이 z −4 라 배 옆구리에 걸친 채 멈춥니다.
            seaSo.FindProperty("passDistance").floatValue = 34f;

            // ⚠ 수평선이 **뱃머리(z 34.3)보다 한참 멀어야** 반응할 시간이 생깁니다.
            //    60m 면 바위가 2.3초 만에 뱃머리에 닿아서, 즉시 꺾어도 못 피합니다.
            //    100m 면 5.7초가 되어 반응할 시간이 3.75초 생깁니다.
            seaSo.FindProperty("horizonDistance").floatValue = 100f;
            seaSo.ApplyModifiedProperties();

            log.AppendLine($"  조타 1도당 옆으로 {LateralPerDegree}m 비켜서게 했습니다 " +
                           $"(끝까지 꺾으면 {LateralPerDegree * 60f:F1}m)");
        }

        log.AppendLine($"  암초에 바위 {found}종을 넣고 뱃머리를 " +
                       $"z {so.FindProperty("fallbackBowZ").floatValue:F1} 로 잡았습니다");
    }

    // 배의 앞 끝 z. 못 찾으면 적어둔 값.
    private static float MeasureBowZ()
    {
        GameObject ship = GameObject.Find(ShipName);

        if (ship == null)
        {
            return ShipBowZ;
        }

        Renderer[] draws = ship.GetComponentsInChildren<Renderer>();

        if (draws.Length == 0)
        {
            return ShipBowZ;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        return box.max.z;
    }

    /// <summary>
    /// 선체 반폭 (m). 암초가 여기 닿으면 부딪힌 것이다.
    ///
    /// ⚠ **돛대와 삭구는 뺍니다.** 배 전체를 재면 7.21m 인데 그건 돛대가 옆으로
    ///    뻗은 것까지 센 값입니다. 그것들은 물 위 한참 높이라 바위에 안 닿습니다.
    ///    선체·갑판만 재면 **5.60m**, 눈에 보이는 물가 폭이 이쪽입니다.
    /// </summary>
    private static float MeasureShipHalfWidth()
    {
        Bounds hull = MeasureHull(out bool any);

        return any ? hull.size.x * 0.5f : 5.6f;
    }

    /// <summary>선체의 뒤끝 z(x)와 앞끝 z(y). 삭구는 뺀 진짜 배 끝이다.</summary>
    private static Vector2 MeasureHullEnds()
    {
        Bounds hull = MeasureHull(out bool any);

        return any ? new Vector2(hull.min.z, hull.max.z) : new Vector2(ShipSternZ, ShipBowZ);
    }

    /// <summary>선체와 갑판만 감싸는 상자. 돛대·삭구는 뺀다.</summary>
    private static Bounds MeasureHull(out bool any)
    {
        GameObject ship = GameObject.Find(ShipName);
        Bounds hull = new Bounds();
        any = false;

        if (ship == null)
        {
            return hull;
        }

        foreach (Renderer draw in ship.GetComponentsInChildren<Renderer>())
        {
            if (!draw.name.StartsWith("Hull") && !draw.name.StartsWith("Deck"))
            {
                continue;
            }

            if (!any)
            {
                hull = draw.bounds;
                any = true;
            }
            else
            {
                hull.Encapsulate(draw.bounds);
            }
        }

        return hull;
    }

    // ------------------------------------------------------------
    // 들고 있는 표시의 **크기**만 정한다
    //
    // ⚠ 자리는 여기서 안 잡습니다. 손에 들려야 하므로 `ShipCoopCarryPose` 가
    //    매 프레임 두 손 사이로 옮깁니다.
    //
    //    한동안 여기서 **머리 위에** 올려뒀습니다. 보이기는 하는데 팔은
    //    가만히 있고 물건만 떠다녀서 드는 것으로 안 읽혔습니다.
    // ------------------------------------------------------------

    /// <summary>들고 있는 표시의 한 변 (m). 멀리서도 보여야 한다.</summary>
    private const float HeldSize = 0.6f;

    /// <summary>들고 있는 표시의 크기를 정한다.</summary>
    private static void SizeHeldVisual(StringBuilder log)
    {
        int sized = 0;

        foreach (CarryTask carry in Object.FindObjectsByType<CarryTask>(FindObjectsInactive.Include,
                                                                       FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(carry);

            if (so.FindProperty("heldVisual").objectReferenceValue is not GameObject held)
            {
                log.AppendLine($"  ⚠ {carry.name} 에 들고 있는 표시가 비어 있습니다");
                continue;
            }

            Undo.RecordObject(held.transform, "배 모델과 갑판 배치");
            held.transform.localScale = Vector3.one * HeldSize;
            sized++;
        }

        log.AppendLine($"  들고 있는 표시 {sized}개를 {HeldSize:F2}m 로 맞췄습니다");
    }
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

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Warriors;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 📐 <b>무쌍 HUD 를 한 세트로 맞춘다.</b> 판 그림 하나 · 규격 하나.
    ///
    /// <b>지금까지 왜 제각각이었나.</b> 세 가지가 겹쳤다.
    ///
    /// 1. <b>금테가 없는 판을 썼다.</b> <c>crew-panel</c> 은 픽셀을 재 보니 금색이 0px 다.
    ///    투명 → 회청색 → 남색으로 바로 간다. 그것을 정보 칸과 카드에 썼으니
    ///    "테두리가 실처럼 얇다" 가 될 수밖에 없었다.
    ///
    /// 2. <b>통짜 그림을 비율로 줄였다.</b> 그러면 금테도 같이 줄어든다.
    ///    상단 바(1024→660, 0.65배)만 두껍게 남고 카드(0.24배)는 사라지다시피 했다.
    ///
    /// 3. <b>판마다 글자 크기와 여백이 달랐다.</b> 옛 배치 값이 그대로 남아 있었다.
    ///
    /// <b>그래서 이렇게 한다.</b>
    ///
    /// <code>
    ///   판 그림   Panel_Gold.png 하나. 배의 voyage-panel 에서 금테만 남기고 안을 비웠다
    ///   그리기    9-슬라이스. 어떤 크기로 늘려도 금테 두께가 같다
    ///   규격      여백 · 글자 크기를 이 파일 맨 위 상수 한 곳에서 정한다
    ///   상단 3판  같은 높이 · 같은 윗선
    /// </code>
    ///
    /// ⚠ 3라운드 노트와 판정선은 건드리지 않는다. 링 두께 7px 같은 별도 규격이 있다.
    /// </summary>
    public static class WarriorsHudLayout
    {
        private const string HudPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/UI/WarriorsHUD.prefab";

        // 코드로 생성한 캡슐 판(WarriorsPanelImport 주석 참고). 원본 Panel_Gold.png 는 남겨 둔다.
        private const string PanelSprite =
            "Assets/Game/Art/MiniGames/Warriors/UI/Panel_GoldOrnate.png";

        /// <summary>
        /// 회색 테 판 — <b>배 게임 것을 그대로 쓴다.</b>
        ///
        /// 새로 그리지 않는다. 배 협동의 V2 판 세트에 이미 "남색 바탕 + 강철색 테" 한 벌이
        /// 있고(<c>panel-team-*</c>), 그것이 금테 판과 같은 그림 계열이라 나란히 놓아도
        /// 따로 놀지 않는다. 금색 선만 강철색으로 바뀐 형제 에셋이다.
        ///
        /// 두 장을 겹쳐 쓴다 — 바탕을 깔고 테두리를 맨 위에 얹는다.
        /// </summary>
        private const string PlainBack =
            "Assets/Game/Art/UI/ShipCoopHudV2/panel-team-background.png";

        private const string PlainEdgeArt =
            "Assets/Game/Art/UI/ShipCoopHudV2/panel-team-frame.png";

        /// <summary>배 게임 프로필 초상화 에셋. 바탕(짙은 청록) + 사람별 색 테.</summary>
        private const string PortraitBack = "Assets/Game/Art/UI/ShipCoopHudV2/portrait-backplate.png";
        private const string PortraitFrameRed = "Assets/Game/Art/UI/ShipCoopHudV2/portrait-frame-red.png";
        private const string PortraitFrameYellow = "Assets/Game/Art/UI/ShipCoopHudV2/portrait-frame-yellow.png";

        private const string KrakenIcon =
            "Assets/Game/Art/MiniGames/Warriors/UI/Icons/Icon_Kraken.png";

        private static readonly string[] Untouchable = { "NoteTrack" };

        // ── 공통 규격 (1920×1080 기준) ────────────────────────────
        //
        // 값을 여기 한 곳에 모은다. 흩어 두면 판마다 조금씩 어긋나고,
        // 그 어긋남이 쌓여 "각자 다른 프리팹처럼" 보인다.

        /// <summary>금테가 화면에 그려지는 두께. 좌우 44/1.4 · 상하 28/1.4 를 잰 값이다.</summary>
        private const float FrameX = 29f;

        private const float FrameY = 19f;

        /// <summary>글자를 금테에서 떼어 놓는 여백. 금테 위에 여유 한 칸.</summary>
        /// <summary>
        /// ⚠ <b>금테 판은 높이마다 배율이 다르다.</b> <see cref="PanelPpu"/> 가 판 높이로 배율을 정하므로
        ///    금테 두께도 판마다 다르고, 안쪽 여백은 그 판의 크기로 계산해야 한다. 상수 PadX/PadY 는
        ///    배율 1.5 시절 값이라 이제 이 함수들을 쓴다.
        /// </summary>
        private static float PadXFor(Vector2 size) => GoldBandX / PanelPpu(size) + 12f;

        private static float PadYFor(Vector2 size) => GoldBandY / PanelPpu(size) + 6f;

        /// <summary>
        /// Panel_GoldCapsule.png 의 테 두께(원본 px). 위아래는 14 (테 2 + 금 9 + 그림자 3).
        /// 양 끝은 반원이라 글자 줄 높이(중심에서 ±37px)에서는 남색이 끝에서 28px 부터 시작한다 —
        /// 그 값을 가로 여백 기준으로 쓴다.
        /// </summary>
        // Panel_GoldOrnate(결과 창 금테 절반 크기): 장식 띠가 안쪽 남색까지 약 20px. 둥근 사각형이라
        // 가로·세로 같다.
        // 금띠 + 구분선이 끝나는 곳(원본 px) — 실측 44. 둥근 사각형이라 가로·세로 같다.
        private const float GoldBandX = 44f;
        private const float GoldBandY = 44f;

        /// <summary>Panel_Gold.png 원본 높이(px). 이 높이로 그릴 때 양 끝 반원이 정확한 원이다.</summary>
        private const float PanelSourceHeight = 140f;

        /// <summary>
        /// 이 판을 그릴 배율. <b>판 높이 = 원본 높이</b>가 되게 잡아 캡슐 양 끝이 원으로 남는다.
        ///
        /// 배율 1.5 고정이면 원본이 93px 로 그려지는데 상단 칸은 124 라 반원이 세로로 1.33배
        /// 늘어나고(실측 지적 "동그라미가 길쭉"), 남색 면이 금테 곡률보다 위아래로 길게 보였다
        /// ("남색 칸 세로가 길다"). 높이에 맞추면 남색 면은 124 - 2×23 = 78 로 줄고 모양이 맞는다.
        /// </summary>
        // 장식 금테(둥근 사각형)는 캡슐이 아니라 높이에 배율을 맞출 필요가 없다. 절반 크기 그림을
        // 1:1 로 그리면 띠 20px · 모서리 반경 ~24px — 124 칸부터 78 칸까지 같은 두께로 보인다.
        // 원본 띠 44px 는 124 칸에 너무 굵다. 2 배로 그리면 띠 22px · 모서리 타일 24px · 반경 ≈10 —
        // 124 칸 안쪽 반높이 34 (1행 33 · 2행 33 이 들어간다), 96 칸도 48 < 96 으로 무사하다.
        private static float PanelPpu(Vector2 size) => 2f;

        private const float PadX = FrameX + 12f;   // 41 — 회색 판 등 배율 1 인 곳만 쓴다

        private const float PadY = FrameY + 6f;    // 26

        /// <summary>큰 숫자 — 남은 시간 · 점수. 가장 먼저 읽혀야 한다.</summary>
        private const float FontBig = 34f;

        /// <summary>
        /// 가운데 카드의 주 정보(처치 수 · 보스 이름). 큰 숫자와 중간 사이 한 칸.
        ///
        /// 가운데 카드는 아래에 막대가 붙어 두 줄을 쓰므로 34 를 넣을 세로가 없다.
        /// 그렇다고 20 으로 내리면 화면에서 가장 중요한 진행도가 라운드 이름과 같아진다.
        /// </summary>
        private const float FontHead = 28f;

        /// <summary>중간 — 라운드 이름 · 처치 수 · HP 숫자.</summary>
        private const float FontMid = 20f;

        /// <summary>본문 — 안내문 · 몬스터 이름.</summary>
        private const float FontBody = 18f;

        /// <summary>라벨 — TIME · SCORE · COMBO 같은 작은 머리말.</summary>
        private const float FontLabel = 14f;

        /// <summary>
        /// 키캡(J · K · L) 한 변.
        ///
        /// ⚠ <b>키캡은 보조 정보다.</b> 흰 바탕에 검은 글자로 두었더니 화면에서 대비가 가장
        ///    세서 <b>몬스터 그림보다 J 가 먼저 보였다.</b> 남색 바탕 · 얇은 금테 · 흰 글자로
        ///    바꾸고 38 → 30 (21% 축소) 으로 줄여 순서를 몬스터명 → 공격명 → 키 로 되돌린다.
        /// </summary>
        private const float KeyCap = 32f;

        /// <summary>키캡 금테 두께(px). 이보다 두꺼우면 다시 눈에 띈다.</summary>
        private const float KeyCapEdge = 2f;

        /// <summary>키캡 바탕. 판보다 한 단계 어두운 남색이라 글자만 떠 보인다.</summary>
        // ⚠ 회색 판 그림은 남색 PNG 라 흰 tint 를 줘도 남색이었다 — 검은 글자가 안 보였던 이유.
        //    키는 Unity 내장 흰 스프라이트(UISprite)에 색을 곱해 그린다. 어두운 윗면 + **흰 글자**.
        private static readonly Color KeyCapFill = new Color(.07f, .11f, .21f, 1f);
        private static readonly Color KeyLetter = new Color(1f, 1f, 1f, 1f);

        /// <summary>키캡 테두리. 판의 금테와 같은 계열이되 채도를 낮췄다.</summary>
        private static readonly Color KeyCapBorder = new Color(.78f, .82f, .90f, 1f);

        /// <summary>키 옆면(아래 3px). 윗면보다 더 어두워야 키가 떠 보인다.</summary>
        private static readonly Color KeyCapSide = new Color(.02f, .04f, .10f, 1f);

        /// <summary>
        /// 키캡에 쓰는 회색 판 바탕의 배율. 원본 모서리 반경 12 · 9-슬라이스 경계 32 를
        /// 6 배로 줄이면 반경 2px · 경계 5px — 32px 키에서 거의 각진 평면 키로 보인다.
        /// </summary>
        private const float KeyCornerPpu = 6f;

        /// <summary>공격 이름과 키캡 사이. 둘이 붙으면 한 덩어리로 읽힌다.</summary>
        private const float LabelGap = 12f;

        /// <summary>상단 세 판이 화면 위에서 같은 거리에 앉는다.</summary>
        private const float TopMargin = 36f;

        /// <summary>상단 판 높이. 셋이 같아야 한 줄로 읽힌다.</summary>
        private const float TopH = 124f;

        /// <summary>화면 좌우 여백.</summary>
        private const float SideMargin = 40f;

        /// <summary>
        /// 하단 카드 줄이 화면 바닥에서 떨어지는 거리.
        ///
        /// 프리팹에는 34 로 박혀 있었는데 화면에서 카드가 바닥에 붙어 답답했다.
        /// 위쪽 여백(36)과 비슷하게 두면 위아래가 한 짝으로 읽힌다.
        /// </summary>
        private const float BottomMargin = 56f;

        [MenuItem("Tools/아라아띠/Warriors HUD 칸·글자 다시 잡기")]
        public static void Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);

            if (root == null)
            {
                Debug.LogError($"[HUD 배치] 프리팹을 열지 못했습니다 — {HudPrefabPath}");
                return;
            }

            try
            {
                RoundCards(root);
                StatusCards(root);
                Progress(root, "PhaseProgress", "Value", "ProgressBar");
                Progress(root, "BossCard", "BossStatus", "BossHP");
                Notices(root);
                Cards(root, "AttackGuide", new[] { "Card_1", "Card_2", "Card_3" }, false);
                Cards(root, "MonsterGuide", new[] { "MCard_1", "MCard_2", "MCard_3" }, true);
                RoundIntro(root);
                TeamPanel(root);

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Verify();
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------
        // 상단 왼쪽 — 라운드 · 남은 시간
        // ------------------------------------------------------------

        private static void RoundCards(GameObject root)
        {
            Vector2 size = new Vector2(360f, TopH);

            foreach (RectTransform card in All(root, "RoundCard"))
            {
                Corner(card, new Vector2(0f, 1f), new Vector2(SideMargin, -TopMargin), size);
                Panel(card, size);

                // 1행 라운드 이름 · 2행 TIME + 큰 숫자.
                // 시간이 라운드 이름보다 커야 한다 — 플레이 중에 계속 봐야 하는 것은 숫자다.
                float halfW = InnerHalfW(size);

                // 라운드 이름은 아래 줄 TIME 라벨과 **왼쪽 선을 맞춘다** (실측 지적 — 가운데 정렬이면
                // "ROUND 3 · 크라켄의 공격" 이 TIME 과 어긋나 두 줄이 따로 놀았다).
                Put(card, "RoundStatus", new Vector2(0f, Row1Y), new Vector2(halfW * 2f, Row1H),
                    13f, FontMid, TextAlignmentOptions.Left);

                // **TIME 라벨 바로 뒤에 숫자.** 양 끝에 갈라 두었더니 "TIME ...... 04:00" 으로
                // 사이가 비어 라벨과 숫자가 한 덩어리로 안 읽혔다(실측 지적). 라벨(60) 뒤 6px 에서
                // 숫자가 시작하고 왼쪽 정렬 — 숫자가 바뀌어도 라벨 옆에 붙어 있다.
                Put(card, "TimeLabel", LeftIn(halfW, 60f, Row2Y), new Vector2(60f, 24f),
                    11f, FontLabel, TextAlignmentOptions.Left);

                float timeX = -halfW + 60f + 6f;
                float timeW = halfW - timeX;
                Put(card, "Time", new Vector2(timeX + timeW * 0.5f, Row2Y), new Vector2(timeW, Row2H),
                    20f, FontBig, TextAlignmentOptions.Left);

                Debug.Log($"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0}");
            }
        }

        // ------------------------------------------------------------
        // 상단 오른쪽 — HP · 점수 · 콤보
        // ------------------------------------------------------------

        /// <summary>
        /// HP 는 크게, 점수와 콤보는 보조로.
        ///
        /// ⚠ <b>세 줄이 아니라 두 줄이다.</b> 상단 세 판의 높이를 맞추려면 124 인데,
        ///    금테와 여백(위아래 26)을 빼면 쓸 수 있는 세로가 72 뿐이다. 세 줄을 넣으면
        ///    한 줄당 24 도 안 되어 글자가 금테에 닿는다. 점수와 콤보를 한 줄에 나눠 놓으면
        ///    위계도 살고 높이도 맞는다.
        /// </summary>
        private static void StatusCards(GameObject root)
        {
            Vector2 size = new Vector2(380f, TopH);

            foreach (RectTransform card in All(root, "HPCard").Concat(All(root, "ScoreCard")))
            {
                Corner(card, new Vector2(1f, 1f), new Vector2(-SideMargin, -TopMargin), size);
                Panel(card, size);

                float halfW = InnerHalfW(size);

                // 1행 — [HP 100] 바로 뒤에서 막대가 시작해 안쪽 오른콝 끝까지. 양 끝에 갈라 두면
                // 글자와 막대 사이가 비어 따로 놀았다(실측 지적). 라벨 폭 88 은 "HP  100" 20pt 기준.
                const float hpLabelW = 88f;
                Put(card, "HP", LeftIn(halfW, hpLabelW, Row1Y), new Vector2(hpLabelW, Row1H),
                    13f, FontMid, TextAlignmentOptions.Left);

                float barX = -halfW + hpLabelW + 8f;
                float barW = halfW - barX;
                Bar(card, "HPBar", new Vector2(barX + barW * 0.5f, Row1Y), new Vector2(barW, 16f));

                // 2행 — 왼쪽에 [SCORE 라벨][큰 숫자], 오른쪽 끝에 콤보.
                //
                // ⚠ 예전에는 콤보가 @116 · 140폭 이라 오른쪽으로 186 까지 갔다.
                //    안쪽 한계가 146 이므로 40px 이 금테에 물려 글자가 잘렸다.
                Put(card, "ScoreLabel", LeftIn(halfW, 60f, Row2Y), new Vector2(60f, 22f),
                    11f, FontLabel, TextAlignmentOptions.Left);

                Vector2 scoreAt = new Vector2(-halfW + 60f + 8f + 45f, Row2Y);

                Put(card, "Score", scoreAt, new Vector2(90f, Row2H),
                    18f, FontBig, TextAlignmentOptions.Left);

                Put(card, "ScoreStatus", scoreAt, new Vector2(90f, Row2H),
                    18f, FontBig, TextAlignmentOptions.Left);

                Put(card, "Combo", RightIn(halfW, 130f, Row2Y), new Vector2(130f, 24f),
                    11f, FontMid, TextAlignmentOptions.Right);

                Hide(card, "Divider");

                Debug.Log($"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0}");
            }
        }

        // ------------------------------------------------------------
        // 상단 가운데 — 목표 진행
        // ------------------------------------------------------------

        private static void Progress(GameObject root, string cardName, string textName, string barName)
        {
            Vector2 size = new Vector2(660f, TopH);

            foreach (RectTransform card in All(root, cardName))
            {
                Corner(card, new Vector2(0.5f, 1f), new Vector2(0f, -TopMargin), size);
                Panel(card, size);

                float halfW = InnerHalfW(size);

                // ⚠ **글자와 막대는 판의 한가운데(x = 0)에 둔다.**
                //
                //    예전에는 문어 오른쪽부터 안쪽 오른쪽 끝까지를 다 썼다. 그러면 덩어리의
                //    중심이 +46 으로 밀려서, 판을 기준으로 보면 <b>글자가 오른쪽으로 치우쳐</b>
                //    보인다. "가운데정렬이 안 맞다" 는 지적이 이것이다.
                //
                //    오른쪽에도 문어와 같은 폭을 비워 두면 덩어리가 판의 한가운데에 선다.
                //    막대는 조금 짧아지지만 <b>가운데로 읽히는 것</b>이 더 중요하다.
                // **문어를 막대와 같은 줄에, 막대의 시작점에 둔다.**
                //
                // 예전에는 칸 한가운데 높이에 큼직하게(68) 얹혀 있어서 <b>막대와 따로 노는
                // 장식</b>으로 보였다. 막대와 같은 중심선에 놓고 바로 옆에서 막대가 시작하면
                // "문어가 끄는 막대" 한 덩어리로 읽힌다.
                // 문어와 막대는 <b>같은 중심선(y = BarRow)</b>에 서고 바로 붙는다.
                // 34 는 너무 작아 "옆에 붙은 아이콘" 으로 보였다. 막대 높이(24)보다
                // 눈에 띄게 크게(36) 잡아야 문어가 막대를 끄는 것처럼 읽힌다.
                // ⚠ **세 칸이 같은 행을 쓴다.** 프리팹 실측으로 이 칸만 1행 y=20 · 28pt, 2행 y=-18
                //    이었고, 좌·우 칸은 1행 y=23 · 20pt, 2행 y=-14 였다. 세 칸이 "제각각" 으로
                //    보인 실체가 이 3~4px 와 폰트 8pt 차이다. 좌·우 칸과 같은 Row1Y/Row2Y ·
                //    FontMid 로 맞춘다. 숫자를 크게 보이려면 좌·우 칸처럼 2행에 두는 것이 규칙이지만,
                //    이 칸의 2행은 막대가 차지하므로 글자는 1행 라벨 크기(20pt)로 간다.
                const float BarRow = Row2Y;
                float mark = 36f;
                float markX = -halfW + mark * 0.5f;
                float railLeft = markX + mark * 0.5f + 6f;
                float railW = halfW - railLeft;
                float railX = railLeft + railW * 0.5f;

                Kraken(card, new Vector2(markX, BarRow), mark);

                // 글자는 <b>칸 한가운데</b>다. 막대가 문어 때문에 오른쪽으로 밀려도 글자까지
                // 따라가면 판 기준으로 치우쳐 보인다.
                Put(card, textName, new Vector2(0f, Row1Y), new Vector2(halfW * 2f, Row1H),
                    13f, FontMid, TextAlignmentOptions.Center);

                Bar(card, barName, new Vector2(railX, BarRow), new Vector2(railW, 24f));

                Debug.Log(
                    $"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0} · " +
                    $"문어 {mark:F0} (막대와 같은 줄) · 막대 {railW:F0}");
            }
        }

        /// <summary>진행 바 왼쪽의 크라켄. 이 게임의 상대가 누구인지 한눈에 보이게.</summary>
        private static void Kraken(RectTransform card, Vector2 at, float size)
        {
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(KrakenIcon);

            if (art == null)
            {
                Debug.LogError($"[HUD 배치] 크라켄 아이콘을 찾지 못했습니다 — {KrakenIcon}");
                return;
            }

            RectTransform badge = Child(card, "KrakenBadge");
            Place(badge, at, new Vector2(size, size));
            badge.SetSiblingIndex(1);

            Image image = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        // ------------------------------------------------------------
        // 안내창
        // ------------------------------------------------------------

        /// <summary>
        /// **한 줄 안내는 한 줄 높이로.**
        ///
        /// 예전에는 한 줄짜리 글을 커다란 빈 박스에 담아 전투 화면을 가렸다.
        /// 글자 한 줄 + 위아래 여백이면 충분하다.
        /// </summary>
        private static void Notices(GameObject root)
        {
            // 목표 안내 — 진행 바 바로 아래, 화면 가운데. 슬림하게.
            foreach (RectTransform card in All(root, "Objective"))
            {
                // ⚠⚠ **여기에 레이아웃 그룹이 붙어 있었다. 그것이 모든 문제의 원인이었다.**
                //
                //    HorizontalLayoutGroup + ContentSizeFitter 가 붙어 있어서
                //    1) ContentSizeFitter 가 내가 넣은 폭(760)을 무시하고 글 길이에 맞춰 412 로 줄였고
                //    2) HorizontalLayoutGroup 이 <b>테두리(ThemeFrame)를 글자 옆 64×64 요소로</b>
                //       세워 놓았다 — 화면에서 "글자 옆 이상한 동그라미" 로 보이던 것이 이것이다
                //    3) 남은 폭이 모자라 글자가 "…" 로 잘렸다
                //
                //    프리팹을 아무리 봐도 안 보였던 이유는 이것이 <b>실행 중에만</b> 일어나기
                //    때문이다. 실행 중 화면을 찍어 보고서야 잡혔다(WarriorsHudRuntimeDump).
                //
                //    자동 폭 맞춤은 편하지만 여기서는 쓸 수 없다. 칸을 직접 정한다.
                Strip<HorizontalLayoutGroup>(card);
                Strip<VerticalLayoutGroup>(card);
                Strip<ContentSizeFitter>(card);

                // **자리: 왼쪽 위 타이머 칸 바로 아래.**
                // 가운데에 두면 전투 화면 한복판을 가린다. 라운드 · 시간과 한 덩어리로 읽히는
                // 자리가 맞다.
                // 회색 판은 금테가 없으므로 여백이 훨씬 적어도 된다.
                // 78 높이에 18pt 글자는 위아래가 휑했다. 52 로 낮추고 글자를 키운다.
                // ⚠ 500 은 한 줄 문장에 비해 길어서 <b>문장보다 박스가 먼저</b> 보였다.
                //    "몬스터 종류에 맞는 공격으로 막아내세요." 는 20pt 로 약 380px 이다.
                //    좌우 여백 14 씩이면 408 로 딱 맞는다. 좌상단 타이머 칸(360)과도
                //    비슷한 폭이라 왼쪽 묶음이 한 덩어리로 읽힌다.
                // ⚠ 폭 408 은 <b>초기값</b>일 뿐이다. 실행 중에는 WarriorsHudPresenter.FitObjectivePanel
                //    이 문장마다 TMP preferredWidth + 좌우 28 로 다시 잰다. 여기 숫자로 폭을
                //    맞추려 하지 말 것 — 문장이 바뀌면 또 어긋난다.
                // 글자는 한 단계 올린다(20 → 22). 42 높이에 20pt 는 위아래가 남았다.
                Vector2 size = new Vector2(408f, 28f + PlainPad);
                Corner(card, new Vector2(0f, 1f),
                       new Vector2(SideMargin, -(TopMargin + TopH + 14f)), size);
                Plain(card, size);

                // 가운데 정렬 — 폭이 문장에 맞춰지므로 좌우 여백이 같아야 한다.
                PutNoClip(card, "Text", Vector2.zero, new Vector2(size.x - PlainPad * 2f, 28f),
                    14f, FontMid + 2f, TextAlignmentOptions.Center);

                Debug.Log($"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0}");
            }

            // 라운드 전환 알림 — 화면 가운데. 잠깐 떴다 사라지므로 조금 크게.
            foreach (RectTransform card in All(root, "Announcement"))
            {
                Vector2 size = new Vector2(700f, 100f);  // 40 글자 + 금띠 22 + 여백 6 씩 (검사 기준 안쪽 반높이 22 ≥ 20)
                Place(card, new Vector2(0f, -150f), size);
                Panel(card, size);

                Put(card, "Text", Vector2.zero, new Vector2(size.x - PadXFor(size) * 2f, 40f),
                    14f, FontBig - 6f, TextAlignmentOptions.Center);

                Debug.Log($"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0}");
            }

            // 판정 글자 배경 칩은 끈다. 화면 한가운데에 글자 없는 금테 막대로만 보였다(실측).
            foreach (RectTransform card in All(root, "JudgementChip"))
            {
                card.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------
        // 하단 카드
        // ------------------------------------------------------------

        /// <summary>
        /// 하단 카드 세 장. <b>세 장이 같은 폭 · 같은 높이 · 같은 간격 · 같은 아랫선.</b>
        ///
        /// 안쪽 배치도 한 규칙이다 — 왼쪽 그림, 가운데 글, 오른쪽 위 키캡.
        /// </summary>
        private static void Cards(GameObject root, string groupName, string[] names, bool monster)
        {
            Vector2 size = monster ? new Vector2(340f, 116f) : new Vector2(300f, 96f);
            float gap = 24f;

            List<RectTransform> made = new List<RectTransform>();

            foreach (string n in names)
            {
                RectTransform card = All(root, n).FirstOrDefault();
                if (card == null) continue;

                Panel(card, size);

                float iconSize = Mathf.Min(size.y - PadYFor(size) * 2f, 72f);
                float iconX = -size.x * 0.5f + PadXFor(size) + iconSize * 0.5f;
                float textX = iconX + iconSize * 0.5f + 14f;

                // 키캡은 <b>공격명 바로 오른쪽</b>에 둔다. 안쪽 오른쪽 끝이 아니다.
                //
                // ⚠ 끝에 고정했을 때 "↔ 가로베기 .......... [J]" 로 빈 칸이 길어져 키캡이
                //    카드에 붙은 임시 표시(디버그 키)처럼 보였다. 실측 지적을 받았다.
                //
                //    그런데 글자 바로 뒤에 붙이면 카드마다 글자 길이가 달라(가로베기 4자 ·
                //    찌르기 3자) 세 장의 키캡 자리가 어긋난다. 그래서 공격명 칸을 <b>가장 긴
                //    공격명(4자 × FontBody 18 ≈ 72) + 여유 = 84</b> 의 <b>고정 폭</b>으로 두고,
                //    키캡을 그 칸 바로 뒤에 세운다. 세 장이 같은 X 에 서면서도 글자와 한 덩어리다.
                // **키캡은 카드 안쪽 오른쪽 위 구석.** (실측 지적 — 공격명 옆에 붙이니 글자 줄이
                // 붐볐다.) 구석에 두면 세 장이 같은 자리고, 글자 줄은 전부 글자에 쓴다.
                float halfW = InnerHalfW(size);
                float halfH = size.y * 0.5f - PadYFor(size);
                // 캡슐 판은 끝이 반원이라 구석 그대로 두면 키캡 모서리가 안쪽 곡선에 닿는다.
                // (340×116: 구석점이 안쪽 반경 46.4 와 정확히 같은 거리) 8·6 만 들이면 37.5 로 안에 든다.
                Vector2 keyAt = new Vector2(halfW - KeyCap * 0.5f - 8f, halfH - KeyCap * 0.5f - 6f);

                if (monster)
                {
                    Place(Find(card, "IconWell"), new Vector2(iconX, 0f), new Vector2(iconSize, iconSize));
                    Place(Find(card, "Icon"), new Vector2(iconX, 0f), new Vector2(iconSize - 6f, iconSize - 6f));

                    // 1행 몬스터명(크게) — 키캡 앞에서 멈춘다 · 2행 [기호][공격명] — 끝까지.
                    float nameW = (halfW - KeyCap - 8f) - textX;
                    Put(card, "Name", new Vector2(textX + nameW * 0.5f, 15f), new Vector2(nameW, 26f),
                        12f, FontMid, TextAlignmentOptions.Left);

                    Put(card, "Glyph", new Vector2(textX + 13f, -14f), new Vector2(26f, 24f),
                        12f, FontBody, TextAlignmentOptions.Center);

                    float attackX = textX + 26f + 8f;
                    float attackW = halfW - attackX;
                    Put(card, "Attack", new Vector2(attackX + attackW * 0.5f, -14f),
                        new Vector2(attackW, 24f), 11f, FontBody, TextAlignmentOptions.Left);

                    Badge(card, keyAt, KeyCap);
                }
                else
                {
                    // 공격 카드는 그림이 없다. 기호를 그림 자리에 크게 놓는다.
                    Put(card, "Glyph", new Vector2(iconX, 0f), new Vector2(iconSize, iconSize),
                        18f, FontBig - 4f, TextAlignmentOptions.Center);

                    float labelW = (halfW - KeyCap - 8f) - textX;
                    Put(card, "Label", new Vector2(textX + labelW * 0.5f, 0f), new Vector2(labelW, 28f),
                        12f, FontBody, TextAlignmentOptions.Left);

                    Badge(card, keyAt, KeyCap);
                }

                made.Add(card);
            }

            Spread(root, groupName, made, gap);
        }

        // ------------------------------------------------------------
        // 라운드 안내판 · 프로필
        // ------------------------------------------------------------

        private static void RoundIntro(GameObject root)
        {
            foreach (RectTransform card in All(root, "Card"))
            {
                if (card.parent == null || card.parent.name != "RoundIntro") continue;

                // ⚠ 금테 판(Panel_Gold, 1024×140)은 <b>캡슐</b> 모양이라 높이가 원본(93px)과
                //    다르면 양 끝 반원이 세로로 늘어난다. 240 높이에서는 2.6배 — "동그라미가
                //    길쭉하다" 는 실측 지적. 라운드 소개는 점수 정보가 아니라 잠깐 뜨는 안내이므로
                //    규칙상으로도 회색 판이 맞다(금테는 진행 정보에만). 회색 판은 모서리 반경 12 의
                //    둥근 사각형이라 어떤 크기로 늘려도 모양이 안 변한다.
                Vector2 size = new Vector2(920f, 240f);
                Plain(card, size);
                card.sizeDelta = size;

                Place(Find(card, "RoundBadge"), new Vector2(0f, 66f), new Vector2(220f, 48f));
                Put(card, "Title", new Vector2(0f, 4f), new Vector2(size.x - PadX * 2f, 52f),
                    20f, FontBig, TextAlignmentOptions.Center);
                Put(card, "Body", new Vector2(0f, -66f), new Vector2(size.x - PadX * 2f, 40f),
                    14f, FontMid, TextAlignmentOptions.Center);

                Debug.Log($"[HUD 배치] {Path(card)}  {size.x:F0}×{size.y:F0}");
            }
        }

        /// <summary>
        /// **참여 중인 두 사람.** 왼쪽 아래 — 전투를 가장 덜 가리는 자리.
        ///
        /// ⚠ 두 칸이다. 이 게임은 최대 2인이라 네 칸을 두면 오지 않을 사람을 기다리는
        ///    것처럼 보인다. 3·4번 칸은 지우지 않고 끈다 — 프레젠터가 배열로 물고 있다.
        /// </summary>
        /// <summary>
        /// 👥 <b>플레이어 프로필 두 칸. 세로로 쌓고, 금테는 쓰지 않는다.</b>
        ///
        /// <b>왜 가로에서 세로로 바꾸는가.</b> 가로 420 짜리 한 줄이 화면 아래를 가로지르면서
        /// 가운데의 몬스터 안내 카드(1068 폭)와 <b>34px 겹쳤다.</b> 세로로 쌓으면 폭이
        /// 절반으로 줄어 왼쪽 구석에 얌전히 들어간다.
        ///
        /// <b>왜 금테를 빼는가.</b> 프로필과 공격 카드가 둘 다 금테라 한 줄 전체가 같은 무게로
        /// 보였다. 금테는 <b>점수가 걸린 진행 정보</b>에만 남긴다. 프로필은 "상대가 살아 있나"
        /// 를 곁눈으로 보는 것이므로 회색 테가 맞다.
        /// </summary>
        private static void TeamPanel(GameObject root)
        {
            // 조사용 덤프를 붙여 둔다. 실행 인자 -huddump 가 없으면 스스로 꺼진다.
            if (root.GetComponent<WarriorsHudRuntimeDump>() == null)
            {
                root.AddComponent<WarriorsHudRuntimeDump>();
                Debug.Log("[HUD 배치] 화면 덤프 도구를 붙였습니다. (-huddump 로 켠다)");
            }

            // **얼굴을 더 꽉 채운다.** frame .36(어깨까지) · lookAtDrop .30 은 60px 칸에서 얼굴이 작은
            // 점으로 보였다("동그라미 안에 얼굴"). .30 / .27 이면 머리 전체 + 목까지 칸을 채운다.
            WarriorsPortrait portrait = root.GetComponent<WarriorsPortrait>();
            if (portrait != null)
            {
                SerializedObject so = new SerializedObject(portrait);
                so.FindProperty("frame").floatValue = .30f;
                so.FindProperty("lookAtDrop").floatValue = .27f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            RectTransform players = All(root, "Players").FirstOrDefault();
            if (players == null) return;

            // 한 칸에 [얼굴][이름 / 상태] 가 들어간다. 얼굴 56 + 여백이면 72 로 충분하다.
            // 244×72 에 간격 10 은 빽빽했다. 얼굴 · 이름 · 체력이 한 칸에 들어가는데
            // 세로 72 로는 위아래 여백이 6px 밖에 안 남는다. 칸을 키우고 사이도 벌린다.
            // 간격 14 · HP 칸과의 사이 28 은 실측에서 빽빽했다. 카드는 키우지 않고(보조 정보)
            // 사이만 벌려 "작은 파티 묶음" 이되 카드가 서로 분리돼 읽히게 한다.
            const float slotW = 268f;
            const float slotH = 84f;
            const float slotGap = 20f;

            Vector2 size = new Vector2(slotW, slotH * WarriorsPlayers.Max + slotGap * (WarriorsPlayers.Max - 1));

            // **자리: 오른쪽 위 HP 칸 바로 아래.**
            //
            // 아래 왼쪽에 두었더니 하단 공격 카드와 한 줄에서 경쟁했고, 실제로 34px 겹치기도
            // 했다. 프로필은 HP · 점수와 같은 "내 상태" 묶음이므로 그 아래가 맞는 자리다.
            Corner(players, new Vector2(1f, 1f),
                   new Vector2(-SideMargin, -(TopMargin + TopH + 40f)), size);

            // ⚠ 바깥 통은 그림을 갖지 않는다. 칸마다 테두리가 있으므로 바깥까지 두르면
            //    테가 두 겹이 된다. 예전 금테 배경을 지운다.
            Image shell = players.GetComponent<Image>();
            if (shell != null)
            {
                shell.enabled = false;

                // 꺼진 그림이 옛 판 그림(Panel_Gold)을 물고 있어 에셋을 못 지웠다.
                // ⚠ 파일이 이미 없으면 sprite 는 null 로 읽혀서 `shell.sprite = null` 이 아무 것도
                //    안 바꾼다(guid 참조가 그대로 남는다). SerializedObject 로 강제로 비운다.
                SerializedObject shellSo = new SerializedObject(shell);
                shellSo.FindProperty("m_Sprite").objectReferenceValue = null;
                shellSo.ApplyModifiedPropertiesWithoutUndo();
            }

            for (int i = 3; i >= WarriorsPlayers.Max; i--)
            {
                RectTransform extra = Find(players, $"Player_{i + 1}");
                if (extra == null) continue;

                Debug.Log($"[HUD 배치] 4인 시절 칸 제거 — {extra.name}");
                Object.DestroyImmediate(extra.gameObject);
            }

            for (int i = 0; i < WarriorsPlayers.Max; i++)
            {
                RectTransform slot = Find(players, $"Player_{i + 1}");
                if (slot == null) continue;

                slot.gameObject.SetActive(true);

                // 위에서 아래로. 1P 가 위다.
                float top = size.y * 0.5f - slotH * 0.5f;
                Place(slot, new Vector2(0f, top - i * (slotH + slotGap)), new Vector2(slotW, slotH));
                Plain(slot, new Vector2(slotW, slotH));

                const float face = 60f;
                const float pad = 12f;

                float faceX = -slotW * 0.5f + pad + face * 0.5f;

                Place(Find(slot, "Backplate"), new Vector2(faceX, 0f), new Vector2(face, face));
                Place(Find(slot, "PortraitFrame"), new Vector2(faceX, 0f), new Vector2(face + 6f, face + 6f));

                // **배 게임 초상화 에셋을 그대로 쓴다.** 회색 판을 얼굴 칸에 쓰니 "동그라미 안에 얼굴"
                // 로만 보였다(실측 지적). ShipCoopHudV2 의 portrait-backplate(짙은 청록 바탕) +
                // portrait-frame-red/yellow(그려진 색 테)가 팀이 이미 쓰는 프로필 모양이다. 160×160
                // 정방형이라 Simple 로 놓고, 테 색은 그림에 있으니 tint 는 흰색.
                // ⚠ 배 초상화 에셋(portrait-frame-*)은 모서리 반경이 커서 카드(회색 판 · 반경 12)와
                //    곡률이 안 맞았다(실측 지적). 얼굴 칸도 **카드와 같은 회색 판 그림**(반경 12,
                //    배율 1)으로 만들고, 사람 색은 테에 tint 로 준다 — 1P 빨강 · 2P 금.
                //    바탕은 같은 그림을 짙은 청록으로 물들여 얼굴 뒤가 카드 남색과 갈린다.
                Sprite squareBack = AssetDatabase.LoadAssetAtPath<Sprite>(PlainBack);
                Sprite squareEdge = AssetDatabase.LoadAssetAtPath<Sprite>(PlainEdgeArt);

                Image plate = Find(slot, "Backplate")?.GetComponent<Image>();
                if (plate != null && squareBack != null)
                {
                    plate.sprite = squareBack;
                    plate.type = Image.Type.Sliced;
                    plate.pixelsPerUnitMultiplier = 1f;
                    plate.color = new Color(.55f, 1.0f, 1.2f, 1f);   // 남색 그림 × 청록 → (5,31,59)쯤
                }

                Image ring = Find(slot, "PortraitFrame")?.GetComponent<Image>();
                if (ring != null && squareEdge != null)
                {
                    ring.sprite = squareEdge;
                    ring.type = Image.Type.Sliced;
                    ring.pixelsPerUnitMultiplier = 1f;
                    // 강철색 테(101,124,145) × tint. 빨강은 R 만 살리고, 금은 R·G 를 살린다.
                    ring.color = i == 0 ? new Color(2.2f, .55f, .45f, 1f) : new Color(2.3f, 1.7f, .35f, 1f);
                }

                float textLeft = faceX + face * 0.5f + 12f;
                float textW = (slotW * 0.5f - pad) - textLeft;

                // 1행 이름(실제 닉네임) · 2행 상태 막대.
                // ⚠ "적중 0" 은 뺐다. 전투 중 아무 판단에도 쓰이지 않는 숫자였다.
                Put(slot, "State", new Vector2(textLeft + textW * 0.5f, 17f), new Vector2(textW, 26f),
                    11f, FontMid, TextAlignmentOptions.Left);

                // ⚠ **막대가 거의 안 보였다.** 홈(bar-track)의 색이 #FFFFFF00, 즉 완전 투명이라
                //    빈 구간이 그려지지 않았고, 높이도 10 뿐이라 채워진 부분만 실오라기처럼
                //    보였다. Bar 로 홈을 어둡게 깔고 높이를 14 로 올린다.
                Bar(slot, "Health", new Vector2(textLeft + textW * 0.5f, -19f), new Vector2(textW, 16f));

                // 체력은 초록이다. 사람마다 다른 색으로 두면 "남은 체력" 으로 안 읽힌다.
                RectTransform fill = Find(slot, "Fill");
                Image paint = fill != null ? fill.GetComponent<Image>() : null;
                if (paint != null) paint.color = new Color(.30f, .86f, .42f, 1f);

                Hide(slot, "Chip");

                // 테두리가 맨 위이므로 내용물을 그 앞으로 끌어올린다.
                RectTransform line = Find(slot, "ThemeFrame");
                if (line != null) line.SetSiblingIndex(1);
            }

            TrimTeamArrays(root);

            Debug.Log(
                $"[HUD 배치] 프로필 {size.x:F0}×{size.y:F0} · 세로 {WarriorsPlayers.Max}칸 " +
                $"(칸 {slotW:F0}×{slotH:F0}, 간격 {slotGap:F0}) · 회색 테");
        }

        /// <summary>
        /// 발표자가 들고 있는 <b>칸 배열을 정원에 맞춰 줄인다.</b>
        ///
        /// <c>playerStateTexts</c> · <c>playerStateFills</c> 는 4칸 시절에 네 개로 이어져
        /// 있다. 위에서 3P · 4P 를 지웠으므로 그대로 두면 뒤 두 칸이 <b>없어진 것을
        /// 가리키는 빈 참조</b>가 되고, 발표자의 순회가 그것을 만진다.
        ///
        /// 인스펙터 값이라 코드로는 못 바꾼다. SerializedObject 로 줄여야 한다.
        /// </summary>
        private static void TrimTeamArrays(GameObject root)
        {
            WarriorsHudPresenter presenter = root.GetComponentInChildren<WarriorsHudPresenter>(true);

            if (presenter == null)
            {
                Debug.LogError("[HUD 배치] 발표자를 찾지 못해 칸 배열을 줄이지 못했습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(presenter);

            foreach (string field in new[] { "playerStateTexts", "playerStateFills" })
            {
                SerializedProperty array = so.FindProperty(field);

                if (array == null || !array.isArray)
                {
                    Debug.LogError($"[HUD 배치] '{field}' 를 찾지 못했습니다.");
                    continue;
                }

                if (array.arraySize <= WarriorsPlayers.Max) continue;

                Debug.Log($"[HUD 배치] {field} {array.arraySize}칸 → {WarriorsPlayers.Max}칸");
                array.arraySize = WarriorsPlayers.Max;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------
        // 손도구
        // ------------------------------------------------------------

        private static void Panel(RectTransform rt, Vector2 size)
        {
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);

            if (art == null)
            {
                Debug.LogError($"[HUD 배치] 판 그림을 찾지 못했습니다 — {PanelSprite}");
                return;
            }

            Image image = rt.GetComponent<Image>();
            if (image == null) return;

            image.sprite = art;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            // ⚠ 9-슬라이스와 preserveAspect 를 같이 켜면 늘리는 계산이 어긋난다.
            image.preserveAspect = false;

            // 배율은 판 높이로 정한다 — 캡슐 양 끝이 원으로 남고, 남색 면 높이가 금테 곡률과 맞는다.
            // (124 → 1.13 · 116 → 1.21 · 96 → 1.46 · 90 → 1.56)
            image.pixelsPerUnitMultiplier = PanelPpu(size);

            rt.sizeDelta = size;
        }

        /// <summary>
        /// 🩶 <b>회색 테 판.</b> 금테를 쓰지 않는 보조 UI 용.
        ///
        /// <b>왜 금테를 빼는가.</b> 모든 칸을 금테로 두었더니 화면 전체가 같은 무게가 되어
        /// <b>어느 것이 중요한지 읽히지 않았다.</b> 금테는 점수가 걸린 진행 정보에만 남기고,
        /// 프로필과 안내 문구처럼 "보고 넘기는" 것은 한 단계 내린다.
        ///
        /// 만드는 법은 키캡과 같다 — 바깥 회색 판 안에 남색 바탕을 2px 안쪽으로 겹친다.
        /// 그림을 새로 그리지 않아도 얇은 테두리가 생긴다.
        /// </summary>
        /// <summary>
        /// 이 칸에 붙은 <typeparamref name="T"/> 를 떼어 낸다.
        ///
        /// 레이아웃 그룹과 크기 맞춤이는 <b>내가 정한 값을 말없이 덮어쓴다.</b> 프리팹에는
        /// 내 값이 저장돼 있는데 화면은 전혀 다르게 나오는 일이 그래서 생긴다.
        /// 자리를 직접 정하는 칸에서는 떼는 편이 확실하다.
        /// </summary>
        private static void Strip<T>(RectTransform rt) where T : Component
        {
            T had = rt.GetComponent<T>();
            if (had == null) return;

            Debug.Log($"[HUD 배치] {Path(rt)} 에서 {typeof(T).Name} 을 뗐습니다. (자리를 덮어쓰고 있었음)");
            Object.DestroyImmediate(had, true);
        }

        /// <summary>회색 테 판의 안쪽 여백. 금테(44)보다 훨씬 얇아도 된다.</summary>
        private const float PlainPad = 14f;

        private static void Plain(RectTransform rt, Vector2 size)
        {
            Sprite back = AssetDatabase.LoadAssetAtPath<Sprite>(PlainBack);
            Sprite edge = AssetDatabase.LoadAssetAtPath<Sprite>(PlainEdgeArt);

            if (back == null || edge == null)
            {
                Debug.LogError($"[HUD 배치] 회색 테 그림을 찾지 못했습니다 — {PlainBack} / {PlainEdgeArt}");
                return;
            }

            Image image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();

            image.sprite = back;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = new Color(1f, 1f, 1f, .88f);
            image.raycastTarget = false;

            // 테두리는 <b>맨 위에</b> 얹는다. 배 게임의 팀 판과 같은 방식이다
            // (WarriorsHudTeamPanel.Skin / Frame).
            RectTransform line = Child(rt, "ThemeFrame");
            line.anchorMin = Vector2.zero;
            line.anchorMax = Vector2.one;
            line.offsetMin = Vector2.zero;
            line.offsetMax = Vector2.zero;

            Image paint = line.GetComponent<Image>() ?? line.gameObject.AddComponent<Image>();
            paint.sprite = edge;
            paint.type = Image.Type.Sliced;
            paint.preserveAspect = false;
            paint.pixelsPerUnitMultiplier = 1f;
            paint.color = Color.white;
            paint.raycastTarget = false;
            line.SetAsLastSibling();

            rt.sizeDelta = size;
        }

        /// <summary>화면 구석에 붙인다. 앵커와 피벗을 같은 구석으로 맞춰 해상도가 변해도 안 밀린다.</summary>
        private static void Corner(RectTransform rt, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        private static void Put(RectTransform parent, string childName, Vector2 at, Vector2 size,
                                float min, float max, TextAlignmentOptions align)
        {
            RectTransform rt = childName == null ? parent : Find(parent, childName);
            if (rt == null) return;

            Place(rt, at, size);

            TMP_Text text = rt.GetComponent<TMP_Text>();
            if (text == null) return;

            text.alignment = align;

            // ⚠ 스스로 줄어들게 둔다. 라운드 이름이나 안내문은 길이가 들쭉날쭉해서
            //    고정 크기로 두면 언젠가 칸을 넘는다.
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.enableWordWrapping = false;

            // ⚠ 기본은 Ellipsis 다. 라운드 이름처럼 길이를 모르는 글은 넘치면 "…" 로 줄이는
            //    편이 칸을 뚫는 것보다 낫다. 다만 <b>반드시 다 읽혀야 하는 안내문</b>은
            //    그러면 안 되므로, 그런 것은 PutNoClip 으로 따로 놓는다.
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>
        /// <b>절대 잘리면 안 되는 글</b>을 놓는다. 안내 문구가 이것이다.
        ///
        /// 문장이 "…" 로 끝나면 무엇을 하라는 것인지 알 수 없다. 칸을 넉넉히 주고,
        /// 그래도 모자라면 글자가 스스로 작아지되 <b>잘리지는 않게</b> 둔다.
        /// </summary>
        private static void PutNoClip(RectTransform parent, string childName, Vector2 at, Vector2 size,
                                      float min, float max, TextAlignmentOptions align)
        {
            Put(parent, childName, at, size, min, max, align);

            RectTransform rt = childName == null ? parent : Find(parent, childName);
            TMP_Text text = rt == null ? null : rt.GetComponent<TMP_Text>();

            if (text != null) text.overflowMode = TextOverflowModes.Overflow;
        }

        private static void Bar(RectTransform parent, string childName, Vector2 at, Vector2 size)
        {
            RectTransform rt = Find(parent, childName);
            if (rt == null) return;

            Place(rt, at, size);

            // 홈은 어두운 남색으로. 판이 평평해졌으므로 막대 자리는 스스로 그려야 한다.
            Image track = rt.GetComponent<Image>();

            if (track != null)
            {
                track.sprite = null;
                track.type = Image.Type.Simple;
                track.color = new Color(0f, 0.08f, 0.20f, 0.85f);
            }

            RectTransform fill = Find(rt, "Fill");

            if (fill != null)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = new Vector2(2f, 2f);
                fill.offsetMax = new Vector2(-2f, -2f);
            }
        }

        /// <summary>
        /// 🔑 <b>키캡(J · K · L).</b> 남색 바탕 · 얇은 금테 · 흰 글자.
        ///
        /// <b>왜 다시 만드는가.</b> 원래는 흰 네모에 검은 글자였다. 이 화면에서 가장 센
        /// 대비라 <b>몬스터 그림과 공격명보다 J 가 먼저 보였다.</b> 키캡은 보조 정보다.
        ///
        /// 세 겹으로 만든다 — 바깥 금테(KeyBadge) 안에 남색 바탕(CapFill), 그 위에 글자(Key).
        /// 금테는 <see cref="KeyCapEdge"/> 만큼만 드러나므로 얇은 테두리로 보인다.
        ///
        /// ⚠ 글자가 <b>맨 마지막 자식</b>이어야 바탕 위에 그려진다.
        /// </summary>
        private static void Badge(RectTransform card, Vector2 at, float size)
        {
            RectTransform badge = Find(card, "KeyBadge");
            if (badge == null) return;

            Place(badge, at, new Vector2(size, size));

            // **키보드 키 모양.** 예전 keycap.png(네모 안에 동그라미)는 "동그라미 안 알파벳" 으로
            // 읽혀 뺐다(실측 지적). 대신 회색 판 바탕(둥근 사각형, 반경 12)을 세게 축소해
            // 모서리가 거의 각진 <b>평면 키</b>를 만들고, 아래 3px 에 어두운 옆면을 남겨
            // 키가 살짝 떠 있는 것처럼 보이게 한다.
            //
            //   [ 금색 얇은 테 ]  ← 전체
            //     [ 어두운 옆면 ]  ← 1.5 안쪽, 아래로 붙음
            //     [ 남색 윗면 ]    ← 1.5 안쪽, 위로 3 올라감. 글자는 여기 가운데
            // 내장 흰 둥근 사각형(32px · 경계 10). 흰 바탕이라 color 가 곧 보이는 색이다.
            Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            const float Lift = 3f;

            Image edge = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
            edge.sprite = square;
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = KeyCornerPpu;
            edge.color = KeyCapBorder;
            edge.raycastTarget = false;

            RectTransform side = Child(badge, "CapSide");
            Place(side, new Vector2(0f, -KeyCapEdge * 0.5f),
                new Vector2(size - KeyCapEdge * 2f, size - KeyCapEdge * 3f));
            side.SetAsFirstSibling();
            Image shade = side.GetComponent<Image>() ?? side.gameObject.AddComponent<Image>();
            shade.sprite = square;
            shade.type = Image.Type.Sliced;
            shade.pixelsPerUnitMultiplier = KeyCornerPpu;
            shade.color = KeyCapSide;
            shade.raycastTarget = false;

            RectTransform fill = Child(badge, "CapFill");
            Place(fill, new Vector2(0f, Lift * 0.5f),
                new Vector2(size - KeyCapEdge * 2f, size - KeyCapEdge * 2f - Lift));
            fill.SetSiblingIndex(1);
            Image paint = fill.GetComponent<Image>() ?? fill.gameObject.AddComponent<Image>();
            paint.sprite = square;
            paint.type = Image.Type.Sliced;
            paint.pixelsPerUnitMultiplier = KeyCornerPpu;
            paint.color = KeyCapFill;
            paint.raycastTarget = false;

            TMP_Text key = badge.GetComponentInChildren<TMP_Text>(true);
            if (key == null) return;

            RectTransform kr = key.rectTransform;
            kr.anchorMin = Vector2.zero;
            kr.anchorMax = Vector2.one;
            kr.offsetMin = new Vector2(0f, 3f);   // 글자는 아래 옆면이 아니라 윗면(3px 위) 가운데에
            kr.offsetMax = Vector2.zero;
            kr.SetAsLastSibling();

            key.alignment = TextAlignmentOptions.Center;
            key.enableAutoSizing = false;

            // ⚠ 글자 크기는 키캡 크기에 <b>비례</b>해야 한다. FontMid(20) 고정으로 두었더니
            //    키캡을 38 → 32 로 줄인 순간 굵은 20pt 대문자가 안쪽 남색(28px)을 넘어
            //    테두리 위로 튀어나왔다(실측 지적). 한 변의 절반(16pt)이면 위아래 여유가 6px 씩 남는다.
            key.fontSize = Mathf.Round(size * 0.5f);
            key.margin = Vector4.zero;
            key.color = KeyLetter;
            key.fontStyle = FontStyles.Bold;
        }

        private static void Spread(GameObject root, string groupName, List<RectTransform> cards, float gap)
        {
            if (cards.Count == 0) return;

            RectTransform group = All(root, groupName).FirstOrDefault();
            if (group == null) return;

            float width = cards[0].sizeDelta.x;
            float height = cards.Max(c => c.sizeDelta.y);
            float total = cards.Count * width + (cards.Count - 1) * gap;

            // ⚠ **바닥에서 띄운다.** 프리팹에 y=34 로 박혀 있어 카드가 화면 끝에 붙어
            //    아래쪽 여유가 없었다. 위쪽 여백(36)과 균형이 맞게 잡는다.
            Corner(group, new Vector2(0.5f, 0f), new Vector2(0f, BottomMargin), new Vector2(total, height));

            float start = -total * 0.5f + width * 0.5f;

            for (int i = 0; i < cards.Count; i++)
            {
                Place(cards[i], new Vector2(start + i * (width + gap), 0f), cards[i].sizeDelta);
            }

            Debug.Log(
                $"[HUD 배치] {groupName}  카드 {width:F0}×{height:F0} · 간격 {gap:F0} · " +
                $"줄 {total:F0} · 바닥에서 {BottomMargin:F0}");
        }

        // ------------------------------------------------------------
        // 안쪽 상자 계산 — **손으로 적은 좌표를 쓰지 않기 위한 것**
        //
        // 판은 금테(가로 32 · 세로 20)를 두르고 그 안에 여백을 더 둔다. 글자를 놓을 수
        // 있는 곳은 그 안쪽 상자뿐인데, 지금까지는 -112 · 116 처럼 눈으로 맞춘 값을 적어
        // 두어 TimeLabel(8px) · HP(20px) · Combo(40px) 가 금테에 물려 잘려 있었다.
        //
        // 아래 함수로만 자리를 잡으면 넘칠 수가 없다.
        // ------------------------------------------------------------

        /// <summary>판 안쪽에서 쓸 수 있는 가로 반폭.</summary>
        private static float InnerHalfW(Vector2 size) => size.x * 0.5f - PadXFor(size);

        /// <summary>상단 카드 1행 중심 y 와 높이. 세 판이 같은 줄을 쓴다.</summary>
        // 124 높이 판의 안쪽 반높이 = 62 - PadYFor(23+6=29) = 33. 1행 20+13 = 33 · 2행 11+22 = 33.
        private const float Row1Y = 20f;
        private const float Row1H = 26f;

        /// <summary>상단 카드 2행 중심 y 와 높이. 큰 숫자가 들어가므로 1행보다 높다.</summary>
        private const float Row2Y = -11f;
        private const float Row2H = 44f;

        /// <summary>안쪽 <b>왼쪽 끝</b>에 폭 <paramref name="w"/> 짜리를 붙인 자리.</summary>
        private static Vector2 LeftIn(float halfW, float w, float y) =>
            new Vector2(-halfW + w * 0.5f, y);

        /// <summary>안쪽 <b>오른쪽 끝</b>에 폭 <paramref name="w"/> 짜리를 붙인 자리.</summary>
        private static Vector2 RightIn(float halfW, float w, float y) =>
            new Vector2(halfW - w * 0.5f, y);

        private static void Place(RectTransform rt, Vector2 at, Vector2 size)
        {
            if (rt == null) return;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
        }

        private static void Hide(RectTransform parent, string childName)
        {
            RectTransform rt = Find(parent, childName);
            if (rt != null) rt.gameObject.SetActive(false);
        }

        private static RectTransform Find(RectTransform parent, string name)
        {
            return parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return (RectTransform)found;

            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        private static IEnumerable<RectTransform> All(GameObject root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>(true)
                .Where(t => t.name == name && !Untouchable.Any(u => Under(t, u)));
        }

        private static bool Under(Transform t, string name)
        {
            for (Transform p = t; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>
        /// ⚠ 저장을 믿지 않고 디스크에서 다시 읽는다. 그리고 <b>자식이 판을 넘지 않는지</b>
        /// 실제 숫자로 본다. 눈으로 못 보는 자리라 이 검사가 유일한 안전망이다.
        /// </summary>
        private static void Verify()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            // ⚠ **금테와 회색 테를 나눠서 본다.**
            //
            //    금테는 점수가 걸린 진행 정보에만 쓴다. 전부 금테로 두었더니 화면이 같은
            //    무게가 되어 어느 것이 중요한지 읽히지 않았다. 아래 두 목록이 그 약속이고,
            //    검사가 그것을 지킨다.
            string[] panels =
            {
                "RoundCard", "HPCard", "ScoreCard", "PhaseProgress", "BossCard",
                "Announcement",
                "Card_1", "Card_2", "Card_3", "MCard_1", "MCard_2", "MCard_3",
            };

            string[] plainPanels = { "Objective", "Player_1", "Player_2" };

            List<string> spill = new List<string>();
            List<string> wrongArt = new List<string>();

            foreach (string name in panels)
            {
                RectTransform card = All(saved, name).FirstOrDefault();
                if (card == null) continue;

                Image image = card.GetComponent<Image>();

                if (image == null || image.sprite == null || image.sprite.name != "Panel_GoldOrnate")
                {
                    wrongArt.Add(name);
                }
                else if (image.type != Image.Type.Sliced)
                {
                    wrongArt.Add($"{name}(9슬라이스 아님)");
                }

                // ⚠ **판 크기가 아니라 안쪽 상자로 잰다.**
                //
                //    예전에는 판의 절반(예: 380/2 = 190)과 비교했다. 그러면 금테 위에
                //    올라앉은 글자가 통과한다 — 실제로 TimeLabel · HP · Combo 가 그렇게
                //    빠져나가 화면에서 테두리에 물려 잘려 있었다. Combo 는 40px 이나
                //    넘었는데도 "칸 안" 으로 셌다.
                //
                //    글자를 놓을 수 있는 곳은 금테와 여백을 뺀 안쪽뿐이다. 거기로 잰다.
                Vector2 inner = new Vector2(
                    card.sizeDelta.x * 0.5f - PadXFor(card.sizeDelta),
                    card.sizeDelta.y * 0.5f - PadYFor(card.sizeDelta));

                foreach (RectTransform child in card.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child == card || child.parent != card || !child.gameObject.activeSelf) continue;

                    Vector2 edge = new Vector2(
                        Mathf.Abs(child.anchoredPosition.x) + child.sizeDelta.x * 0.5f,
                        Mathf.Abs(child.anchoredPosition.y) + child.sizeDelta.y * 0.5f);

                    if (edge.x > inner.x + 1f || edge.y > inner.y + 1f)
                    {
                        spill.Add(
                            $"{name}/{child.name} (가로 {edge.x:F0}/{inner.x:F0} · " +
                            $"세로 {edge.y:F0}/{inner.y:F0})");
                    }
                }
            }

            // 회색 테 칸은 배 게임 판을 쓰고, 금테를 쓰면 안 된다.
            Sprite plainArt = AssetDatabase.LoadAssetAtPath<Sprite>(PlainBack);

            foreach (string name in plainPanels)
            {
                RectTransform card = All(saved, name).FirstOrDefault();
                if (card == null) continue;

                Image image = card.GetComponent<Image>();

                if (image == null || image.sprite != plainArt)
                {
                    wrongArt.Add($"{name}(회색 테여야 함)");
                }
                else if (Find(card, "ThemeFrame") == null)
                {
                    wrongArt.Add($"{name}(테두리 없음)");
                }
            }

            if (wrongArt.Count > 0)
            {
                Debug.LogError($"[HUD 배치] 약속한 테를 안 쓰는 칸 — {string.Join(", ", wrongArt)}");
                return;
            }

            if (spill.Count > 0)
            {
                Debug.LogError(
                    "[HUD 배치] 금테 안쪽을 넘은 자식 (실제/한계) — " +
                    string.Join("\n    ", spill));
                return;
            }

            Debug.Log(
                "[HUD 배치] ✅ 모든 칸이 같은 판 그림(9-슬라이스)을 쓰고, " +
                "자식이 금테 안쪽 상자 안에 들어갑니다.");
        }
    }
}

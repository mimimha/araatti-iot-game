using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MiniGames.Common.DebugTools;
using MiniGames.Common.UI;

namespace MiniGames.Common.EditorTools
{
    /// <summary>
    /// 공통 매칭·결과 UI 를 짓는다. 결과물은 두 가지다.
    ///
    ///   1. <b>프리팹</b> CommonMatchCanvas — 미니게임 씬에 그대로 끌어다 놓는 드롭인 한 덩어리.
    ///      Canvas(매칭·결과 화면) 와 Systems(흐름·컨트롤러·씬 전환·명단) 가 같이 들어 있어서,
    ///      미니게임마다 UI 를 복사해 고칠 일이 없다. 루트의 <see cref="CommonMatchingUI"/> 에
    ///      포탈이 자기 <see cref="MiniGameConfig"/> 를 <c>Show(config)</c> 로 넘기면 끝이다.
    ///   2. <b>테스트 씬</b> CommonMatchResultTest — 위 프리팹 + 해변 배경 + 설정 목록 + 테스트 조작 줄.
    ///
    /// 매칭 판은 위에서 아래로 네 칸이 고정돼 있다. 글은 최소한만 — 인원 숫자·자동 시작·부제는 없다.
    ///
    ///     HeaderArea      작게 "광산 미니게임" + 크게 "플레이어를 매칭 중입니다" + 튀는 점 셋
    ///     StatusArea      매칭 중 "30초 후 자동 시작" → 카운트다운이면 큰 숫자
    ///     PlayerSlotArea  파티원 카드 — 두 상태에서 같은 Y
    ///     ActionArea      (배만) "4명이 모두 모여야…" + [게임 시작] [매칭 취소]
    ///
    /// 손으로 끌어다 붙인 UI 는 무엇이 무엇에 물려 있는지 씬을 열어야만 알 수 있다. 여기서
    /// 한 번에 지어 두면 배치와 연결이 전부 코드로 남아, 고칠 때 이 파일만 고치고 메뉴를
    /// 다시 누르면 된다.
    ///
    /// 그림은 전부 프로젝트에 이미 있는 것만 쓴다. 새로 만든 이미지는 하나도 없다.
    ///
    /// 메뉴: Tools ▸ 아라아띠 ▸ 공통 매칭·결과 테스트 씬 만들기
    /// </summary>
    public static class CommonMatchSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Develop/SeoYeon/CommonMatchResultTest.unity";
        private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab";
        private const string ConfigDir = "Assets/Game/ScriptableObjects/MiniGames/Common/";
        /// <summary>
        /// 배경. 캐릭터 커스터마이징 화면(CharacterCustomizationTest)과 같은 섬·해변·카메라를 그대로
        /// 프리팹으로 묶어 둔 것이다. 매칭 화면은 로비→미니게임 사이에 잠깐 보이는 판이라 커스터마이징
        /// 화면과 같은 곳에 있는 느낌이 나야 한다. 카메라(Preview Camera)와 조명도 안에 들어 있다.
        /// </summary>
        private const string BackdropPrefab =
            "Assets/Game/Prefabs/Environment/CustomizationBeachBackdrop.prefab";
        private const string FontPath = "Assets/Game/Fonts/NotoSansKR-Bold SDF.asset";

        private const string Hud = "Assets/Game/Art/UI/ShipCoopHudV2/";
        private const string FramePath = "Assets/Game/Art/UI/ChannelSelect/channel-panel-frame-login-matched.png";
        private const string ButtonPath = "Assets/Game/Art/UI/Common/button-login-base-balanced-gold.png";
        private const string BackplatePath = Hud + "portrait-backplate.png";
        private const string DotPath = Hud + "badge-player.png";
        private const string WhitePlatePath = Hud + "bar-fill-white.png";
        private const string GemPath = "Assets/Game/Art/MiniGames/Warriors/UI/SeaHeartGem.png";

        private static readonly string[] PortraitPaths =
        {
            Hud + "portrait-captain.png", Hud + "portrait-diver.png",
            Hud + "portrait-jester.png", Hud + "portrait-wig.png",
        };

        private static readonly string[] PortraitFramePaths =
        {
            Hud + "portrait-frame-yellow.png", Hud + "portrait-frame-green.png",
            Hud + "portrait-frame-purple.png", Hud + "portrait-frame-red.png",
        };

        internal static readonly Color[] SlotAccents =
        {
            // 1번 자리는 사실상 늘 본인이다. 빨강은 "뭔가 잘못됐다"로 읽혀서 금색으로 바꿨다.
            new(.98f, .80f, .36f, 1f),   // 금색 - 선장 (본인)
            new(.32f, .82f, .55f, 1f),   // 초록 - 다이버
            new(.68f, .52f, .95f, 1f),   // 보라 - 광대
            new(.93f, .47f, .45f, 1f),   // 산호 - 백발
        };

        internal static readonly Color Plank = new(1f, 1f, 1f, 1f);           // 프레임은 원본 색 그대로
        internal static readonly Color Sunk = new(.045f, .100f, .180f, 1f);   // 기록: 나무판보다 어둡게
        internal static readonly Color Raised = new(.130f, .280f, .490f, 1f); // 보상: 나무판보다 밝게
        internal static readonly Color Gold = new(1f, .82f, .35f, 1f);
        internal static readonly Color Cyan = new(.62f, .90f, .99f, 1f);
        internal static readonly Color White = new(.96f, .98f, 1f, 1f);
        internal static readonly Color Muted = new(.72f, .81f, .92f, 1f);

        internal static TMP_FontAsset font;
        internal static Sprite frame, button, backplate, dot, whitePlate, gem;
        internal static Sprite[] portraits, portraitFrames;

        private const float PanelW = 1240f, PanelH = 778f;
        private const float ResultW = 900f, ResultH = 830f;

        // ── 매칭 판 세로 배치 (판 위쪽 기준 px). 위→아래 순서가 곧 정보의 순서다.
        //    게임 이름(작게) → 제목(크게) → [카운트다운 숫자 자리] → 카드 → (배만) 이유 한 줄 → 버튼.
        //    숫자 자리는 매칭 중엔 비어 있으므로 딱 숫자 높이만큼만 남긴다. 더 남기면 판이 헐렁해 보인다.
        private const float GameTitleY = -66f, GameTitleH = 28f;     // -66 ~ -94
        private const float TitleY = -98f, TitleH = 54f;             // -98 ~ -152
        private const float StatusAreaY = -162f, StatusAreaH = 96f;  // -162 ~ -258  (매칭 중: 자동 시작 한 줄 / 카운트다운: 숫자)
        private const float NumberH = 96f;
        private const float SlotAreaY = -270f, SlotH = 330f;         // -270 ~ -600  ← 두 상태에서 같은 Y
        private const float HintY = -608f, HintH = 26f;              // -608 ~ -634
        private const float ActionY = -642f, ActionH = 66f;          // -642 ~ -708

        [MenuItem("Tools/아라아띠/공통 매칭·결과 테스트 씬 만들기")]
        public static void Build()
        {
            ApplyBorders();

            // 빈 씬을 먼저 연다. 새 씬을 열면 아무도 붙들고 있지 않은 에셋이 정리되는데,
            // 그 전에 불러 둔 ScriptableObject 는 여기서 조용히 사라진다. 실제로 그렇게
            // 해 봤다가 config 세 개가 전부 fileID 0 으로 저장됐다. 그래서 불러오기는 전부 이 뒤로.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LoadSharedArt();

            var sword = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(ConfigDir + "MiniGame_Sword.asset");
            var mining = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(ConfigDir + "MiniGame_Mining.asset");
            var ship = AssetDatabase.LoadAssetAtPath<MiniGameConfig>(ConfigDir + "MiniGame_Ship.asset");
            if (sword == null || mining == null || ship == null)
            {
                Debug.LogError("[SceneBuilder] 미니게임 설정 에셋을 찾지 못했습니다: " + ConfigDir);
                return;
            }

            // ── Environment: 캐릭터 커스터마이징 화면의 섬·해변 배경을 그대로 세운다 (카메라·조명 포함).
            var env = new GameObject("Environment");
            var backdrop = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefab);
            if (backdrop == null)
            {
                Debug.LogError("[SceneBuilder] 배경 프리팹이 없습니다: " + BackdropPrefab);
                return;
            }

            var backdropGo = (GameObject)PrefabUtility.InstantiatePrefab(backdrop, env.transform);
            backdropGo.name = "CustomizationBeachBackdrop";

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // ── Systems (씬 쪽): 설정 세 개 목록. 실제 게임에서는 포탈이 자기 설정을 직접 들고 있다.
            var systems = new GameObject("Systems");
            var providerGo = new GameObject("MiniGameConfigProvider");
            providerGo.transform.SetParent(systems.transform);
            var provider = providerGo.AddComponent<MiniGameConfigProvider>();
            Bind(provider, "sword", sword);
            Bind(provider, "mining", mining);
            Bind(provider, "ship", ship);

            // ── 드롭인 한 덩어리: Canvas + Systems
            GameObject ui = BuildCommonUI(sword, out MatchFlow flow, out MatchFlowController controller,
                out MatchPanelPresenter matchPanel, out ResultPanelPresenter resultPanel,
                out CommonMatchingUI facade);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(ui, PrefabPath, InteractionMode.AutomatedAction);

            // ── 테스트 전용. 프리팹 바깥에 둔다 — 실제 게임에 딸려 가면 안 된다.
            BuildDebug(flow, controller, matchPanel, resultPanel, facade, provider);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] 프리팹: {PrefabPath}\n[SceneBuilder] 테스트 씬: {ScenePath}");
        }

        /// <summary>
        /// 9-slice 테두리를 박아 둔다. 늘려도 밧줄 굵기가 유지되어야 한다.
        /// 지금 이 스프라이트들을 쓰는 곳은 전부 Image Type = Simple/Filled 이라 보더는
        /// 무시되고, 따라서 다른 화면에는 아무 영향이 없다 (확인함). 이미 같은 값이면 건너뛴다.
        /// </summary>
        internal static void ApplyBorders()
        {
            SetBorder(FramePath, new Vector4(130f, 150f, 130f, 150f));
            SetBorder(ButtonPath, new Vector4(70f, 20f, 70f, 20f));
            SetBorder(BackplatePath, new Vector4(28f, 28f, 28f, 28f));
            SetBorder(WhitePlatePath, new Vector4(10f, 8f, 10f, 8f));
            foreach (string p in PortraitFramePaths) SetBorder(p, new Vector4(30f, 30f, 30f, 30f));
        }

        /// <summary>
        /// 매칭 화면이 쓰는 그림과 글꼴을 전부 불러 둔다. 전부 프로젝트에 이미 있던 것이다.
        /// 새 씬을 연 <b>뒤에</b> 불러야 한다 (씬을 열면 참조 없는 에셋이 정리된다).
        /// </summary>
        internal static void LoadSharedArt()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            frame = Load(FramePath);
            button = Load(ButtonPath);
            backplate = Load(BackplatePath);
            dot = Load(DotPath);
            whitePlate = Load(WhitePlatePath);
            gem = Load(GemPath);

            portraits = new Sprite[4];
            portraitFrames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                portraits[i] = Load(PortraitPaths[i]);
                portraitFrames[i] = Load(PortraitFramePaths[i]);
            }
        }

        // ------------------------------------------------------------
        // 드롭인 프리팹 — 이것만 씬에 놓으면 공통 흐름이 돈다
        // ------------------------------------------------------------

        private static GameObject BuildCommonUI(MiniGameConfig startConfig,
            out MatchFlow flow, out MatchFlowController controller,
            out MatchPanelPresenter matchPanel, out ResultPanelPresenter resultPanel,
            out CommonMatchingUI facade)
        {
            var root = new GameObject("CommonMatchUI");
            facade = root.AddComponent<CommonMatchingUI>();

            var systems = new GameObject("Systems");
            systems.transform.SetParent(root.transform);

            var flowGo = new GameObject("MatchFlow");
            flowGo.transform.SetParent(systems.transform);
            flow = flowGo.AddComponent<MatchFlow>();

            var sceneGo = new GameObject("SceneTransitionService");
            sceneGo.transform.SetParent(systems.transform);
            SceneTransitionService sceneTransition = sceneGo.AddComponent<SceneTransitionService>();

            var controllerGo = new GameObject("MatchFlowController");
            controllerGo.transform.SetParent(systems.transform);
            controller = controllerGo.AddComponent<MatchFlowController>();

            // 명단은 static 이지만 하이어라키에서 보이고 인스펙터로 읽을 수 있게 자리를 둔다.
            var rosterGo = new GameObject("PlayerRoster");
            rosterGo.transform.SetParent(systems.transform);
            rosterGo.AddComponent<PlayerRosterHost>();

            GameObject canvasGo = Canvas("Canvas", root.transform, 0);

            matchPanel = BuildMatchPanel(canvasGo.transform, flow);
            resultPanel = BuildResultPanel(canvasGo.transform);

            Bind(flow, "config", startConfig);
            BindFloat(flow, "countdownSeconds", 5f);
            BindBool(flow, "joinLocalPlayerOnStart", true);
            BindBool(flow, "autoStart", true);

            Bind(controller, "flow", flow);
            Bind(controller, "sceneTransition", sceneTransition);
            Bind(controller, "resultPanel", resultPanel);
            // 테스트 씬에서는 진짜 씬을 열지 않는다. 실제 미니게임에 붙일 때 끈다.
            BindBool(controller, "suppressSceneLoad", true);
            BindBool(controller, "returnToLobbyOnCancel", true);

            Bind(facade, "flow", flow);
            Bind(facade, "controller", controller);
            Bind(facade, "canvasRoot", canvasGo);
            Bind(facade, "matchPanel", matchPanel.gameObject);
            Bind(facade, "resultPanel", resultPanel);
            BindBool(facade, "showOnStart", true);

            return root;
        }

        internal static GameObject Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(UnityEngine.Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent);

            var canvas = go.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            // 1920x1080 기준으로 만들고 화면 크기에 맞춰 통째로 줄인다. 16:9 면 (1600x900 포함)
            // 전체가 같은 비율로 줄어들 뿐이라 배치가 깨지지 않는다.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            return go;
        }

        // ------------------------------------------------------------
        // 매칭 + 카운트다운 — 한 판 위에서 머리말만 바뀐다
        // ------------------------------------------------------------

        private static MatchPanelPresenter BuildMatchPanel(Transform parent, MatchFlow flow)
        {
            var panel = Node("MatchPanel", parent, Vector2.zero, new Vector2(PanelW, PanelH), top: false);
            MatchPanelPresenter presenter = panel.AddComponent<MatchPanelPresenter>();

            Image plank = Plate(panel, frame, Plank);
            // 배수를 올리면 밧줄이 얇아진다. 장식보다 정보가 먼저 보이게 하는 가장 싼 방법.
            plank.pixelsPerUnitMultiplier = 2.1f;

            // ── HeaderArea: 어느 게임인지(작게) + 지금 무엇을 하는지(크게). 상단 글은 이 둘뿐이다.
            var header = Node("HeaderArea", panel.transform, new Vector2(0f, GameTitleY),
                new Vector2(1160f, GameTitleY - TitleY + TitleH));   // 92
            TMP_Text gameTitle = Label(header.transform, "GameTitle", "광산 미니게임",
                21f, Gold, Vector2.zero, new Vector2(1000f, GameTitleH));
            TMP_Text title = Label(header.transform, "Title", "플레이어를 매칭 중입니다",
                44f, White, new Vector2(0f, TitleY - GameTitleY), new Vector2(1000f, TitleH));

            // 제목 뒤에서 튀는 점 셋. 제목과 따로 두어 제목 글자는 움직이지 않는다.
            // x 는 실행 중 제목의 실제 폭을 재서 Presenter 가 잡는다. y 는 제목 글줄과 같은 높이.
            float headerH = GameTitleY - TitleY + TitleH;
            float titleCentreY = (TitleY - GameTitleY) - TitleH * .5f;   // 헤더 위 기준 제목 중심
            float dotY = titleCentreY + headerH * .5f;                    // 헤더 중심 기준으로 바꾼다
            var dots = new TMP_Text[3];
            for (int i = 0; i < dots.Length; i++)
                dots[i] = Label(header.transform, $"Dot{i + 1}", ".", 44f, White,
                    new Vector2(0f, dotY), new Vector2(20f, TitleH), centre: true);

            // ── StatusArea: 매칭 중엔 "30초 후 자동 시작" 한 줄, 카운트다운이면 큰 숫자로 교체.
            //    크기를 고정해 두어야 내용이 바뀌어도 아래 칸이 밀리지 않는다.
            var statusArea = Node("StatusArea", panel.transform, new Vector2(0f, StatusAreaY),
                new Vector2(1160f, StatusAreaH));
            var matchStatus = Node("MatchStatus", statusArea.transform, Vector2.zero,
                new Vector2(1160f, StatusAreaH));
            TMP_Text autoStart = Label(matchStatus.transform, "AutoStartText", "30초 후 자동 시작",
                23f, Cyan, new Vector2(0f, -6f), new Vector2(800f, 32f));
            var countdownStatus = Node("CountdownStateArea", statusArea.transform, Vector2.zero,
                new Vector2(1160f, StatusAreaH));
            TMP_Text cdNumber = Label(countdownStatus.transform, "Number", "5",
                84f, White, new Vector2(0f, -(StatusAreaH - NumberH) * .5f), new Vector2(420f, NumberH));
            countdownStatus.SetActive(false);

            // ── PlayerSlotArea: 두 상태에서 같은 Y. 여기가 움직이면 다른 화면처럼 보인다.
            var slotArea = Node("PlayerSlotArea", panel.transform, new Vector2(0f, SlotAreaY), new Vector2(1060f, SlotH));
            var slots = new MatchSlotView[4];
            const float slotWidth = 236f, gap = 34f;
            float row = slots.Length * slotWidth + (slots.Length - 1) * gap;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = BuildSlot(slotArea.transform, i,
                    -row * .5f + slotWidth * .5f + i * (slotWidth + gap), slotWidth);

                // 내 자리 강조색. 자리마다 다른 accent 와 별개로, 내가 어느 자리에 앉든 금색이다.
                BindColor(slots[i], "localAccent", SlotAccents[0]);
            }

            // ── ActionArea: (배만) 못 누르는 이유 한 줄 + 버튼 두 개, 같은 크기.
            //    숨겨져도 위 칸에 영향이 없도록 별도 컨테이너.
            var actionArea = Node("ActionArea", panel.transform, new Vector2(0f, HintY),
                new Vector2(PanelW, HintY - ActionY + ActionH));   // 102
            TMP_Text hint = Label(actionArea.transform, "StartHint", "",
                19f, Muted, Vector2.zero, new Vector2(1000f, HintH));
            Button start = ButtonNode(actionArea.transform, "StartButton", "게임 시작",
                new Vector2(-172f, ActionY - HintY), new Vector2(300f, ActionH), Gold, out TMP_Text startLabel);
            Button cancel = ButtonNode(actionArea.transform, "CancelButton", "매칭 취소",
                new Vector2(172f, ActionY - HintY), new Vector2(300f, ActionH), White, out _);

            // 배에서 4명이 안 찼을 때 잠긴 버튼이 "잠겼다" 고 읽혀야 한다. 기본 비활성 색은 거의 티가 안 났다.
            ColorBlock colours = start.colors;
            colours.disabledColor = new Color(.45f, .50f, .58f, .75f);
            start.colors = colours;

            Bind(presenter, "flow", flow);
            Bind(presenter, "gameTitleText", gameTitle);
            Bind(presenter, "titleText", title);
            BindArray(presenter, "dotTexts", dots);
            Bind(presenter, "matchStatus", matchStatus);
            Bind(presenter, "autoStartText", autoStart);
            Bind(presenter, "countdownStatus", countdownStatus);
            Bind(presenter, "countdownNumber", cdNumber);
            BindArray(presenter, "slots", slots);
            BindFloat(presenter, "slotGap", gap);
            Bind(presenter, "actionArea", actionArea);
            Bind(presenter, "startHintText", hint);
            Bind(presenter, "startButton", start);
            Bind(presenter, "startButtonLabel", startLabel);
            Bind(presenter, "cancelButton", cancel);
            return presenter;
        }

        internal static MatchSlotView BuildSlot(Transform parent, int index, float x, float width)
        {
            var go = Node($"Slot_{index + 1}", parent, new Vector2(x, 0f), new Vector2(width, 330f));

            // 카드는 순백 판에 색을 얹는다. 어두운 스프라이트에 곱하면 무엇을 넣어도
            // 거의 검정이 되어, 네이비 패널 안에서 카드가 구멍처럼 뚫려 보였다.
            Image card = Plate(go, whitePlate, Color.white);
            card.pixelsPerUnitMultiplier = .45f;

            MatchSlotView view = go.AddComponent<MatchSlotView>();

            var wellGo = Node("PortraitWell", go.transform, new Vector2(0f, -26f), new Vector2(152f, 152f));
            Image well = Plate(wellGo, backplate, Color.white);

            var portraitGo = Node("Portrait", wellGo.transform, Vector2.zero, new Vector2(140f, 140f), top: false);
            var portrait = portraitGo.AddComponent<Image>();
            portrait.sprite = portraits[index];
            portrait.type = Image.Type.Simple;       // 캐릭터 그림은 늘리지 않는다
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            var frameGo = Node("PortraitFrame", wellGo.transform, Vector2.zero, new Vector2(160f, 160f), top: false);
            var pFrame = frameGo.AddComponent<Image>();
            pFrame.sprite = portraitFrames[index];
            pFrame.type = Image.Type.Sliced;
            pFrame.pixelsPerUnitMultiplier = 1f;
            pFrame.raycastTarget = false;

            var namePlateGo = Node("NamePlate", go.transform, new Vector2(0f, -198f), new Vector2(width - 48f, 46f));
            Image namePlate = Plate(namePlateGo, whitePlate, Color.white);
            namePlate.pixelsPerUnitMultiplier = 1f;
            TMP_Text name = Label(namePlateGo.transform, "Name", "매칭 중...",
                23f, White, Vector2.zero, new Vector2(width - 56f, 40f), centre: true);

            var readyGo = Node("ReadyPlate", go.transform, new Vector2(0f, -256f), new Vector2(width - 48f, 44f));
            Image readyPlate = Plate(readyGo, whitePlate, new Color(.043f, .086f, .149f, .85f));
            readyPlate.pixelsPerUnitMultiplier = 1f;

            var dotGo = Node("Dot", readyGo.transform, new Vector2(-52f, 0f), new Vector2(18f, 18f), top: false);
            var dotImage = dotGo.AddComponent<Image>();
            dotImage.sprite = dot;
            dotImage.raycastTarget = false;

            TMP_Text state = Label(readyGo.transform, "State", "WAIT",
                20f, White, new Vector2(16f, 0f), new Vector2(width - 94f, 38f), centre: true);

            Bind(view, "card", card);
            Bind(view, "portraitBack", well);
            Bind(view, "portrait", portrait);
            Bind(view, "portraitFrame", pFrame);
            Bind(view, "namePlate", namePlate);
            Bind(view, "nameText", name);
            Bind(view, "readyPlate", readyPlate);
            Bind(view, "readyDot", dotImage);
            Bind(view, "stateText", state);
            BindColor(view, "accent", SlotAccents[index]);
            return view;
        }

        // ------------------------------------------------------------
        // 결과
        // ------------------------------------------------------------

        private static ResultPanelPresenter BuildResultPanel(Transform parent)
        {
            // 바깥 껍데기는 항상 켜 둔다. Awake 에서 버튼을 잇는데 껍데기까지 꺼 버리면
            // Awake 가 영영 돌지 않아 [다시 하기]/[로비로] 가 먹통이 된다.
            var panel = Node("ResultPanel", parent, Vector2.zero, new Vector2(ResultW, ResultH), top: false);
            ResultPanelPresenter presenter = panel.AddComponent<ResultPanelPresenter>();

            var content = Node("Content", panel.transform, Vector2.zero, new Vector2(ResultW, ResultH), top: false);
            Image plank = Plate(content, frame, Plank);
            plank.pixelsPerUnitMultiplier = 2.3f;

            TMP_Text title = Label(content.transform, "Title", "GAME CLEAR",
                60f, Gold, new Vector2(0f, -88f), new Vector2(700f, 78f));
            TMP_Text subtitle = Label(content.transform, "Subtitle", "미션 성공",
                20f, Muted, new Vector2(0f, -178f), new Vector2(700f, 28f));

            var stats = Node("StatPanel", content.transform, new Vector2(0f, -226f), new Vector2(680f, 204f));
            Plate(stats, whitePlate, Sunk).pixelsPerUnitMultiplier = 1f;
            Row(stats.transform, "Score", "최종 점수", "0", -22f, out TMP_Text scoreLabel, out TMP_Text scoreValue);
            Row(stats.transform, "Time", "플레이 시간", "00:00", -78f, out TMP_Text timeLabel, out TMP_Text timeValue);
            GameObject extraRow = Row(stats.transform, "Extra", "기록", "0", -134f,
                out TMP_Text extraLabel, out TMP_Text extraValue);

            // 기록보다 32px 떨어뜨리고 한 단 밝게. 결과 화면에서 눈이 먼저 가야 하는 곳이다.
            var reward = Node("RewardPanel", content.transform, new Vector2(0f, -462f), new Vector2(680f, 168f));
            Plate(reward, whitePlate, Raised).pixelsPerUnitMultiplier = 1f;
            Label(reward.transform, "Heading", "획득한 보상",
                18f, Cyan, new Vector2(0f, -16f), new Vector2(620f, 24f));

            var gemGo = Node("Gem", reward.transform, new Vector2(-166f, -44f), new Vector2(70f, 70f));
            var gemImage = gemGo.AddComponent<Image>();
            gemImage.sprite = gem;
            gemImage.type = Image.Type.Simple;
            gemImage.preserveAspect = true;
            gemImage.raycastTarget = false;

            TMP_Text rewardName = Label(reward.transform, "Name", "바다의 심장 조각",
                36f, new Color(1f, .96f, .86f, 1f), new Vector2(40f, -52f), new Vector2(540f, 50f));
            TMP_Text rewardState = Label(reward.transform, "State", "획득!",
                19f, Gold, new Vector2(0f, -118f), new Vector2(620f, 28f));

            Button retry = ButtonNode(content.transform, "RetryButton", "다시 하기",
                new Vector2(-178f, -662f), new Vector2(300f, 72f), Gold, out _, 28f);
            Button lobby = ButtonNode(content.transform, "LobbyButton", "로비로",
                new Vector2(178f, -662f), new Vector2(300f, 72f), White, out _, 28f);

            Bind(presenter, "root", content);
            Bind(presenter, "titleText", title);
            Bind(presenter, "subtitleText", subtitle);
            Bind(presenter, "scoreLabel", scoreLabel);
            Bind(presenter, "scoreValue", scoreValue);
            Bind(presenter, "timeLabel", timeLabel);
            Bind(presenter, "timeValue", timeValue);
            Bind(presenter, "extraRow", extraRow);
            Bind(presenter, "extraLabel", extraLabel);
            Bind(presenter, "extraValue", extraValue);
            Bind(presenter, "rewardRoot", reward);
            Bind(presenter, "rewardName", rewardName);
            Bind(presenter, "rewardState", rewardState);
            Bind(presenter, "retryButton", retry);
            Bind(presenter, "lobbyButton", lobby);

            content.SetActive(false);
            return presenter;
        }

        private static GameObject Row(Transform parent, string name, string label, string value, float y,
            out TMP_Text labelText, out TMP_Text valueText)
        {
            var row = Node(name + "Row", parent, new Vector2(0f, y), new Vector2(600f, 48f));
            labelText = Label(row.transform, "Label", label, 23f, Muted,
                new Vector2(-300f, 0f), new Vector2(320f, 40f), left: true);
            valueText = Label(row.transform, "Value", value, 27f, White,
                new Vector2(300f, 0f), new Vector2(320f, 40f), right: true);
            return row;
        }

        // ------------------------------------------------------------
        // 테스트 조작 — 프리팹 바깥. 실제 게임에는 딸려 가지 않는다
        // ------------------------------------------------------------

        private static void BuildDebug(MatchFlow flow, MatchFlowController controller,
            MatchPanelPresenter matchPanel, ResultPanelPresenter resultPanel,
            CommonMatchingUI facade, MiniGameConfigProvider provider)
        {
            var debugRoot = new GameObject("Debug");
            GameObject canvasGo = Canvas("DebugCanvas", debugRoot.transform, 10);

            GameObject playing = BuildPlayingBanner(canvasGo.transform);

            // 조작 줄은 게임 세 개 버튼만. 판(배경)도 상태 글도 두지 않는다 — 화면 아래가 답답해 보였다.
            // +Player / -Player / 준비 토글 / 조각 초기화 는 리그 코드에 남아 있으니 필요하면 버튼만 다시 달면 된다.
            var bar = Node("DebugBar", canvasGo.transform, Vector2.zero, new Vector2(560f, 96f), top: false);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
            rt.pivot = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);

            float x = -(170f * 3f + 12f * 2f) * .5f;
            Button swordBtn = Small(bar.transform, "SwordGame", "검 1~2인", ref x);
            Button miningBtn = Small(bar.transform, "MiningGame", "광산 1~4인", ref x);
            Button shipBtn = Small(bar.transform, "ShipGame", "배 4인", ref x);

            var rigGo = new GameObject("MatchDebugControls");
            rigGo.transform.SetParent(debugRoot.transform);
            MatchResultTestRig rig = rigGo.AddComponent<MatchResultTestRig>();

            Bind(rig, "flow", flow);
            Bind(rig, "controller", controller);
            Bind(rig, "matchingUI", facade);
            Bind(rig, "matchPanel", matchPanel);
            Bind(rig, "resultPanel", resultPanel);
            Bind(rig, "configs", provider);
            BindBool(rig, "showDebugControls", true);
            Bind(rig, "debugRoot", bar);
            Bind(rig, "swordButton", swordBtn);
            Bind(rig, "miningButton", miningBtn);
            Bind(rig, "shipButton", shipBtn);
            Bind(rig, "playingRoot", playing);
            Bind(rig, "playingText", playing.transform.Find("Text").GetComponent<TMP_Text>());
        }

        private static GameObject BuildPlayingBanner(Transform parent)
        {
            var go = Node("PlayingBanner", parent, Vector2.zero, new Vector2(820f, 190f), top: false);
            Plate(go, frame, Plank).pixelsPerUnitMultiplier = 2.4f;
            Label(go.transform, "Text", "진행 중...", 36f, White, Vector2.zero, new Vector2(700f, 60f), centre: true);
            go.SetActive(false);
            return go;
        }

        private static Button Small(Transform parent, string name, string label, ref float x)
        {
            const float w = 170f;
            Button b = ButtonNode(parent, name, label, new Vector2(x + w * .5f, -40f),
                new Vector2(w, 46f), White, out _, 18f);
            x += w + 12f;
            return b;
        }

        // ------------------------------------------------------------
        // 조립 도구
        // ------------------------------------------------------------

        internal static Sprite Load(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("[SceneBuilder] 스프라이트를 찾지 못했습니다: " + path);
            return s;
        }

        /// <summary>9-slice 테두리를 박는다. 이미 같은 값이면 아무것도 하지 않는다.</summary>
        internal static void SetBorder(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[SceneBuilder] 텍스처 임포터를 못 찾았습니다: " + path);
                return;
            }

            if (importer.spriteBorder == border) return;

            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        /// <param name="top">
        /// true 면 부모의 위쪽 가장자리에 매단다. 판 안의 줄들은 "위에서 몇 픽셀" 로
        /// 읽는 편이 셈이 쉬워서, 판 자신만 가운데(top:false)에 두고 그 안은 전부 위 기준이다.
        /// </param>
        internal static GameObject Node(string name, Transform parent, Vector2 pos, Vector2 size, bool top = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, top ? 1f : .5f);
            rt.pivot = new Vector2(.5f, top ? 1f : .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return go;
        }

        internal static Image Plate(GameObject go, Sprite sprite, Color colour)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = colour;
            return image;
        }

        internal static TMP_Text Label(Transform parent, string name, string text, float size, Color colour,
            Vector2 pos, Vector2 rect, bool centre = false, bool left = false, bool right = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;

            if (centre || left || right)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(left ? 0f : right ? 1f : .5f, .5f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
            }

            rt.sizeDelta = rect;
            rt.anchoredPosition = pos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = left ? TextAlignmentOptions.Left
                : right ? TextAlignmentOptions.Right
                : TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        internal static Button ButtonNode(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            Color textColour, out TMP_Text labelText, float fontSize = 26f)
        {
            var go = Node(name, parent, pos, size);
            Image image = Plate(go, button, Color.white);
            image.pixelsPerUnitMultiplier = 2.6f;

            Button b = go.AddComponent<Button>();
            b.targetGraphic = image;

            // 글자는 버튼 rect 전체를 덮고 가운데 정렬한다. 세로 중앙이 어긋나지 않는다.
            labelText = Label(go.transform, "Label", label, fontSize, textColour,
                Vector2.zero, size, centre: true);
            labelText.alignment = TextAlignmentOptions.Center;
            return b;
        }

        internal static void Bind(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[SceneBuilder] '{field}' 를 {target.GetType().Name} 에서 찾지 못했습니다.");
                return;
            }

            // 넣으려는 값이 이미 죽은 객체면 조용히 fileID 0 으로 저장돼 버린다. 소리를 내게 한다.
            if (value == null)
            {
                Debug.LogError($"[SceneBuilder] {target.GetType().Name}.{field} 에 넣을 값이 비어 있습니다.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BindBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[SceneBuilder] '{field}' 없음"); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BindFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[SceneBuilder] '{field}' 없음"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BindColor(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[SceneBuilder] '{field}' 없음"); return; }
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BindArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

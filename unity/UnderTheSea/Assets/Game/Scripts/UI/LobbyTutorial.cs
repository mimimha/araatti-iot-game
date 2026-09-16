using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnderTheSea.Account;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비에 처음 들어온 사람에게 조작법을 알려 주는 안내.
///
/// <b>알려 주는 것은 두 가지뿐이다.</b>
///   1. WASD 로 움직인다
///   2. 마우스로 둘러본다
/// 미니게임 입구는 <see cref="ProximityPortal"/> 이 가까이 가면 알아서 열어 주므로 다루지 않는다.
///
/// <b>화면을 가리지 않는다.</b>
/// 큰 판을 띄우지 않는다. 화면 아래 구석에 키 그림과 짧은 단어만 놓는다.
/// 게임을 멈추지도 않는다. 안내를 보는 동안에도 계속 걸어 다닐 수 있다.
///
/// <b>읽는 안내가 아니라 따라 하는 안내다.</b>
/// 확인 버튼이 없다. 그려진 키를 실제로 누르면 그 키에 불이 들어오고, 아래 실선이 차오른다.
/// 다 차면 다음으로 넘어간다. 손을 떼면 천천히 줄어든다.
/// 그래서 글을 읽지 않고 넘길 수 없고, 한 번은 직접 해 보게 된다.
///
///     [W][A][S][D] 이동   →  누르면 불이 들어옴  →  선이 참  →  다음
///     [마우스] 카메라      →  돌리면 화살표가 흔들림 →  선이 참  →  끝
///
/// <b>다른 UI 를 잠시 감추고 싶을 때.</b>
/// 채팅창·아이템창처럼 나중에 붙는 화면은 <see cref="Started"/> 와 <see cref="Finished"/> 를
/// 구독해 스스로 숨었다 나오면 된다. 이 스크립트는 남의 UI 를 직접 끄지 않는다.
/// 무엇이 생길지 모르는 채로 남의 오브젝트를 이름으로 찾아 끄면 나중에 반드시 어긋나기 때문이다.
///
///     void OnEnable()  { LobbyTutorial.Started += Hide; LobbyTutorial.Finished += Show; }
///     void OnDisable() { LobbyTutorial.Started -= Hide; LobbyTutorial.Finished -= Show; }
///
/// 늦게 켜지는 화면은 <see cref="IsRunning"/> 을 한 번 물어보면 된다.
///
/// <b>왜 코드로 만드는가.</b> <see cref="TransitionOverlay"/> 와 같은 이유다.
///   · Fusion 은 세션을 시작하면서 씬을 다시 로드한다. Lobby 씬에 올려 두면 그때 사라진다
///   · 큰 Lobby 씬을 건드리지 않아도 된다 (CONVENTION.md 3장 — 같은 Main 씬을 여럿이 고치지 않는다)
/// <see cref="UnityEngine.Object.DontDestroyOnLoad"/> 로 씬 재로드를 넘기고, 로비를 벗어나면 스스로 사라진다.
/// 화면에는 정상적으로 로비 위에 겹쳐 보인다. Overlay 캔버스는 소속 씬과 무관하게 화면 맨 위에 그려진다.
///
/// <b>나중에 디자인을 입히는 방법.</b> 코드를 고칠 필요 없다.
/// <c>Resources/LobbyTutorial.prefab</c> 을 만들어 두면 이 스크립트가 그것을 대신 띄운다.
/// 프리팹 안에서 아래 이름을 그대로 쓰면 각 부품이 자동으로 연결된다. 없는 것은 그냥 건너뛴다.
///
///     Label      TMP_Text   안내 문구가 들어간다
///     KeyGroup   GameObject WASD 안내에서만 켜진다. 자식에 W · A · S · D 이름의 Image
///     MouseGroup GameObject 마우스 안내에서만 켜진다
///     Progress   Image      Type 을 Filled 로 둔다. 진행도가 fillAmount 로 들어간다
///     ProgressTrack GameObject 진행선 전체. 마지막 문구에서 통째로 꺼진다
///
/// <b>한 번만 나온다.</b> 끝까지 본 사람에게는 다시 나오지 않는다.
/// 다시 보려면 에디터 메뉴 <c>Tools/아라아띠/튜토리얼 다시 보기</c>.
/// </summary>
public class LobbyTutorial : MonoBehaviour
{
    /// <summary>있으면 이 프리팹을 쓴다. 없으면 코드로 만든다.</summary>
    private const string ReplacementPrefabPath = "LobbyTutorial";

    /// <summary>
    /// **아직 튜토리얼을 봐야 하는 캐릭터**를 기억하는 PlayerPrefs 키의 앞머리.
    /// 뒤에 캐릭터 id 가 붙는다. (예: <c>LobbyTutorialPending_17</c>)
    ///
    /// ⚠ "봤다" 가 아니라 **"봐야 한다"** 를 적는다. 반대로 하면 이 기능이 생기기 전에
    ///    만들어진 캐릭터도, 남의 컴퓨터에서 처음 로그인한 캐릭터도 전부 튜토리얼을 본다.
    ///    캐릭터를 **만든 그 순간**에만 표시를 세워 두면 그런 일이 없다.
    ///
    /// 그래서 기록이 없으면 "안 봐도 된다" 로 친다. 처음 보는 캐릭터에게 굳이
    /// 띄우지 않는 쪽이 안전하다. 튜토리얼은 새로 시작한 사람을 위한 것이다.
    ///
    /// ── 나중에 서버로 옮길 때 ─────────────────────────────────
    ///
    ///   지금은 **이 컴퓨터에만** 남는다. 캐릭터를 만든 PC 가 아닌 곳에서 로그인하면
    ///   튜토리얼이 안 뜬다. 어디서든 한 번은 보게 하려면 캐릭터 테이블에 칸이 하나 필요하다.
    ///
    ///   갈아끼울 곳은 셋뿐이다. 나머지는 손댈 것이 없다.
    ///     <see cref="MarkPending"/>   생성할 때 세운다
    ///     <see cref="Pending"/>       띄울지 묻는다
    ///     <see cref="MarkDone"/>      끝나면 지운다
    /// </summary>
    public const string PendingKeyPrefix = "LobbyTutorialPending_";

    // 교체 프리팹에서도 찾아 쓰는 이름들. 바꾸면 프리팹 쪽도 같이 바꿔야 한다.
    private const string LabelName = "Label";
    private const string KeyGroupName = "KeyGroup";
    private const string MouseGroupName = "MouseGroup";
    private const string JumpGroupName = "JumpGroup";
    private const string ProgressName = "Progress";
    private const string ProgressTrackName = "ProgressTrack";

    /// <summary>
    /// 건너뛰기 버튼의 이름. 교체 프리팹에만 있고, 없으면 그 기능이 조용히 빠진다.
    ///
    /// ⚠ 이 버튼을 누를 수 있으려면 <b>버튼 쪽에 <see cref="CanvasGroup"/> 을 하나 더 달고
    ///    <c>ignoreParentGroups</c> 를 켜야 한다.</b> 안내 전체는 클릭을 안 받게
    ///    막아 두었기 때문이다(<c>blocksRaycasts = false</c>). 그러지 않으면 눌러도
    ///    아무 일도 일어나지 않는다.
    /// </summary>
    private const string SkipButtonName = "SkipButton";

    // 문구는 짧게 둔다. 화면 구석에서 한눈에 읽혀야 한다.
    private const string MoveText = "이동";
    /// <summary>
    /// 카메라 단계.
    ///
    /// ⚠ 다른 단계와 달리 **어떻게** 까지 적는다. 이동과 점프는 키를 그려 두면 끝이지만,
    ///    카메라는 "오른쪽 버튼을 누른 채로 끈다" 는 동작이라 그림만으로는 안 읽힌다.
    ///    실제로 마우스만 움직여 보고 아무 일도 안 일어나면 그 자리에서 막힌다.
    /// </summary>
    private const string LookText = "둘러보기\n<size=70%>오른쪽 버튼을 누른 채 움직이기</size>";
    private const string JumpText = "점프";

    /// <summary>
    /// 마지막 한마디. 조작이 아니라 **무엇을 하러 왔는지**를 말한다.
    ///
    /// ⚠ 앞의 것들과 달리 한 문장이라 길다. 그래서 이 단계에서는 키 그림을 끄고
    ///    글자 칸을 안내 상자 전체로 넓힌다. (<see cref="Show"/>)
    ///    좁은 칸 그대로 두면 석 줄로 접히면서 아래가 잘린다.
    /// </summary>
    private const string CloseText = "다양한 사람들을 만나, 바다의 심장 조각을 함께 모아보세요.";

    /// <summary>안내를 넘기기 전에 실제로 움직여야 하는 시간(초).</summary>
    private const float MoveHoldSeconds = 1.2f;

    /// <summary>손을 뗐을 때 진행선이 줄어드는 데 걸리는 시간(초). 채우는 것보다 느리게 둔다.</summary>
    private const float ReleaseDecaySeconds = 2.4f;

    /// <summary>시점을 다 돌렸다고 인정할 누적 각도.</summary>
    private const float LookYawDegrees = 90f;

    /// <summary>
    /// 몇 번 뛰어야 넘어가는가.
    ///
    /// 이동·둘러보기와 달리 점프는 **누르고 있는 것이 아니라 한 번씩 튀는 것**이다.
    /// 그래서 시간을 재지 않고 횟수를 센다. 두 번으로 둔 이유는, 한 번이면
    /// 다른 키를 누르다 우연히 눌러 지나갈 수 있어서다.
    /// </summary>
    private const int JumpCount = 2;

    /// <summary>마지막 문구를 띄워 두는 시간(초).</summary>
    private const float CloseSeconds = 2.5f;

    private const float FadeSeconds = 0.3f;

    // 로비는 밝은 모래 해변이고, 미니게임은 어두운 물속이다. 둘 다에서 읽혀야 한다.
    // 그래서 흰 반투명이 아니라 **어두운 박스에 흰 글자**로 간다.
    // 밝은 바탕에서는 어두운 박스가, 어두운 바탕에서는 흰 글자가 각각 살아난다.
    private static readonly Color KeyIdleColor = new Color(0.02f, 0.05f, 0.08f, 0.55f);
    private static readonly Color KeyPressedColor = new Color(0.30f, 0.82f, 1f, 0.98f);
    private static readonly Color KeyIdleTextColor = new Color(1f, 1f, 1f, 0.96f);
    private static readonly Color KeyPressedTextColor = new Color(0.02f, 0.10f, 0.18f, 1f);
    private static readonly Color LabelColor = Color.white;

    private static LobbyTutorial instance;

    private GameObject view;
    private CanvasGroup group;
    private TMP_Text label;
    /// <summary>글자 칸. 단계에 따라 넓혔다 줄인다.</summary>
    private RectTransform labelRect;

    /// <summary>화면을 코드로 만들었는가. 교체 프리팹을 쓴 경우에는 거짓이다.</summary>
    private bool builtInCode;

    // 키 그림이 있을 때는 그 옆에, 마지막 한마디에서는 상자 전체를 쓴다.
    //
    // 옆에 붙을 때도 40 이 아니라 96 인 이유: 카메라 단계가 두 줄이다.
    // 한 줄짜리(이동 · 점프)는 가운데 정렬이라 높이가 남아도 그대로 보인다.
    private static readonly Vector2 LabelSizeBeside = new Vector2(300f, 96f);
    private static readonly Vector2 LabelPosBeside = new Vector2(132f, 18f);
    private static readonly Vector2 LabelSizeAlone = new Vector2(420f, 96f);
    private static readonly Vector2 LabelPosAlone = new Vector2(0f, 12f);

    // 교체 프리팹이 글자 칸을 좌우로 늘려 둔 경우의 **왼쪽 여백**.
    // 키 그림이 있을 때는 그림을 피해 들어가고, 없을 때는 판 끝까지 쓴다.
    private const float LabelInsetBeside = 208f;
    private const float LabelInsetAlone = 32f;

    /// <summary>글자 칸이 좌우로 늘어나 있는가. 늘어나 있으면 여백만 조절해도 된다.</summary>
    private static bool IsStretchedSideways(RectTransform rect)
    {
        return Mathf.Approximately(rect.anchorMin.x, 0f)
               && Mathf.Approximately(rect.anchorMax.x, 1f);
    }

    private GameObject keyGroup;
    private GameObject mouseGroup;
    private GameObject jumpGroup;
    private Image progress;
    private GameObject progressTrack;

    private readonly List<KeyCap> caps = new List<KeyCap>();

    /// <summary>
    /// 점프 키 그림. <see cref="caps"/> 와 **따로 둔다.**
    ///
    /// ⚠ 같은 목록에 넣으면 <see cref="WaitForMove"/> 가 전부를 훑기 때문에
    ///    이동 단계에서 Space 를 눌러도 통과해 버린다.
    /// </summary>
    private readonly List<KeyCap> jumpCaps = new List<KeyCap>();
    private readonly List<RectTransform> mouseArrows = new List<RectTransform>();

    /// <summary>키 하나의 그림과 "지금 눌려 있는가" 를 묶어 둔다.</summary>
    private sealed class KeyCap
    {
        public Image Box;
        public TMP_Text Text;
        public Func<bool> IsPressed;
        public Vector3 RestScale;
    }

    /// <summary>지금 튜토리얼이 떠 있는가. 늦게 켜지는 UI 가 물어본다.</summary>
    public static bool IsRunning => instance != null;

    /// <summary>튜토리얼이 시작됐다. 채팅창·아이템창이 스스로 숨을 때 쓴다.</summary>
    public static event Action Started;

    /// <summary>튜토리얼이 끝났다(중간에 로비를 떠난 경우 포함). 숨었던 UI 가 다시 나온다.</summary>
    public static event Action Finished;

    /// <summary>
    /// 지금 조작 중인 캐릭터의 id. 아직 정해지지 않았으면 0.
    ///
    /// 서버가 원본이다. (CharacterSessionCache) 여기서는 읽기만 한다.
    /// </summary>
    private static long CurrentCharacterId
    {
        get
        {
            CharacterDto character = AccountServiceLocator.Characters?.CurrentCharacter;
            return character != null ? character.id : 0L;
        }
    }

    private static string PendingKeyOf(long characterId)
    {
        return PendingKeyPrefix + characterId;
    }

    /// <summary>
    /// **캐릭터를 막 만들었다.** 그 캐릭터가 로비에 처음 들어갈 때 튜토리얼을 띄운다.
    ///
    /// 캐릭터 생성 화면이 부른다. (<c>CharacterCustomizationPersistence</c>)
    /// </summary>
    public static void MarkPending(long characterId)
    {
        if (characterId == 0L)
        {
            return;
        }

        PlayerPrefs.SetInt(PendingKeyOf(characterId), 1);
        PlayerPrefs.Save();
    }

    /// <summary>이 캐릭터가 아직 튜토리얼을 봐야 하는가.</summary>
    private static bool Pending
    {
        get
        {
            long id = CurrentCharacterId;
            return id != 0L && PlayerPrefs.GetInt(PendingKeyOf(id), 0) != 0;
        }
    }

    /// <summary>이 캐릭터가 튜토리얼을 이미 끝냈는가. 에디터 메뉴가 쓴다.</summary>
    public static bool Seen => !Pending;

    /// <summary>
    /// 지금 캐릭터가 다음 로비 입장에서 튜토리얼을 다시 보게 한다. 에디터 메뉴가 쓴다.
    ///
    /// 캐릭터를 고르지 않은 상태(로그인 전 등)에서는 할 수 있는 것이 없다.
    /// </summary>
    public static void ClearSeen()
    {
        long id = CurrentCharacterId;

        if (id == 0L)
        {
            Debug.LogWarning(
                "[LobbyTutorial] 지금 고른 캐릭터가 없습니다. 로그인한 뒤에 다시 하세요.");
            return;
        }

        MarkPending(id);
    }

    /// <summary>튜토리얼을 끝냈다. 이 캐릭터에게는 다시 나오지 않는다.</summary>
    private static void MarkDone(long characterId)
    {
        if (characterId == 0L)
        {
            return;
        }

        PlayerPrefs.DeleteKey(PendingKeyOf(characterId));
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
        // TransitionOverlay 와 같은 판단 기준을 쓴다.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            return;
        }

        LocalPlayer.Registered += CreateWhenNeeded;
    }

    /// <summary>
    /// 내 캐릭터가 로비에 생긴 순간에만 만든다.
    ///
    /// 씬 로드 시점이 아니라 캐릭터가 생긴 시점인 이유는, Fusion 에서는 접속이 끝나야
    /// 캐릭터가 스폰되기 때문이다. 그전에 "이동" 을 띄우면 움직일 몸이 없다.
    /// </summary>
    private static void CreateWhenNeeded(NetworkObject player)
    {
        // 새로 만든 캐릭터에게만 띄운다. 이미 있던 캐릭터는 그냥 지나간다.
        if (instance != null || !Pending)
        {
            return;
        }

        // 미니게임 씬에도 캐릭터는 스폰된다. 로비에서만 띄운다.
        if (!InLobby())
        {
            return;
        }

        GameObject host = new GameObject(nameof(LobbyTutorial));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<LobbyTutorial>();
    }

    private void Awake()
    {
        instance = this;

        // 교체 프리팹이 있으면 그것을 쓰고, 없으면 코드로 만든다.
        // 어느 쪽인지 기억해 둔다 — 남의 배치를 코드가 밀어내면 안 된다. (Show)
        view = LoadReplacement();
        builtInCode = view == null;

        if (builtInCode)
        {
            view = BuildFallbackView();
        }

        view.transform.SetParent(transform, worldPositionStays: false);

        group = view.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = view.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        // 안내는 보여 주기만 한다. 클릭을 가로채면 뒤쪽 조작이 막힌다.
        group.interactable = false;
        group.blocksRaycasts = false;

        Bind();

        Started?.Invoke();

        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;

        // 끝까지 봤든 중간에 로비를 떠났든, 숨었던 UI 는 반드시 다시 나와야 한다.
        Finished?.Invoke();
    }

    /// <summary>
    /// 화면 부품을 이름으로 찾아 둔다.
    /// 교체 프리팹에 없는 부품은 null 로 남고, 그 부품을 쓰는 연출만 조용히 빠진다.
    /// </summary>
    private void Bind()
    {
        label = FindByName<TMP_Text>(LabelName) ?? view.GetComponentInChildren<TMP_Text>(includeInactive: true);
        labelRect = label != null ? label.rectTransform : null;
        keyGroup = FindChild(KeyGroupName);
        mouseGroup = FindChild(MouseGroupName);
        jumpGroup = FindChild(JumpGroupName);
        progress = FindByName<Image>(ProgressName);

        // 진행선은 바탕과 채움이 한 쌍이다. 껐다 켤 때는 통째로 다룬다.
        // 채움만 끄면 빈 바탕이 덩그러니 남고, 교체 프리팹이 바탕을 안 만들었으면 채움만 끈다.
        progressTrack = FindChild(ProgressTrackName)
                        ?? (progress != null ? progress.gameObject : null);

        Button skip = FindByName<Button>(SkipButtonName);

        if (skip != null)
        {
            skip.onClick.AddListener(Skip);
        }

        // 점프 키는 자기 그룹에서 찾는다. 이동 키와 목록이 섞이면 안 된다.
        if (jumpGroup != null)
        {
            BindCap(jumpGroup, "Space", () => Pressed(k => k.spaceKey), jumpCaps);
        }

        if (keyGroup == null)
        {
            return;
        }

        // 키 그림과 실제 키 입력을 잇는다. 이름이 맞는 것만 연결한다.
        BindCap(keyGroup, "W", () => Pressed(k => k.wKey), caps);
        BindCap(keyGroup, "A", () => Pressed(k => k.aKey), caps);
        BindCap(keyGroup, "S", () => Pressed(k => k.sKey), caps);
        BindCap(keyGroup, "D", () => Pressed(k => k.dKey), caps);
    }

    private void BindCap(GameObject where, string name, Func<bool> isPressed, List<KeyCap> into)
    {
        Transform found = FindIn(where.transform, name);
        if (found == null)
        {
            return;
        }

        Image box = found.GetComponent<Image>();
        if (box == null)
        {
            return;
        }

        into.Add(new KeyCap
        {
            Box = box,
            Text = found.GetComponentInChildren<TMP_Text>(includeInactive: true),
            IsPressed = isPressed,
            RestScale = found.localScale,
        });
    }

    /// <summary>창에 포커스가 있을 때만 눌린 것으로 본다. PlayerInputProvider 와 같은 기준이다.</summary>
    private static bool Pressed(Func<Keyboard, KeyControl> pick)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && Application.isFocused && pick(keyboard).isPressed;
    }

    /// <summary>건너뛰기를 이미 눌렀는가. 두 번 눌려도 한 번만 먹는다.</summary>
    private bool skipping;

    /// <summary>
    /// 건너뛰기. 남은 단계를 버리고 바로 끝낸다.
    ///
    /// <b>본 것으로 친다.</b> 다음에 로비에 들어와도 다시 뜨지 않는다.
    /// 건너뛴 사람은 이미 조작을 안다는 뜻이고, 매번 다시 띄우면 그 버튼이 무의미해진다.
    /// 다시 보고 싶으면 개발자 메뉴에 <c>Tools/아라아띠/튜토리얼 다시 보기</c> 가 있다.
    /// </summary>
    public void Skip()
    {
        if (skipping)
        {
            return;
        }

        skipping = true;

        // ⚠ 돌고 있던 단계를 반드시 멈춘다. 안 멈추면 사라지는 중에도
        //    다음 단계가 켜지면서 한 번 깜빡인다.
        StopAllCoroutines();

        StartCoroutine(FinishNow());
    }

    private IEnumerator FinishNow()
    {
        yield return FadeTo(0f);

        MarkDone(CurrentCharacterId);

        Destroy(gameObject);
    }

    /// <summary>안내 순서. 한 단계를 해내면 다음으로 넘어간다.</summary>
    private IEnumerator Run()
    {
        yield return WaitForScreenToClear();

        // 접속에 실패했으면 화면에는 실패 사유가 떠 있다. 그 위에 조작 안내를 얹지 않는다.
        if (TransitionStatus.Current == TransitionStatus.Phase.Failed)
        {
            Destroy(gameObject);
            yield break;
        }

        yield return Show(MoveText, keys: true, mouse: false);
        yield return WaitForMove();

        yield return Show(LookText, keys: false, mouse: true);
        yield return WaitForLook();

        yield return Show(JumpText, keys: false, mouse: false, jump: true);
        yield return WaitForJump();

        yield return Show(CloseText, keys: false, mouse: false);
        yield return new WaitForSeconds(CloseSeconds);

        yield return FadeTo(0f);

        // 끝까지 본 캐릭터만 표시를 지운다. 중간에 로비를 떠났다면 다음에 다시 보여 준다.
        MarkDone(CurrentCharacterId);

        Destroy(gameObject);
    }

    /// <summary>
    /// 전환 암전이 걷힐 때까지 기다린다.
    ///
    /// 캐릭터는 <see cref="TransitionStatus"/> 가 아직 <c>Loading</c> 인 동안 스폰된다.
    /// 그때 바로 안내를 띄우면 <see cref="TransitionOverlay"/> 의 검은 화면 뒤에서
    /// 페이드인이 끝나 버린다. 실제로 확인했다 — 첫 안내를 아무도 못 본다.
    ///
    /// 그래서 화면이 열린 뒤에 시작한다. 조작을 할 수 있게 된 순간과 안내가 나오는 순간을 맞춘다.
    /// </summary>
    private IEnumerator WaitForScreenToClear()
    {
        while (TransitionStatus.Current == TransitionStatus.Phase.Loading)
        {
            if (LeftLobby())
            {
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// WASD 를 <see cref="MoveHoldSeconds"/> 만큼 눌러 움직이면 통과.
    /// 누르는 동안 해당 키에 불이 들어오고 선이 찬다. 손을 떼면 천천히 줄어든다.
    /// </summary>
    private IEnumerator WaitForMove()
    {
        float held = 0f;

        while (held < MoveHoldSeconds)
        {
            if (LeftLobby())
            {
                yield break;
            }

            bool moving = false;

            // 눌린 키에 불을 켠다. 어느 키가 먹히는지 눈으로 바로 알 수 있다.
            foreach (KeyCap cap in caps)
            {
                bool down = cap.IsPressed();
                moving |= down;
                Highlight(cap, down);
            }

            held = moving
                ? held + Time.deltaTime
                // 완전히 0 으로 되돌리지 않고 천천히 깎는다. 잠깐 손을 뗐다고 처음부터 하면 지친다.
                : Mathf.Max(0f, held - Time.deltaTime * (MoveHoldSeconds / ReleaseDecaySeconds));

            SetProgress(held / MoveHoldSeconds);

            yield return null;
        }

        SetProgress(1f);

        // 마지막으로 켠 불을 끄고 넘어간다.
        foreach (KeyCap cap in caps)
        {
            Highlight(cap, false);
        }
    }

    /// <summary>
    /// <see cref="JumpCount"/> 번 뛰면 통과.
    ///
    /// ⚠ 다른 단계와 달리 **누른 시간이 아니라 누른 횟수**를 센다. 점프는 한 번씩 튀는
    ///    동작이라 붙잡고 있을 수가 없다. 그래서 진행선도 줄어들지 않는다.
    ///    한 번 뛴 것을 도로 빼앗으면 무엇을 잘못했는지 알 수가 없다.
    /// </summary>
    private IEnumerator WaitForJump()
    {
        int jumped = 0;
        bool wasDown = false;

        while (jumped < JumpCount)
        {
            if (LeftLobby())
            {
                yield break;
            }

            bool down = false;

            // 눌린 동안 키에 불을 켠다. 이동 단계와 같은 감각으로 보이게 한다.
            foreach (KeyCap cap in jumpCaps)
            {
                down |= cap.IsPressed();
                Highlight(cap, down);
            }

            // 누르고 있는 것이 아니라 **새로 누른 순간**만 센다.
            // 안 그러면 꾹 누르고 있기만 해도 게이지가 차서, 뛰지 않고 넘어간다.
            if (down && !wasDown)
            {
                jumped++;
            }

            wasDown = down;

            SetProgress((float)jumped / JumpCount);

            yield return null;
        }

        SetProgress(1f);

        foreach (KeyCap cap in jumpCaps)
        {
            Highlight(cap, false);
        }
    }

    /// <summary>시점을 <see cref="LookYawDegrees"/> 만큼 돌리면 통과.</summary>
    private IEnumerator WaitForLook()
    {
        Camera eye = Camera.main;

        // 카메라가 없는 씬에서는 판정할 기준이 없다. 붙잡아 두지 않고 넘긴다.
        if (eye == null)
        {
            yield break;
        }

        float turned = 0f;
        float previous = eye.transform.eulerAngles.y;

        while (turned < LookYawDegrees)
        {
            if (LeftLobby() || eye == null)
            {
                yield break;
            }

            float now = eye.transform.eulerAngles.y;
            // 0도와 360도 사이를 넘어가도 한 프레임의 변화량은 작다. 그 값을 쌓는다.
            turned += Mathf.Abs(Mathf.DeltaAngle(previous, now));
            previous = now;

            PulseArrows();
            SetProgress(turned / LookYawDegrees);

            yield return null;
        }

        SetProgress(1f);
    }

    /// <summary>좌우 화살표를 번갈아 흔들어 "마우스를 옆으로 움직여라" 를 몸짓으로 보여 준다.</summary>
    private void PulseArrows()
    {
        if (mouseArrows.Count == 0)
        {
            return;
        }

        // 좌우가 반대 위상으로 움직인다. 한쪽이 나가면 한쪽이 들어온다.
        float swing = Mathf.Sin(Time.time * 4f) * 5f;

        for (int i = 0; i < mouseArrows.Count; i++)
        {
            RectTransform arrow = mouseArrows[i];
            if (arrow == null)
            {
                continue;
            }

            float direction = i % 2 == 0 ? -1f : 1f;
            Vector2 at = arrow.anchoredPosition;
            arrow.anchoredPosition = new Vector2(direction * (34f + swing), at.y);
        }
    }

    private static void Highlight(KeyCap cap, bool on)
    {
        if (cap.Box != null)
        {
            cap.Box.color = on ? KeyPressedColor : KeyIdleColor;
            // 눌린 느낌을 주려고 살짝 눌러 준다.
            cap.Box.rectTransform.localScale = on ? cap.RestScale * 0.9f : cap.RestScale;
        }

        if (cap.Text != null)
        {
            cap.Text.color = on ? KeyPressedTextColor : KeyIdleTextColor;
        }
    }

    private void SetProgress(float ratio)
    {
        if (progress != null)
        {
            progress.fillAmount = Mathf.Clamp01(ratio);
        }
    }

    /// <summary>
    /// 지금 로비에 있는가.
    ///
    /// <b>캐릭터가 속한 씬을 보면 안 된다.</b> Fusion 이 스폰한 오브젝트는 Lobby 씬이 아니라
    /// 러너 전용 씬(<c>NetworkManager_[Player:3]</c> 같은 이름)에 들어간다. 실제로 확인했다.
    /// 활성 씬도 기준이 못 된다. Fusion 이 로비를 네트워크 씬으로 additive 로 올리는
    /// 경로(<see cref="SceneFlow.LobbyLoadedByNetwork"/>)에서는 활성 씬이 아직 ChannelSelect 일 수 있다.
    ///
    /// 그래서 "Lobby 씬이 지금 로드돼 있는가" 를 먼저 본다.
    ///
    /// ⚠ **그것만으로는 모자랐다.** 실제로 로비에 들어가 보면 씬이 하나뿐이고 이름이
    ///    <c>FusionRunner (Client)_[Player:3]</c> 다. 로비는 **씬이 아니라 그 안의 루트
    ///    오브젝트** <c>[Lobby]</c> 로 들어와 있다. 이름이 "Lobby" 인 씬은 없다.
    ///    그래서 이 판정이 늘 거짓이었고, 튜토리얼이 한 번도 뜨지 않았다.
    ///
    ///    씬으로 올라오는 경로가 없어진 것은 아니므로 둘 다 본다. 하나라도 맞으면 로비다.
    /// </summary>
    private static bool InLobby()
    {
        Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);

        if (lobby.IsValid() && lobby.isLoaded)
        {
            return true;
        }

        return HasLobbyRoot();
    }

    /// <summary>
    /// 로비 내용을 담은 루트 오브젝트가 있는가. Fusion 이 러너 씬 안에 넣는 경우다.
    ///
    /// 이름이 <c>[Lobby]</c> 처럼 대괄호로 감싸여 오므로 둘 다 받아 준다.
    /// </summary>
    private static bool HasLobbyRoot()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
            {
                continue;
            }

            // ⚠ 할당 없는 쪽을 쓴다. 이 판정은 대기 중 **매 프레임** 불린다. (LeftLobby)
            scene.GetRootGameObjects(rootBuffer);

            for (int r = 0; r < rootBuffer.Count; r++)
            {
                string name = rootBuffer[r].name;

                if (name == SceneFlow.Lobby || name == "[" + SceneFlow.Lobby + "]")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>루트 오브젝트를 담아 두는 그릇. 매 프레임 새로 만들지 않으려고 돌려 쓴다.</summary>
    private static readonly List<GameObject> rootBuffer = new List<GameObject>();

    /// <summary>로비를 벗어났거나 캐릭터가 사라졌으면 안내를 걷는다.</summary>
    private bool LeftLobby()
    {
        // 캐릭터가 사라지면(퇴장·세션 종료) 안내할 대상이 없다.
        if (LocalPlayer.Exists && InLobby())
        {
            return false;
        }

        Destroy(gameObject);
        return true;
    }

    /// <summary>문구를 갈아 끼우고, 이번 단계에 쓰는 그림만 켠다.</summary>
    private IEnumerator Show(string text, bool keys, bool mouse, bool jump = false)
    {
        if (group.alpha > 0f)
        {
            yield return FadeTo(0f);
        }

        if (label != null)
        {
            label.text = text;
        }

        if (keyGroup != null)
        {
            keyGroup.SetActive(keys);
        }

        if (mouseGroup != null)
        {
            mouseGroup.SetActive(mouse);
        }

        if (jumpGroup != null)
        {
            jumpGroup.SetActive(jump);
        }

        bool hasGraphic = keys || mouse || jump;

        // 마지막 한마디에는 진행선이 필요 없다. 할 일이 없기 때문이다.
        if (progressTrack != null)
        {
            progressTrack.SetActive(hasGraphic);
        }

        // 그림이 빠진 자리를 글자가 물려받는다. 마지막 한마디는 한 문장이라 좁은 칸에 안 들어간다.
        //
        // ⚠ 교체 프리팹을 쓰는 경우에는 그쪽 배치를 존중해 건드리지 않는다.
        //    코드로 만든 화면일 때만 위치를 안다.
        if (labelRect != null && builtInCode)
        {
            labelRect.sizeDelta = hasGraphic ? LabelSizeBeside : LabelSizeAlone;
            labelRect.anchoredPosition = hasGraphic ? LabelPosBeside : LabelPosAlone;
        }
        else if (labelRect != null && IsStretchedSideways(labelRect))
        {
            // 교체 프리팹이 글자 칸을 **좌우로 늘려** 두었다면, 그림이 빠진 단계에서
            // 왼쪽 여백만 줄여 준다. 그림 자리까지 글자가 물려받는다.
            //
            // 늘려 두지 않았다면 그쪽이 자리를 직접 정한 것이니 건드리지 않는다.
            // 좌우로 늘린 칸은 "남는 만큼 쓰겠다" 는 뜻이라고 본다.
            Vector2 offset = labelRect.offsetMin;
            offset.x = hasGraphic ? LabelInsetBeside : LabelInsetAlone;
            labelRect.offsetMin = offset;
        }

        SetProgress(0f);

        yield return FadeTo(1f);
    }

    private IEnumerator FadeTo(float target)
    {
        float from = group.alpha;
        float elapsed = 0f;

        while (elapsed < FadeSeconds)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, target, elapsed / FadeSeconds);
            yield return null;
        }

        group.alpha = target;
    }

    private T FindByName<T>(string name) where T : Component
    {
        Transform found = FindIn(view.transform, name);
        return found != null ? found.GetComponent<T>() : null;
    }

    private GameObject FindChild(string name)
    {
        Transform found = FindIn(view.transform, name);
        return found != null ? found.gameObject : null;
    }

    /// <summary>꺼져 있는 것까지 포함해 이름으로 찾는다. Transform.Find 는 깊이 들어가지 않는다.</summary>
    private static Transform FindIn(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static GameObject LoadReplacement()
    {
        GameObject prefab = Resources.Load<GameObject>(ReplacementPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        Debug.Log($"[LobbyTutorial] 교체 프리팹을 사용합니다: Resources/{ReplacementPrefabPath}");
        return Instantiate(prefab);
    }

    // ------------------------------------------------------------
    // 임시 외형
    //
    // 화면 아래 가운데에 놓인 작은 줄 하나가 전부다. 판도 테두리도 없다.
    //
    //        [W]
    //     [A][S][D]  이동
    //     ───────────────
    //
    // 게임 화면을 최대한 덮지 않는 것이 목적이다.
    // 디자인은 Resources/LobbyTutorial.prefab 쪽에서 하면 되고, 그때 이 코드는 쓰이지 않는다.
    // ------------------------------------------------------------

    private const float CapSize = 44f;
    private const float CapGap = 50f;

    /// <summary>글자에 깔아 주는 그림자. 밝은 배경에서도 읽히게 한다.</summary>
    private static void AddShadow(GameObject target)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private GameObject BuildFallbackView()
    {
        GameObject root = new GameObject("View", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // TransitionOverlay(전환 암전) 보다는 아래에 둔다. 전환 중에는 가려져야 한다.
        canvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 배경 없는 빈 틀. 화면 **왼쪽 아래** 구석에 붙는다.
        //
        // 가운데 아래에 두면 캐릭터와 겹친다. 로비 카메라가 캐릭터를 화면 아래쪽에 두기 때문이다.
        // 실제로 확인했다 — "이동" 글자가 캐릭터 등판 위에 얹혔다.
        // 구석은 캐릭터도 지형도 가리지 않고, 시선을 화면 중앙에서 뺏지도 않는다.
        GameObject hint = NewRect("Hint", root.transform, new Vector2(420f, 140f));
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.zero;
        hintRect.pivot = Vector2.zero;
        hintRect.anchoredPosition = new Vector2(56f, 56f);

        keyGroup = BuildKeyGroup(hint.transform);
        mouseGroup = BuildMouseGroup(hint.transform);
        jumpGroup = BuildJumpGroup(hint.transform);
        BuildLabel(hint.transform);
        BuildProgress(hint.transform);

        return root;
    }

    /// <summary>그림 오른쪽에 붙는 짧은 단어. 젤다의 "이동" 처럼 한두 단어만 둔다.</summary>
    private static void BuildLabel(Transform parent)
    {
        // 키 그림과 붙지 않게 띄운다. 붙으면 D 키와 글자가 한 덩어리로 보인다.
        // 단계마다 Show 가 다시 잡아 주므로 여기 값은 첫 프레임용이다.
        GameObject text = NewRect(LabelName, parent, LabelSizeBeside);
        text.GetComponent<RectTransform>().anchoredPosition = LabelPosBeside;

        TextMeshProUGUI tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.text = MoveText;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.fontSize = 30f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LabelColor;
        tmp.raycastTarget = false;

        AddShadow(text);
    }

    /// <summary>W 를 위에, A S D 를 아래에 둔 익숙한 배치.</summary>
    private static GameObject BuildKeyGroup(Transform parent)
    {
        GameObject root = NewRect(KeyGroupName, parent, new Vector2(150f, 100f));
        root.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, 18f);

        BuildKeyCap(root.transform, "W", new Vector2(0f, CapGap * 0.5f));
        BuildKeyCap(root.transform, "A", new Vector2(-CapGap, -CapGap * 0.5f));
        BuildKeyCap(root.transform, "S", new Vector2(0f, -CapGap * 0.5f));
        BuildKeyCap(root.transform, "D", new Vector2(CapGap, -CapGap * 0.5f));

        return root;
    }

    /// <summary>
    /// 스페이스바 하나. 다른 키보다 **가로로 길게** 그린다.
    ///
    /// 실제 자판에서 제일 긴 키라, 길이만으로 무슨 키인지 알아본다.
    /// 글자를 못 읽어도 손이 먼저 간다.
    /// </summary>
    private static GameObject BuildJumpGroup(Transform parent)
    {
        GameObject root = NewRect(JumpGroupName, parent, new Vector2(150f, 100f));
        root.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, 18f);
        root.SetActive(false);

        BuildKeyCap(root.transform, "Space", Vector2.zero, new Vector2(CapSize * 3.2f, CapSize), 18f);

        return root;
    }

    private static void BuildKeyCap(Transform parent, string name, Vector2 at)
    {
        BuildKeyCap(parent, name, at, new Vector2(CapSize, CapSize), 24f);
    }

    private static void BuildKeyCap(Transform parent, string name, Vector2 at, Vector2 size, float fontSize)
    {
        GameObject cap = NewRect(name, parent, size);
        cap.GetComponent<RectTransform>().anchoredPosition = at;

        Image box = cap.AddComponent<Image>();
        box.color = KeyIdleColor;
        box.raycastTarget = false;

        GameObject text = NewRect("Text", cap.transform, size);
        TextMeshProUGUI tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = KeyIdleTextColor;
        tmp.raycastTarget = false;

        AddShadow(text);
    }

    /// <summary>마우스 몸통 하나와 좌우 화살표. 화살표는 진행 중에 흔들린다.</summary>
    private GameObject BuildMouseGroup(Transform parent)
    {
        GameObject root = NewRect(MouseGroupName, parent, new Vector2(150f, 100f));
        root.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, 18f);
        root.SetActive(false);

        GameObject body = NewRect("Body", root.transform, new Vector2(38f, 56f));
        Image bodyImage = body.AddComponent<Image>();
        bodyImage.color = KeyIdleColor;
        bodyImage.raycastTarget = false;

        // 휠 자리. 마우스처럼 보이게 하는 최소한의 표시다.
        GameObject wheel = NewRect("Wheel", body.transform, new Vector2(4f, 14f));
        wheel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 13f);
        Image wheelImage = wheel.AddComponent<Image>();
        wheelImage.color = KeyIdleTextColor;
        wheelImage.raycastTarget = false;

        // 오른쪽 버튼. **눌러야 하는 곳이라 불이 들어온 색으로 칠한다.**
        //
        // ⚠ 이것이 없으면 마우스를 움직여 보고 아무 일도 안 일어나 막힌다.
        //    카메라는 우클릭을 누르고 있는 동안에만 돈다. (LocalPlayerView)
        GameObject rightButton = NewRect("RightButton", body.transform, new Vector2(15f, 20f));
        rightButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 16f);
        Image rightImage = rightButton.AddComponent<Image>();
        rightImage.color = KeyPressedColor;
        rightImage.raycastTarget = false;

        mouseArrows.Add(BuildArrow(root.transform, "ArrowLeft", "◀", -34f));
        mouseArrows.Add(BuildArrow(root.transform, "ArrowRight", "▶", 34f));

        return root;
    }

    private static RectTransform BuildArrow(Transform parent, string name, string glyph, float x)
    {
        GameObject arrow = NewRect(name, parent, new Vector2(30f, 30f));
        RectTransform rect = arrow.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(x, 0f);

        TextMeshProUGUI tmp = arrow.AddComponent<TextMeshProUGUI>();
        tmp.text = glyph;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 22f;
        tmp.color = KeyPressedColor;
        tmp.raycastTarget = false;

        AddShadow(arrow);

        return rect;
    }

    /// <summary>얼마나 했는지 보여 주는 가는 선. 채워진 만큼만 그려진다.</summary>
    private void BuildProgress(Transform parent)
    {
        GameObject track = NewRect(ProgressTrackName, parent, new Vector2(300f, 3f));
        track.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -38f);

        Image trackImage = track.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.45f);
        trackImage.raycastTarget = false;

        GameObject fill = NewRect(ProgressName, track.transform, new Vector2(300f, 3f));

        progress = fill.AddComponent<Image>();
        progress.color = KeyPressedColor;
        progress.raycastTarget = false;
        // sprite 가 없으면 Filled 가 먹지 않는다. 흰 사각형 하나를 만들어 쓴다.
        progress.sprite = WhiteSprite();
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = (int)Image.OriginHorizontal.Left;
        progress.fillAmount = 0f;
    }

    private static Sprite whiteSprite;

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        whiteSprite = Sprite.Create(
            Texture2D.whiteTexture, new UnityEngine.Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        whiteSprite.name = "LobbyTutorialWhite";
        return whiteSprite;
    }

    /// <summary>가운데 기준으로 놓이는 빈 RectTransform 하나.</summary>
    private static GameObject NewRect(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        return go;
    }
}

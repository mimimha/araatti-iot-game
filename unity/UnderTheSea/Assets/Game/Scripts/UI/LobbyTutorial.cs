using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
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

    /// <summary>끝까지 본 사람을 기억하는 PlayerPrefs 키.</summary>
    public const string SeenKey = "LobbyTutorialSeen";

    // 교체 프리팹에서도 찾아 쓰는 이름들. 바꾸면 프리팹 쪽도 같이 바꿔야 한다.
    private const string LabelName = "Label";
    private const string KeyGroupName = "KeyGroup";
    private const string MouseGroupName = "MouseGroup";
    private const string ProgressName = "Progress";
    private const string ProgressTrackName = "ProgressTrack";

    // 문구는 짧게 둔다. 화면 구석에서 한눈에 읽혀야 한다.
    private const string MoveText = "이동";
    private const string LookText = "둘러보기";
    private const string CloseText = "빛나는 문으로 가 보세요";

    /// <summary>안내를 넘기기 전에 실제로 움직여야 하는 시간(초).</summary>
    private const float MoveHoldSeconds = 1.2f;

    /// <summary>손을 뗐을 때 진행선이 줄어드는 데 걸리는 시간(초). 채우는 것보다 느리게 둔다.</summary>
    private const float ReleaseDecaySeconds = 2.4f;

    /// <summary>시점을 다 돌렸다고 인정할 누적 각도.</summary>
    private const float LookYawDegrees = 90f;

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
    private GameObject keyGroup;
    private GameObject mouseGroup;
    private Image progress;
    private GameObject progressTrack;

    private readonly List<KeyCap> caps = new List<KeyCap>();
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

    /// <summary>이 사람이 튜토리얼을 이미 끝까지 봤는가.</summary>
    public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

    /// <summary>다음 로비 입장에서 튜토리얼이 다시 나오게 한다. 에디터 메뉴가 쓴다.</summary>
    public static void ClearSeen()
    {
        PlayerPrefs.DeleteKey(SeenKey);
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
        if (instance != null || Seen)
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

        view = LoadReplacement() ?? BuildFallbackView();
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
        keyGroup = FindChild(KeyGroupName);
        mouseGroup = FindChild(MouseGroupName);
        progress = FindByName<Image>(ProgressName);

        // 진행선은 바탕과 채움이 한 쌍이다. 껐다 켤 때는 통째로 다룬다.
        // 채움만 끄면 빈 바탕이 덩그러니 남고, 교체 프리팹이 바탕을 안 만들었으면 채움만 끈다.
        progressTrack = FindChild(ProgressTrackName)
                        ?? (progress != null ? progress.gameObject : null);

        if (keyGroup == null)
        {
            return;
        }

        // 키 그림과 실제 키 입력을 잇는다. 이름이 맞는 것만 연결한다.
        BindCap("W", () => Pressed(k => k.wKey));
        BindCap("A", () => Pressed(k => k.aKey));
        BindCap("S", () => Pressed(k => k.sKey));
        BindCap("D", () => Pressed(k => k.dKey));
    }

    private void BindCap(string name, Func<bool> isPressed)
    {
        Transform found = FindIn(keyGroup.transform, name);
        if (found == null)
        {
            return;
        }

        Image box = found.GetComponent<Image>();
        if (box == null)
        {
            return;
        }

        caps.Add(new KeyCap
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

        yield return Show(CloseText, keys: false, mouse: false);
        yield return new WaitForSeconds(CloseSeconds);

        yield return FadeTo(0f);

        // 끝까지 본 사람에게만 기억한다. 중간에 로비를 떠났다면 다음에 다시 보여 준다.
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

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
    /// 그래서 "Lobby 씬이 지금 로드돼 있는가" 만 본다. 두 경로에서 모두 맞는 유일한 기준이다.
    /// </summary>
    private static bool InLobby()
    {
        Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);
        return lobby.IsValid() && lobby.isLoaded;
    }

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
    private IEnumerator Show(string text, bool keys, bool mouse)
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

        // 마지막 한마디에는 진행선이 필요 없다. 할 일이 없기 때문이다.
        if (progressTrack != null)
        {
            progressTrack.SetActive(keys || mouse);
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
        BuildLabel(hint.transform);
        BuildProgress(hint.transform);

        return root;
    }

    /// <summary>그림 오른쪽에 붙는 짧은 단어. 젤다의 "이동" 처럼 한두 단어만 둔다.</summary>
    private static void BuildLabel(Transform parent)
    {
        GameObject text = NewRect(LabelName, parent, new Vector2(240f, 40f));
        // 키 그림과 붙지 않게 띄운다. 붙으면 D 키와 글자가 한 덩어리로 보인다.
        text.GetComponent<RectTransform>().anchoredPosition = new Vector2(132f, 18f);

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

    private static void BuildKeyCap(Transform parent, string name, Vector2 at)
    {
        GameObject cap = NewRect(name, parent, new Vector2(CapSize, CapSize));
        cap.GetComponent<RectTransform>().anchoredPosition = at;

        Image box = cap.AddComponent<Image>();
        box.color = KeyIdleColor;
        box.raycastTarget = false;

        GameObject text = NewRect("Text", cap.transform, new Vector2(CapSize, CapSize));
        TextMeshProUGUI tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24f;
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

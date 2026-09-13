using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 전환이 끝날 때까지 화면을 덮는 임시 오버레이.
///
/// <b>이 스크립트가 아는 것은 하나뿐이다</b> — <see cref="TransitionStatus"/> 의
/// 상태(<c>Loading / Ready / Failed</c>)와 표시할 문구.
/// Fusion 도, Lobby 인지 Game 인지도, 무엇을 기다리는 중인지도 모른다.
/// 그래서 나중에 전환 종류가 늘어나도 이 파일은 건드릴 일이 없다.
///
/// <b>왜 코드로 만드는가.</b>
/// 씬에 미리 놓으면 두 가지가 걸린다.
///   · 전환이 없는 화면(Login 등)에서도 떠 있게 된다
///   · Fusion 은 세션을 시작하면서 씬을 다시 로드한다. 씬에 있으면 그때 사라진다
/// 그래서 <see cref="TransitionStatus"/> 가 처음 가릴 일이 생겼을 때 만들고
/// <see cref="Object.DontDestroyOnLoad"/> 로 씬 재로드를 넘긴다.
/// 큰 씬(Lobby)을 수정하지 않아도 되는 것은 덤이다.
///
/// <b>나중에 교체하는 방법.</b> 코드를 고칠 필요 없다.
/// <c>Resources/TransitionOverlay.prefab</c> 을 만들어 두면 이 스크립트가 그것을 대신 띄운다.
/// 프리팹 안에 <see cref="TMP_Text"/> 가 있으면 문구를 그쪽에 넣는다.
/// 없으면 아래 <see cref="BuildFallbackView"/> 가 검은 패널 + 글자를 만든다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
public class TransitionOverlay : MonoBehaviour
{
    /// <summary>있으면 이 프리팹을 쓴다. 없으면 코드로 만든다.</summary>
    private const string ReplacementPrefabPath = "TransitionOverlay";

    private const string DefaultLoadingText = "불러오는 중...";
    private const string DefaultFailedText = "전환에 실패했습니다.";

    private static TransitionOverlay instance;

    private GameObject view;
    private TMP_Text label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
        // Fusion 을 묻지 않고 "그릴 장치가 있는가" 만 본다.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            return;
        }

        TransitionStatus.Changed += CreateWhenNeeded;
    }

    /// <summary>가릴 일이 생기는 첫 순간에만 만든다. 계속 Ready 인 화면에서는 만들지 않는다.</summary>
    private static void CreateWhenNeeded(TransitionStatus.Phase phase, string message)
    {
        if (instance != null)
        {
            return;
        }

        if (phase != TransitionStatus.Phase.Loading && phase != TransitionStatus.Phase.Failed)
        {
            return;
        }

        GameObject host = new GameObject(nameof(TransitionOverlay));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<TransitionOverlay>();
    }

    private void Awake()
    {
        instance = this;

        view = LoadReplacement() ?? BuildFallbackView();
        view.transform.SetParent(transform, worldPositionStays: false);

        label = view.GetComponentInChildren<TMP_Text>(includeInactive: true);

        Apply(TransitionStatus.Current, TransitionStatus.Message);
        TransitionStatus.Changed += Apply;
    }

    private void OnDestroy()
    {
        TransitionStatus.Changed -= Apply;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Apply(TransitionStatus.Phase phase, string message)
    {
        switch (phase)
        {
            case TransitionStatus.Phase.Ready:
            case TransitionStatus.Phase.Idle:
                // 준비가 끝났다. 화면을 넘긴다.
                view.SetActive(false);
                break;

            case TransitionStatus.Phase.Failed:
                // 가려진 채로 멈추지 않도록 사유를 띄운다.
                view.SetActive(true);
                SetText(string.IsNullOrEmpty(message) ? DefaultFailedText : message);
                break;

            default:
                view.SetActive(true);
                SetText(string.IsNullOrEmpty(message) ? DefaultLoadingText : message);
                break;
        }
    }

    private void SetText(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    private static GameObject LoadReplacement()
    {
        GameObject prefab = Resources.Load<GameObject>(ReplacementPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        Debug.Log($"[TransitionOverlay] 교체 프리팹을 사용합니다: Resources/{ReplacementPrefabPath}");
        return Instantiate(prefab);
    }

    /// <summary>
    /// 임시 외형. 전체를 덮는 검은 패널과 가운데 글자 하나가 전부다.
    /// 디자인·애니메이션은 교체 프리팹 쪽에서 하면 된다.
    /// </summary>
    private static GameObject BuildFallbackView()
    {
        GameObject root = new GameObject("View", typeof(Canvas), typeof(CanvasScaler));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 다른 UI 위에 확실히 덮이도록 아주 높게 둔다.
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(root.transform, worldPositionStays: false);

        Image image = panel.GetComponent<Image>();
        image.color = Color.black;
        // 클릭을 가로채지 않는다. 이 오버레이는 보여 주기만 한다.
        image.raycastTarget = false;

        Stretch(panel.GetComponent<RectTransform>());

        GameObject text = new GameObject("Label", typeof(TextMeshProUGUI));
        text.transform.SetParent(root.transform, worldPositionStays: false);

        TextMeshProUGUI tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.text = DefaultLoadingText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 48f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        Stretch(text.GetComponent<RectTransform>());

        return root;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

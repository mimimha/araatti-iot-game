using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로그인 / 회원가입 화면.
///
/// ⚠ 지금은 서버도 DB도 없습니다.
///    입력은 가능하지만 검증하지 않고, 비워 두어도 다음 화면으로 넘어갑니다.
///    나중에 인증을 붙일 때 Submit() 안쪽만 바꾸면 됩니다.
///
/// 담당하는 것
///   - 로그인 / 회원가입 탭 전환 (겉모습만)
///   - 이메일 · 비밀번호 입력
///   - 다음 화면으로 이동
///   - 뒤로가기
/// </summary>
public class LoginScreenController : MonoBehaviour
{
    [Header("씬 이동")]
    [Tooltip("로그인 / 회원가입 후 이동할 씬")]
    [SerializeField] private string nextSceneName = "CharacterCreate";

    [Tooltip("뒤로가기로 이동할 씬")]
    [SerializeField] private string backSceneName = "Title";

    [Header("탭")]
    [SerializeField] private Button loginTabButton;
    [SerializeField] private Button registerTabButton;
    [SerializeField] private Image loginTabImage;
    [SerializeField] private Image registerTabImage;
    [SerializeField] private TextMeshProUGUI loginTabLabel;
    [SerializeField] private TextMeshProUGUI registerTabLabel;

    [Header("탭 상태 이미지")]
    [Tooltip("선택된 탭에 쓸 이미지. 비워 두면 밝기로만 구분한다.")]
    [SerializeField] private Sprite tabActiveSprite;

    [Tooltip("선택되지 않은 탭에 쓸 이미지")]
    [SerializeField] private Sprite tabInactiveSprite;

    [Tooltip("상태 이미지가 없을 때, 선택되지 않은 탭의 밝기")]
    [SerializeField, Range(0.3f, 1f)] private float inactiveTabBrightness = 0.62f;

    [Header("탭 글자 색")]
    [SerializeField] private Color activeTabTextColor = Color.white;
    [SerializeField] private Color inactiveTabTextColor = new Color(0.75f, 0.80f, 0.88f);

    [Header("입력")]
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;

    [Header("버튼")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI submitLabel;
    [SerializeField] private Button backButton;

    [Header("버튼 문구")]
    [SerializeField] private string loginLabelText = "로그인";
    [SerializeField] private string registerLabelText = "회원가입";

    private bool _isLoginMode = true;

    /// <summary>지금 로그인 탭인지. false 면 회원가입 탭.</summary>
    public bool IsLoginMode => _isLoginMode;

    private void Awake()
    {
        WarnIfMissing(emailField, nameof(emailField));
        WarnIfMissing(passwordField, nameof(passwordField));
        WarnIfMissing(submitButton, nameof(submitButton));
    }

    private void OnEnable()
    {
        if (loginTabButton != null) loginTabButton.onClick.AddListener(SelectLoginTab);
        if (registerTabButton != null) registerTabButton.onClick.AddListener(SelectRegisterTab);
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (backButton != null) backButton.onClick.AddListener(GoBack);
    }

    private void OnDisable()
    {
        if (loginTabButton != null) loginTabButton.onClick.RemoveListener(SelectLoginTab);
        if (registerTabButton != null) registerTabButton.onClick.RemoveListener(SelectRegisterTab);
        if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
        if (backButton != null) backButton.onClick.RemoveListener(GoBack);
    }

    private void Start()
    {
        SelectLoginTab();

        // 비밀번호는 점으로 가린다.
        if (passwordField != null)
        {
            passwordField.contentType = TMP_InputField.ContentType.Password;
            passwordField.ForceLabelUpdate();
        }
    }

    // ------------------------------------------------------------
    // 탭
    // ------------------------------------------------------------

    public void SelectLoginTab()
    {
        _isLoginMode = true;
        ApplyTabVisual();
    }

    public void SelectRegisterTab()
    {
        _isLoginMode = false;
        ApplyTabVisual();
    }

    private void ApplyTabVisual()
    {
        // 선택된 탭과 아닌 탭의 모습을 서로 바꾼다.
        ApplyTab(loginTabImage, loginTabLabel, _isLoginMode);
        ApplyTab(registerTabImage, registerTabLabel, !_isLoginMode);

        if (submitLabel != null)
        {
            submitLabel.text = _isLoginMode ? loginLabelText : registerLabelText;
        }

    }

    /// <summary>탭 하나의 모습을 선택 상태에 맞게 바꾼다.</summary>
    private void ApplyTab(Image image, TextMeshProUGUI label, bool active)
    {
        if (image != null)
        {
            if (tabActiveSprite != null && tabInactiveSprite != null)
            {
                // 상태 이미지가 준비돼 있으면 서로 교체한다.
                image.sprite = active ? tabActiveSprite : tabInactiveSprite;
                image.color = Color.white;
            }
            else
            {
                // 없으면 밝기로만 구분한다.
                float v = active ? 1f : inactiveTabBrightness;
                image.color = new Color(v, v, v, 1f);
            }
        }

        if (label != null)
        {
            label.color = active ? activeTabTextColor : inactiveTabTextColor;
        }
    }

    // ------------------------------------------------------------
    // 이동
    // ------------------------------------------------------------

    /// <summary>
    /// 로그인 / 회원가입 실행.
    ///
    /// 지금은 검증 없이 그냥 다음 화면으로 넘어간다.
    /// 나중에 인증을 붙이면 이 안에서 서버에 요청하고,
    /// 성공했을 때만 씬을 넘기도록 바꾸면 된다.
    /// </summary>
    public void Submit()
    {
        string email = emailField != null ? emailField.text : string.Empty;
        string mode = _isLoginMode ? "로그인" : "회원가입";

        Debug.Log($"[LoginScreen] {mode} — 입력한 이메일: \"{email}\" (아직 검증하지 않습니다)");

        LoadScene(nextSceneName, "다음 화면");
    }

    public void GoBack()
    {
        LoadScene(backSceneName, "뒤로가기");
    }

    private void LoadScene(string sceneName, string what)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[LoginScreen] {what} 씬 이름이 비어 있습니다. Inspector 를 확인해 주세요.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[LoginScreen] \"{sceneName}\" 씬을 찾을 수 없습니다. " +
                "File > Build Profiles 의 Scene List 에 등록되어 있는지 확인해 주세요.", this);
            return;
        }

        Debug.Log($"[LoginScreen] 씬 이동: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    private void WarnIfMissing(Object reference, string fieldName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"[LoginScreen] {fieldName} 참조가 비어 있습니다. Inspector 에서 연결해 주세요.", this);
        }
    }
}

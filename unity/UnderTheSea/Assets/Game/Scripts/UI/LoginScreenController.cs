using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnderTheSea.Account;

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

    [Header("안내 문구")]
    [Tooltip("실패 사유와 진행 상태를 보여준다. 비워 두면 Console 에만 남는다. (씬 수정 없이도 동작한다)")]
    [SerializeField] private TextMeshProUGUI messageLabel;

    [SerializeField] private string processingText = "확인하는 중...";

    private bool _isLoginMode = true;

    /// <summary>요청을 보내고 결과를 기다리는 중인지. 버튼 중복 클릭을 막는다.</summary>
    private bool _isBusy;

    // 회원가입 성공 뒤 곧바로 로그인하기 위해 잠시 들고 있는다.
    private string _pendingEmail = string.Empty;
    private string _pendingPassword = string.Empty;

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

        if (AccountServiceLocator.IsReady)
        {
            AccountServiceLocator.Auth.OnSignUpResult += HandleSignUpResult;
            AccountServiceLocator.Auth.OnLogInResult += HandleLogInResult;
            AccountServiceLocator.Characters.OnMyCharactersResult += HandleMyCharactersResult;
        }
    }

    private void OnDisable()
    {
        if (loginTabButton != null) loginTabButton.onClick.RemoveListener(SelectLoginTab);
        if (registerTabButton != null) registerTabButton.onClick.RemoveListener(SelectRegisterTab);
        if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
        if (backButton != null) backButton.onClick.RemoveListener(GoBack);

        if (AccountServiceLocator.IsReady)
        {
            AccountServiceLocator.Auth.OnSignUpResult -= HandleSignUpResult;
            AccountServiceLocator.Auth.OnLogInResult -= HandleLogInResult;
            AccountServiceLocator.Characters.OnMyCharactersResult -= HandleMyCharactersResult;
        }
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
    /// <summary>
    /// 로그인 / 회원가입 실행.
    ///
    /// 여기서 씬을 넘기지 않는다. 서비스가 성공을 알려주고, 이어서 캐릭터 개수를 받아온 뒤에야
    /// 넘긴다. 검증도 서비스가 한다. 서버와 규칙이 갈라지지 않게 하기 위함이다.
    /// </summary>
    public void Submit()
    {
        if (_isBusy)
        {
            return;
        }

        string email = emailField != null ? emailField.text : string.Empty;
        string password = passwordField != null ? passwordField.text : string.Empty;

        if (!AccountServiceLocator.IsReady)
        {
            // 서비스가 없다. 이 씬만 단독 실행한 경우다. 예전 방식으로 통과시킨다.
            Debug.LogWarning(
                "[LoginScreen] 계정 서비스가 없어 이 PC 의 저장값만 보고 넘어갑니다. " +
                "정상 흐름을 보려면 Boot 씬부터 실행해 주세요.", this);
            SceneFlow.FromLogin();
            return;
        }

        _pendingEmail = email;
        _pendingPassword = password;

        SetBusy(true);
        SetMessage(processingText);

        if (_isLoginMode)
        {
            AccountServiceLocator.Auth.LogIn(email, password);
        }
        else
        {
            AccountServiceLocator.Auth.SignUp(email, password);
        }
    }

    /// <summary>회원가입 결과. 성공하면 토큰을 받으러 곧바로 로그인한다.</summary>
    private void HandleSignUpResult(bool success, string reason)
    {
        if (!success)
        {
            Fail(reason);
            return;
        }

        // 가입 응답에는 토큰이 없다. (서버 규격) 그래서 로그인을 한 번 더 부른다.
        AccountServiceLocator.Auth.LogIn(_pendingEmail, _pendingPassword);
    }

    /// <summary>로그인 결과. 성공하면 캐릭터 개수를 받아온다.</summary>
    private void HandleLogInResult(bool success, string reason)
    {
        if (!success)
        {
            Fail(reason);
            return;
        }

        _pendingPassword = string.Empty;
        AccountServiceLocator.Characters.RequestMyCharacters();
    }

    /// <summary>캐릭터 개수를 받았다. 이제서야 다음 화면이 정해진다.</summary>
    private void HandleMyCharactersResult(bool success, CharacterDto[] characters, string reason)
    {
        if (!success)
        {
            Fail(reason);
            return;
        }

        SetBusy(false);
        SetMessage(string.Empty);

        SceneFlow.FromLogin(characters != null ? characters.Length : 0);
    }

    private void Fail(string reason)
    {
        _pendingPassword = string.Empty;
        SetBusy(false);
        SetMessage(string.IsNullOrWhiteSpace(reason) ? "요청을 처리하지 못했습니다." : reason);
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (submitButton != null) submitButton.interactable = !busy;
    }

    /// <summary>안내 라벨이 연결되어 있지 않아도 동작한다. 그때는 Console 에만 남는다.</summary>
    private void SetMessage(string text)
    {
        if (messageLabel == null)
        {
            if (!string.IsNullOrEmpty(text)) Debug.Log($"[LoginScreen] {text}", this);
            return;
        }

        messageLabel.text = text;
        messageLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    public void GoBack()
    {
        SceneFlow.BackToTitle();
    }

    private void WarnIfMissing(Object reference, string fieldName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"[LoginScreen] {fieldName} 참조가 비어 있습니다. Inspector 에서 연결해 주세요.", this);
        }
    }
}

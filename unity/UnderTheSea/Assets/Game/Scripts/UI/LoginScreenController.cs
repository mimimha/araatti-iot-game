using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnderTheSea.Account;

/// <summary>
/// 로그인 / 회원가입 화면.
///
/// 담당하는 것
///   - 로그인 / 회원가입 탭 전환 (겉모습만)
///   - 이메일 · 비밀번호 입력
///   - 보내기 전 입력 검사와 칸별 경고 표시 (아래 "칸별 경고" 참고)
///   - 다음 화면으로 이동
///   - 뒤로가기
///
/// **칸별 경고**
///   입력이 규칙에 맞지 않으면 그 입력창 **바로 아래**에 빨간 글씨로 이유를 띄운다.
///   규칙은 <see cref="CredentialRules"/> 에 있고 서버와 같다. 여기서 걸리면 서버에 보내지 않는다.
///   서버가 거절한 경우에도 메시지에 "이메일" · "비밀번호" 가 들어 있으면 그 칸 아래로 보낸다.
///
///   경고 라벨은 씬에 없어도 된다. 비어 있으면 실행 시 입력창 아래에 코드로 만든다.
///   씬을 고치지 않는 이유: 씬은 병합 충돌이 가장 심한 파일이다. 디자이너가 자리를 직접 잡고 싶으면
///   Inspector 의 <c>emailErrorLabel</c> · <c>passwordErrorLabel</c> 에 연결하면 그것을 쓴다.
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

    [Header("칸별 경고")]
    [Tooltip("이메일 칸 아래 경고. 비워 두면 실행 시 EmailField 아래에 자동으로 만든다.")]
    [SerializeField] private TextMeshProUGUI emailErrorLabel;

    [Tooltip("비밀번호 칸 아래 경고. 비워 두면 실행 시 PasswordField 아래에 자동으로 만든다.")]
    [SerializeField] private TextMeshProUGUI passwordErrorLabel;

    [Tooltip("자동으로 만드는 경고 글자 색")]
    [SerializeField] private Color errorTextColor = new Color(1f, 0.36f, 0.36f);

    [Tooltip("자동으로 만드는 경고 글자 크기")]
    [SerializeField] private float errorFontSize = 20f;

    [Tooltip("입력창 아래 가장자리에서 경고까지의 간격 (px)")]
    [SerializeField] private float errorLabelGap = 4f;

    [Tooltip("경고 글자의 좌우 여백 (px). 입력창 안 글자 시작 위치와 맞추는 용도")]
    [SerializeField] private float errorLabelSidePadding = 12f;

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

        EnsureErrorLabels();
    }

    private void OnEnable()
    {
        if (loginTabButton != null) loginTabButton.onClick.AddListener(SelectLoginTab);
        if (registerTabButton != null) registerTabButton.onClick.AddListener(SelectRegisterTab);
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (backButton != null) backButton.onClick.AddListener(GoBack);

        // 고치기 시작하면 그 칸의 경고는 바로 지운다.
        if (emailField != null) emailField.onValueChanged.AddListener(ClearEmailError);
        if (passwordField != null) passwordField.onValueChanged.AddListener(ClearPasswordError);

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

        if (emailField != null) emailField.onValueChanged.RemoveListener(ClearEmailError);
        if (passwordField != null) passwordField.onValueChanged.RemoveListener(ClearPasswordError);

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

        // 탭을 바꾸면 이전 탭에서 난 경고는 의미가 없다.
        ClearFieldErrors();
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
    /// 여기서 씬을 넘기지 않는다. 서비스가 성공을 알려주고, 이어서 캐릭터 개수를 받아온 뒤에야
    /// 넘긴다.
    ///
    /// 보내기 전에 <see cref="CredentialRules"/> 로 한 번 걸러 칸별 경고를 띄운다.
    /// 이 규칙은 서버 것을 그대로 옮긴 것이라 서버와 갈라지지 않는다. 최종 판정은 여전히 서버가 하며,
    /// 서버가 거절하면 그 메시지도 같은 방식으로 칸 아래에 띄운다.
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

        if (!ValidateBeforeSend(email, password))
        {
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
        ClearFieldErrors();

        SceneFlow.FromLogin(characters != null ? characters.Length : 0);
    }

    /// <summary>
    /// 서비스(서버)가 거절했다. 메시지를 보고 어느 칸의 문제인지 짚어 그 칸 아래에 띄운다.
    /// 어느 칸인지 알 수 없는 메시지(연결 실패 등)는 공통 안내 라벨로, 그것도 없으면 비밀번호 칸 아래로 간다.
    /// </summary>
    private void Fail(string reason)
    {
        _pendingPassword = string.Empty;
        SetBusy(false);
        SetMessage(string.Empty);

        string text = string.IsNullOrWhiteSpace(reason) ? "요청을 처리하지 못했습니다." : reason;

        bool mentionsEmail = text.Contains("이메일");
        bool mentionsPassword = text.Contains("비밀번호");

        if (mentionsEmail && !mentionsPassword)
        {
            // "이메일 형식이 올바르지 않습니다." · "이미 가입된 이메일입니다."
            ShowError(emailErrorLabel, text);
        }
        else if (mentionsPassword && !mentionsEmail)
        {
            // "비밀번호는 8자 이상이어야 합니다."
            ShowError(passwordErrorLabel, text);
        }
        else if (messageLabel != null)
        {
            // "이메일 또는 비밀번호가 올바르지 않습니다." · 연결 실패 등 — 어느 칸이라고 짚을 수 없다.
            SetMessage(text);
        }
        else
        {
            // 공통 라벨이 씬에 없으면 마지막 칸 아래에 띄운다. 최소한 화면에는 보여야 한다.
            ShowError(passwordErrorLabel, text);
        }
    }

    // ------------------------------------------------------------
    // 칸별 경고
    // ------------------------------------------------------------

    /// <summary>
    /// 보내기 전 검사. 걸리는 칸마다 경고를 띄우고, 하나라도 걸리면 false.
    ///
    /// 회원가입은 서버 규칙(형식 · 길이)을 전부 본다.
    /// 로그인은 빈칸만 본다. 형식이 틀린 이메일은 어차피 서버가 "이메일 또는 비밀번호가 올바르지 않습니다" 로
    /// 답하므로, 여기서 형식까지 따지면 어떤 이메일이 가입돼 있는지 새어 나갈 여지를 만든다.
    /// </summary>
    private bool ValidateBeforeSend(string email, string password)
    {
        ClearFieldErrors();

        string emailProblem = _isLoginMode
            ? (string.IsNullOrWhiteSpace(email) ? "이메일을 입력해 주세요." : null)
            : CredentialRules.ValidateEmail(email);

        string passwordProblem = _isLoginMode
            ? (string.IsNullOrWhiteSpace(password) ? "비밀번호를 입력해 주세요." : null)
            : CredentialRules.ValidatePassword(password);

        if (emailProblem != null) ShowError(emailErrorLabel, emailProblem);
        if (passwordProblem != null) ShowError(passwordErrorLabel, passwordProblem);

        return emailProblem == null && passwordProblem == null;
    }

    private void ClearEmailError(string _) => ShowError(emailErrorLabel, string.Empty);

    private void ClearPasswordError(string _) => ShowError(passwordErrorLabel, string.Empty);

    private void ClearFieldErrors()
    {
        ShowError(emailErrorLabel, string.Empty);
        ShowError(passwordErrorLabel, string.Empty);
    }

    /// <summary>빈 문자열이면 숨긴다. 라벨이 없으면(만들지 못했으면) Console 에만 남긴다.</summary>
    private void ShowError(TextMeshProUGUI label, string text)
    {
        if (label == null)
        {
            if (!string.IsNullOrEmpty(text)) Debug.Log($"[LoginScreen] {text}", this);
            return;
        }

        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>Inspector 에 연결된 라벨이 없으면 입력창 아래에 코드로 만든다. 씬을 고치지 않기 위함이다.</summary>
    private void EnsureErrorLabels()
    {
        if (emailErrorLabel == null) emailErrorLabel = CreateErrorLabel(emailField, "EmailError");
        if (passwordErrorLabel == null) passwordErrorLabel = CreateErrorLabel(passwordField, "PasswordError");
    }

    /// <summary>
    /// 입력창의 자식으로 경고 라벨을 하나 만든다.
    ///
    /// 입력창 **아래 가장자리**에 붙여 아래로 늘어나게 잡는다. (anchor y=0, pivot y=1)
    /// 입력창 안 글자와 같은 폰트를 쓰고, 마스크가 걸린 Text Area 바깥에 두어 잘리지 않게 한다.
    /// </summary>
    private TextMeshProUGUI CreateErrorLabel(TMP_InputField field, string name)
    {
        if (field == null)
        {
            return null;
        }

        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(field.transform, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);

        float height = errorFontSize * 1.4f;
        rect.offsetMin = new Vector2(errorLabelSidePadding, -(errorLabelGap + height));
        rect.offsetMax = new Vector2(-errorLabelSidePadding, -errorLabelGap);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (field.textComponent != null)
        {
            label.font = field.textComponent.font;
        }
        label.fontSize = errorFontSize;
        label.color = errorTextColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.text = string.Empty;

        go.SetActive(false);
        return label;
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

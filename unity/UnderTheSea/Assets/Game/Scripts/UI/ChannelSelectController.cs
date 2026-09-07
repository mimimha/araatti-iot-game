using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 채널(서버) 선택 화면.
///
/// 채널 목록은 INetworkService 에서 받아온다.
/// Boot 씬을 거치지 않고 이 씬만 단독 실행하면 서비스가 없으므로,
/// 그때는 화면 확인용 예시 목록을 대신 보여준다.
///
/// 담당하는 것
///   - 채널 목록 받아오기 / 새로고침
///   - 줄 선택
///   - 입장 (접속 요청 후 다음 씬으로)
///   - 뒤로가기
/// </summary>
public class ChannelSelectController : MonoBehaviour
{
    [Header("씬 이동")]
    [Tooltip("입장에 성공하면 이동할 씬")]
    [SerializeField] private string nextSceneName = "Lobby";

    [Tooltip("뒤로가기로 이동할 씬")]
    [SerializeField] private string backSceneName = "CharacterCreate";

    [Header("채널 줄")]
    [Tooltip("위에서부터 순서대로 넣는다. 채널이 줄보다 많으면 앞에서부터만 보인다.")]
    [SerializeField] private ChannelRowView[] rows;

    [Header("버튼")]
    [SerializeField] private Button joinButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button refreshButton;

    [Header("안내 문구")]
    [Tooltip("불러오는 중 / 실패 등을 보여준다. 없어도 동작한다.")]
    [SerializeField] private TextMeshProUGUI messageLabel;

    [SerializeField] private string loadingText = "채널 목록을 불러오는 중...";
    [SerializeField] private string emptyText = "접속할 수 있는 채널이 없습니다.";
    [SerializeField] private string connectingText = "입장하는 중...";

    [Header("혼잡 판정")]
    [Tooltip("이 비율 이상 차면 '혼잡' 으로 표시한다.")]
    [SerializeField, Range(0.5f, 1f)] private float crowdedRatio = 0.8f;

    [Header("서버가 없을 때 (화면 확인용)")]
    [Tooltip("Boot 씬을 거치지 않고 이 씬만 실행했을 때 보여줄 예시 목록")]
    [SerializeField]
    private List<ServerInfo> sampleChannels = new List<ServerInfo>
    {
        new ServerInfo("ch-1", "채널 1", 12, 40),
        new ServerInfo("ch-2", "채널 2", 36, 40),
        new ServerInfo("ch-3", "채널 3", 8, 40),
    };

    private readonly List<ServerInfo> _channels = new List<ServerInfo>();
    private int _selectedIndex = -1;
    private bool _isConnecting;

    /// <summary>지금 고른 채널. 아무것도 안 골랐으면 null.</summary>
    public string SelectedServerId =>
        _selectedIndex >= 0 && _selectedIndex < _channels.Count ? _channels[_selectedIndex].Id : null;

    private void Awake()
    {
        WarnIfMissing(joinButton, nameof(joinButton));
        if (rows == null || rows.Length == 0)
        {
            Debug.LogWarning("[ChannelSelect] rows 가 비어 있습니다. Inspector 에서 연결해 주세요.", this);
        }
    }

    private void OnEnable()
    {
        if (joinButton != null) joinButton.onClick.AddListener(Join);
        if (backButton != null) backButton.onClick.AddListener(GoBack);
        if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);

        if (rows != null)
        {
            foreach (ChannelRowView row in rows)
            {
                if (row != null) row.Clicked += Select;
            }
        }

        if (NetworkServiceLocator.IsReady)
        {
            NetworkServiceLocator.Current.OnServerListUpdated += HandleServerList;
            NetworkServiceLocator.Current.OnConnectResult += HandleConnectResult;
        }
    }

    private void OnDisable()
    {
        if (joinButton != null) joinButton.onClick.RemoveListener(Join);
        if (backButton != null) backButton.onClick.RemoveListener(GoBack);
        if (refreshButton != null) refreshButton.onClick.RemoveListener(Refresh);

        if (rows != null)
        {
            foreach (ChannelRowView row in rows)
            {
                if (row != null) row.Clicked -= Select;
            }
        }

        if (NetworkServiceLocator.IsReady)
        {
            NetworkServiceLocator.Current.OnServerListUpdated -= HandleServerList;
            NetworkServiceLocator.Current.OnConnectResult -= HandleConnectResult;
        }
    }

    private void Start()
    {
        Refresh();
    }

    // ------------------------------------------------------------
    // 목록
    // ------------------------------------------------------------

    public void Refresh()
    {
        if (_isConnecting) return;

        if (NetworkServiceLocator.IsReady)
        {
            SetMessage(loadingText);
            NetworkServiceLocator.Current.RequestServerList();
            return;
        }

        // 서버가 없다. 화면을 확인할 수 있도록 예시 목록을 쓴다.
        Debug.LogWarning(
            "[ChannelSelect] 네트워크 서비스가 없어 예시 채널 목록을 표시합니다. " +
            "실제 목록을 보려면 Boot 씬부터 실행해 주세요.", this);
        HandleServerList(sampleChannels.ToArray());
    }

    private void HandleServerList(ServerInfo[] list)
    {
        // 새로고침 전 선택을 서버 ID로 기억한다. 목록 순서가 바뀌어도
        // 같은 채널이 남아 있으면 선택을 유지할 수 있다.
        string previouslySelectedServerId = SelectedServerId;

        _channels.Clear();
        if (list != null) _channels.AddRange(list);

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;

            if (i < _channels.Count) rows[i].Bind(i, _channels[i], crowdedRatio);
            else rows[i].Clear();
        }

        // 새로고침은 목록만 갱신한다. 첫 채널을 강제로 선택하지 않는다.
        // 이전 선택이 사라졌거나 만원이 됐다면 선택을 해제한다.
        int restoredIndex = string.IsNullOrEmpty(previouslySelectedServerId)
            ? -1
            : _channels.FindIndex(c => c.Id == previouslySelectedServerId && !c.IsFull);
        Select(restoredIndex);

        SetMessage(_channels.Count == 0 ? emptyText : string.Empty);
    }

    // ------------------------------------------------------------
    // 선택
    // ------------------------------------------------------------

    public void Select(int index)
    {
        _selectedIndex = index >= 0 && index < _channels.Count ? index : -1;

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null) rows[i].SetSelected(i == _selectedIndex);
        }

        if (joinButton != null) joinButton.interactable = _selectedIndex >= 0 && !_isConnecting;
    }

    // ------------------------------------------------------------
    // 이동
    // ------------------------------------------------------------

    public void Join()
    {
        if (_isConnecting) return;

        string serverId = SelectedServerId;
        if (string.IsNullOrEmpty(serverId))
        {
            Debug.Log("[ChannelSelect] 채널을 먼저 골라 주세요.");
            return;
        }

        if (!NetworkServiceLocator.IsReady)
        {
            // 서버가 없으면 접속 과정을 건너뛰고 그냥 다음 화면으로 넘어간다.
            Debug.Log($"[ChannelSelect] 서버 없이 진행합니다. 고른 채널: {serverId}");
            LoadScene(nextSceneName, "다음 화면");
            return;
        }

        _isConnecting = true;
        if (joinButton != null) joinButton.interactable = false;
        SetMessage(connectingText);

        NetworkServiceLocator.Current.Connect(PlayerNickname(), serverId);
    }

    private void HandleConnectResult(bool success, string reason)
    {
        _isConnecting = false;
        if (joinButton != null) joinButton.interactable = _selectedIndex >= 0;

        if (!success)
        {
            Debug.LogWarning($"[ChannelSelect] 입장 실패: {reason}", this);
            SetMessage(reason);
            return;
        }

        SetMessage(string.Empty);
        LoadScene(nextSceneName, "다음 화면");
    }

    public void GoBack()
    {
        LoadScene(backSceneName, "뒤로가기");
    }

    /// <summary>
    /// 접속에 쓸 닉네임.
    /// 캐릭터 생성 화면이 붙으면 거기서 정한 이름을 여기로 넘기면 된다.
    /// </summary>
    private static string PlayerNickname()
    {
        string saved = PlayerPrefs.GetString("PlayerNickname", string.Empty);
        return string.IsNullOrWhiteSpace(saved) ? "선원" : saved;
    }

    private void LoadScene(string sceneName, string what)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[ChannelSelect] {what} 씬 이름이 비어 있습니다. Inspector 를 확인해 주세요.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[ChannelSelect] \"{sceneName}\" 씬을 찾을 수 없습니다. " +
                "File > Build Profiles 의 Scene List 에 등록되어 있는지 확인해 주세요.", this);
            return;
        }

        Debug.Log($"[ChannelSelect] 씬 이동: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    private void SetMessage(string text)
    {
        if (messageLabel == null) return;
        messageLabel.text = text;
        messageLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void WarnIfMissing(Object reference, string fieldName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"[ChannelSelect] {fieldName} 참조가 비어 있습니다. Inspector 에서 연결해 주세요.", this);
        }
    }
}

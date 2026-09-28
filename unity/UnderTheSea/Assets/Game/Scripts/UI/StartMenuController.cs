using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 시작 화면의 메뉴 전체를 관리한다.
///
/// 담당하는 것
///   - 메뉴 목록과 현재 선택 위치 관리 (선택은 항상 정확히 하나)
///   - 키보드 / 게임패드 / 마우스 입력 처리
///   - EventSystem 선택 상태 동기화
///   - 버튼 기능 실행 (게임 시작 / 설정 / 종료)
///   - 설정 패널 열고 닫기와 포커스 복원
///   - 사운드 음소거 토글과 저장
///
/// 버튼의 겉모습은 MenuButtonView 가 담당한다.
/// </summary>
public class StartMenuController : MonoBehaviour
{
    public enum MenuAction
    {
        StartGame,
        OpenSettings,
        Quit
    }

    [Serializable]
    public class MenuEntry
    {
        [Tooltip("이 메뉴 항목의 버튼")]
        public MenuButtonView view;

        [Tooltip("눌렀을 때 실행할 기능")]
        public MenuAction action = MenuAction.StartGame;
    }

    [Header("메뉴 (위에서 아래 순서대로 등록)")]
    [SerializeField] private List<MenuEntry> menuEntries = new List<MenuEntry>();

    [Header("설정")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsIconButton;
    [SerializeField] private Button settingsCloseButton;

    [Header("사운드")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundIcon;
    [Tooltip("음소거일 때 아이콘 투명도")]
    [SerializeField, Range(0.1f, 1f)] private float mutedIconAlpha = 0.35f;

    [Header("효과음 (없어도 동작함)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip submitClip;

    [Header("입력 반복")]
    [Tooltip("키를 누르고 있을 때 첫 반복까지의 대기 시간(초)")]
    [SerializeField, Range(0.1f, 1f)] private float firstRepeatDelay = 0.4f;

    [Tooltip("그 뒤 반복 간격(초)")]
    [SerializeField, Range(0.05f, 0.5f)] private float repeatInterval = 0.15f;

    private const string MutePrefKey = "AraAtti.Audio.Muted";
    private const string MasterVolumePrefKey = "AraAtti.Audio.MasterVolume";
    private const string MusicVolumePrefKey = "AraAtti.Audio.MusicVolume";
    private const string EffectsVolumePrefKey = "AraAtti.Audio.EffectsVolume";

    public static event Action<float> MusicVolumeChanged;
    public static event Action<float> EffectsVolumeChanged;

    private int _index = -1;
    private bool _settingsOpen;
    private int _indexBeforeSettings;
    private int _lastMoveDirection;
    private float _nextRepeatTime;
    private bool _subscribed;
    private SettingsPanelView _settingsView;

    /// <summary>현재 선택된 메뉴 번호. 아무것도 선택되지 않았으면 -1.</summary>
    public int SelectedIndex => _index;

    public int MenuCount => menuEntries.Count;

    public bool IsSettingsOpen => _settingsOpen;

    public bool IsMuted => Mathf.Approximately(AudioListener.volume, 0f);

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        if (menuEntries.Count == 0)
        {
            Debug.LogWarning("[StartMenuController] 메뉴가 하나도 등록되지 않았습니다. Inspector 의 Menu Entries 를 채워 주세요.", this);
            return;
        }

        RestoreAudioState();

        if (settingsPanel != null)
        {
            _settingsView = settingsPanel.GetComponent<SettingsPanelView>();
            if (_settingsView == null)
            {
                _settingsView = settingsPanel.AddComponent<SettingsPanelView>();
            }

            _settingsView.Initialize(
                AudioListener.volume,
                MusicVolume,
                EffectsVolume,
                SetMasterVolume,
                SetMusicVolume,
                SetEffectsVolume,
                settingsCloseButton);
            settingsPanel.SetActive(false);
        }

        _settingsOpen = false;

        // 화면이 열리면 첫 번째 항목(게임 시작)을 선택한다.
        Select(0, playSound: false);
    }

    private void Update()
    {
        if (menuEntries.Count == 0)
        {
            return;
        }

        if (_settingsOpen)
        {
            HandleSettingsInput();
            return;
        }

        HandleNavigationInput();

        if (WasSubmitPressed())
        {
            Execute(_index);
        }
    }

    // ------------------------------------------------------------
    // 선택
    // ------------------------------------------------------------

    /// <summary>번호로 메뉴를 선택한다. 범위를 넘어가면 반대쪽으로 순환한다.</summary>
    public void Select(int index, bool playSound)
    {
        if (menuEntries.Count == 0)
        {
            return;
        }

        int count = menuEntries.Count;
        int wrapped = ((index % count) + count) % count;

        bool changed = _index != wrapped;
        _index = wrapped;

        // 선택 장식은 항상 하나만 켜져 있도록 매번 전부 갱신한다.
        for (int i = 0; i < count; i++)
        {
            if (menuEntries[i].view != null)
            {
                menuEntries[i].view.SetSelected(i == _index);
            }
        }

        SyncEventSystem();

        if (changed && playSound)
        {
            PlayClip(moveClip);
        }
    }

    public void MoveSelection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        Select(_index + direction, playSound: true);
    }

    private void SyncEventSystem()
    {
        if (EventSystem.current == null || _index < 0 || _index >= menuEntries.Count)
        {
            return;
        }

        MenuButtonView view = menuEntries[_index].view;
        if (view == null)
        {
            return;
        }

        GameObject target = view.gameObject;
        if (EventSystem.current.currentSelectedGameObject != target)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    // ------------------------------------------------------------
    // 입력
    // ------------------------------------------------------------

    private void HandleNavigationInput()
    {
        int direction = ReadVerticalDirection();

        if (direction == 0)
        {
            _lastMoveDirection = 0;
            return;
        }

        // 방향이 바뀌면 즉시 한 칸 이동하고 첫 반복까지 대기한다.
        if (direction != _lastMoveDirection)
        {
            _lastMoveDirection = direction;
            _nextRepeatTime = Time.unscaledTime + firstRepeatDelay;
            MoveSelection(direction);
            return;
        }

        if (Time.unscaledTime >= _nextRepeatTime)
        {
            _nextRepeatTime = Time.unscaledTime + repeatInterval;
            MoveSelection(direction);
        }
    }

    /// <summary>위쪽이면 -1, 아래쪽이면 +1. 누르지 않았으면 0.</summary>
    private int ReadVerticalDirection()
    {
        bool up = false;
        bool down = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            up |= keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed;
            down |= keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float stickY = gamepad.leftStick.ReadValue().y;
            up |= gamepad.dpad.up.isPressed || stickY > 0.5f;
            down |= gamepad.dpad.down.isPressed || stickY < -0.5f;
        }

        if (up == down)
        {
            return 0;
        }

        return up ? -1 : 1;
    }

    private bool WasSubmitPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null &&
            (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }

    private void HandleSettingsInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseSettings();
            return;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    // ------------------------------------------------------------
    // 실행
    // ------------------------------------------------------------

    private void Execute(int index)
    {
        if (index < 0 || index >= menuEntries.Count)
        {
            return;
        }

        MenuEntry entry = menuEntries[index];
        if (entry.view != null && !entry.view.IsInteractable)
        {
            return;
        }

        if (entry.view != null)
        {
            entry.view.PlayPressedEffect();
        }

        PlayClip(submitClip);

        switch (entry.action)
        {
            case MenuAction.StartGame:
                StartGame();
                break;
            case MenuAction.OpenSettings:
                OpenSettings();
                break;
            case MenuAction.Quit:
                QuitGame();
                break;
        }
    }

    public void StartGame()
    {
        // 어디로 갈지는 SceneFlow 가 정한다.
        SceneFlow.FromTitle();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            Debug.LogWarning("[StartMenuController] 설정 패널이 연결되지 않았습니다. Inspector 의 Settings Panel 을 확인해 주세요.", this);
            return;
        }

        _indexBeforeSettings = Mathf.Max(_index, 0);
        _settingsOpen = true;
        SetMenuButtonsVisible(false);
        settingsPanel.SetActive(true);

        if (_settingsView != null)
        {
            _settingsView.FocusDefault();
        }
        else if (settingsCloseButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(settingsCloseButton.gameObject);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        _settingsOpen = false;
        SetMenuButtonsVisible(true);

        // 설정을 열기 전에 보고 있던 항목으로 되돌린다.
        Select(_indexBeforeSettings, playSound: false);
    }

    private void SetMenuButtonsVisible(bool visible)
    {
        foreach (MenuEntry entry in menuEntries)
        {
            if (entry.view != null)
            {
                entry.view.gameObject.SetActive(visible);
            }
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[StartMenuController] 게임 종료 요청 (에디터에서는 실제로 종료하지 않습니다)");
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------
    // 사운드
    // ------------------------------------------------------------

    public void ToggleSound()
    {
        if (IsMuted)
        {
            float savedVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, 1f);
            SetMasterVolume(Mathf.Max(savedVolume, 0.5f));
            return;
        }

        SetMasterVolume(0f);
    }

    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;

        if (soundIcon != null)
        {
            Color color = soundIcon.color;
            color.a = volume <= 0.001f ? mutedIconAlpha : 1f;
            soundIcon.color = color;
        }

        if (volume > 0.001f)
        {
            PlayerPrefs.SetFloat(MasterVolumePrefKey, volume);
        }

        PlayerPrefs.SetInt(MutePrefKey, volume <= 0.001f ? 1 : 0);
        PlayerPrefs.Save();

        if (_settingsView != null)
        {
            _settingsView.SetMasterVolumeWithoutNotify(volume);
        }
    }

    public float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, 1f));

    public float EffectsVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumePrefKey, 1f));

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, volume);
        PlayerPrefs.Save();
        _settingsView?.SetMusicVolumeWithoutNotify(volume);
        MusicVolumeChanged?.Invoke(volume);
    }

    public void SetEffectsVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(EffectsVolumePrefKey, volume);
        PlayerPrefs.Save();

        if (audioSource != null)
        {
            audioSource.volume = volume;
        }

        _settingsView?.SetEffectsVolumeWithoutNotify(volume);
        EffectsVolumeChanged?.Invoke(volume);
    }

    private void RestoreAudioState()
    {
        bool muted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
        float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, 1f));
        AudioListener.volume = muted ? 0f : savedVolume;

        if (audioSource != null)
        {
            audioSource.volume = EffectsVolume;
        }

        if (soundIcon != null)
        {
            Color color = soundIcon.color;
            color.a = muted ? mutedIconAlpha : 1f;
            soundIcon.color = color;
        }
    }

    private void PlayClip(AudioClip clip)
    {
        // 효과음이 지정되지 않아도 오류 없이 넘어간다.
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    // ------------------------------------------------------------
    // 이벤트 연결
    // ------------------------------------------------------------

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        foreach (MenuEntry entry in menuEntries)
        {
            if (entry.view == null)
            {
                continue;
            }

            entry.view.PointerEntered += OnPointerEnteredButton;
            entry.view.Clicked += OnButtonClicked;
        }

        if (settingsIconButton != null)
        {
            settingsIconButton.onClick.AddListener(OpenSettings);
        }

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.AddListener(CloseSettings);
        }

        if (soundButton != null)
        {
            soundButton.onClick.AddListener(ToggleSound);
        }

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        foreach (MenuEntry entry in menuEntries)
        {
            if (entry.view == null)
            {
                continue;
            }

            entry.view.PointerEntered -= OnPointerEnteredButton;
            entry.view.Clicked -= OnButtonClicked;
        }

        if (settingsIconButton != null)
        {
            settingsIconButton.onClick.RemoveListener(OpenSettings);
        }

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.RemoveListener(CloseSettings);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveListener(ToggleSound);
        }

        _subscribed = false;
    }

    private void OnPointerEnteredButton(MenuButtonView view)
    {
        if (_settingsOpen)
        {
            return;
        }

        int index = IndexOf(view);
        if (index >= 0)
        {
            // 마우스를 올리면 그 항목이 현재 선택이 된다. 밖으로 나가도 선택은 유지된다.
            Select(index, playSound: true);
        }
    }

    private void OnButtonClicked(MenuButtonView view)
    {
        if (_settingsOpen)
        {
            return;
        }

        int index = IndexOf(view);
        if (index < 0)
        {
            return;
        }

        Select(index, playSound: false);
        Execute(index);
    }

    private int IndexOf(MenuButtonView view)
    {
        for (int i = 0; i < menuEntries.Count; i++)
        {
            if (menuEntries[i].view == view)
            {
                return i;
            }
        }

        return -1;
    }

#if UNITY_EDITOR
    /// <summary>Inspector 우클릭 메뉴에서 설정이 올바른지 간단히 확인한다.</summary>
    [ContextMenu("메뉴 구성 검사")]
    private void ValidateSetup()
    {
        int problems = 0;

        if (menuEntries.Count == 0)
        {
            Debug.LogWarning("[검사] 등록된 메뉴가 없습니다.", this);
            problems++;
        }

        for (int i = 0; i < menuEntries.Count; i++)
        {
            if (menuEntries[i].view == null)
            {
                Debug.LogWarning($"[검사] {i}번 메뉴의 view 가 비어 있습니다.", this);
                problems++;
            }
        }

        // 이동할 씬은 SceneFlow 가 정한다. 여기서는 그 씬이 등록되어 있는지만 확인한다.
        if (!Application.CanStreamedLevelBeLoaded(SceneFlow.Login))
        {
            Debug.LogWarning(
                $"[검사] \"{SceneFlow.Login}\" 씬이 Build Profiles 의 Scene List 에 없습니다. " +
                "[시작] 을 눌러도 넘어가지 않습니다.", this);
            problems++;
        }

        if (settingsPanel == null)
        {
            Debug.LogWarning("[검사] 설정 패널이 연결되지 않았습니다.", this);
            problems++;
        }

        Debug.Log(problems == 0
            ? "[검사] 메뉴 구성에 문제가 없습니다."
            : $"[검사] 문제 {problems}건을 확인해 주세요.", this);
    }

    /// <summary>
    /// 선택 번호가 제대로 순환하는지, 선택 장식이 항상 하나만 켜지는지 확인한다.
    /// 플레이 모드에서 Inspector 우클릭으로 실행한다.
    /// </summary>
    [ContextMenu("선택 순환 검사 (플레이 모드)")]
    private void ValidateSelectionCycle()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[검사] 플레이 모드에서 실행해 주세요.", this);
            return;
        }

        int count = menuEntries.Count;
        if (count == 0)
        {
            Debug.LogWarning("[검사] 메뉴가 없습니다.", this);
            return;
        }

        int startIndex = Mathf.Max(_index, 0);
        int problems = 0;

        // 한 바퀴 돌고 한 칸 더 이동해서 처음으로 돌아오는지 본다.
        for (int step = 0; step <= count; step++)
        {
            int target = startIndex + step;
            int expected = ((target % count) + count) % count;

            Select(target, playSound: false);

            if (_index != expected)
            {
                Debug.LogError($"[검사] {step}번째 이동 실패: 기대 {expected}, 실제 {_index}", this);
                problems++;
            }

            int selectedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (menuEntries[i].view != null && menuEntries[i].view.IsSelected)
                {
                    selectedCount++;
                }
            }

            if (selectedCount != 1)
            {
                Debug.LogError($"[검사] {step}번째 이동에서 선택 장식이 {selectedCount}개 켜져 있습니다. 1개여야 합니다.", this);
                problems++;
            }
        }

        // 위쪽으로도 순환하는지 확인
        Select(0, playSound: false);
        MoveSelection(-1);
        if (_index != count - 1)
        {
            Debug.LogError($"[검사] 첫 항목에서 위로 이동 시 마지막({count - 1})로 가야 하는데 {_index} 입니다.", this);
            problems++;
        }

        Select(startIndex, playSound: false);

        Debug.Log(problems == 0
            ? $"[검사] 선택 순환 정상. 항목 {count}개, 선택 장식은 항상 1개."
            : $"[검사] 문제 {problems}건 발견.", this);
    }
#endif
}

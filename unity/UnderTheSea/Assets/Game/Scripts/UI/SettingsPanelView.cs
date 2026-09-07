using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 설정창을 구성한다.
/// 화면에 꼭 필요한 오디오와 IoT 연결 항목만 제공한다.
/// 실제 IoT 서비스는 DeviceRefreshRequested를 구독하고 ReportDeviceStatus를 호출하면 된다.
/// </summary>
public sealed class SettingsPanelView : MonoBehaviour
{
    public static event Action DeviceRefreshRequested;

    private static readonly Color PrimaryText = new Color(0.96f, 0.98f, 1f, 1f);
    private static readonly Color SecondaryText = new Color(0.68f, 0.80f, 0.90f, 1f);
    private static readonly Color Gold = new Color(0.93f, 0.67f, 0.20f, 1f);
    private static readonly Color Cyan = new Color(0.24f, 0.78f, 0.95f, 1f);
    private static readonly Color Track = new Color(0.025f, 0.12f, 0.23f, 0.95f);
    private static readonly Color ButtonOff = new Color(0.10f, 0.20f, 0.32f, 1f);
    private static readonly Color Connected = new Color(0.40f, 0.88f, 0.60f, 1f);

    private const string RuntimeRootName = "SettingsContent";

    private Slider _masterVolumeSlider;
    private Slider _musicVolumeSlider;
    private Slider _effectsVolumeSlider;
    private TextMeshProUGUI _masterVolumeValue;
    private TextMeshProUGUI _musicVolumeValue;
    private TextMeshProUGUI _effectsVolumeValue;
    private TextMeshProUGUI _deviceStatus;
    private Button _refreshButton;
    private TextMeshProUGUI _refreshButtonLabel;

    private Action<float> _onMasterVolumeChanged;
    private Action<float> _onMusicVolumeChanged;
    private Action<float> _onEffectsVolumeChanged;
    private Coroutine _refreshTimeout;
    private int _refreshVersion;

    public Selectable DefaultSelectable => _masterVolumeSlider;

    public void Initialize(
        float initialMasterVolume,
        float initialMusicVolume,
        float initialEffectsVolume,
        Action<float> onMasterVolumeChanged,
        Action<float> onMusicVolumeChanged,
        Action<float> onEffectsVolumeChanged,
        Button closeButton)
    {
        _onMasterVolumeChanged = onMasterVolumeChanged;
        _onMusicVolumeChanged = onMusicVolumeChanged;
        _onEffectsVolumeChanged = onEffectsVolumeChanged;

        Transform box = transform.Find("Box");
        if (box == null)
        {
            Debug.LogWarning("[SettingsPanelView] SettingsPanel 아래 Box를 찾을 수 없습니다.", this);
            return;
        }

        UseOriginalAssetSizes(box, closeButton);

        Transform previous = box.Find(RuntimeRootName);
        if (previous != null)
        {
            Destroy(previous.gameObject);
        }

        TextMeshProUGUI titleText = box.Find("Title")?.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = titleText != null ? titleText.font : null;
        RectTransform content = CreateRect(RuntimeRootName, box, new Vector2(450f, 300f), new Vector2(0f, 18f));

        CreateSectionTitle(content, "오디오", new Vector2(-175f, 112f), font);
        CreateVolumeRow(content, "전체 음량", "Master", 73f, font,
            out _masterVolumeSlider, out _masterVolumeValue, HandleMasterVolumeChanged);
        CreateVolumeRow(content, "배경 음악", "Music", 33f, font,
            out _musicVolumeSlider, out _musicVolumeValue, HandleMusicVolumeChanged);
        CreateVolumeRow(content, "효과음", "Effects", -7f, font,
            out _effectsVolumeSlider, out _effectsVolumeValue, HandleEffectsVolumeChanged);

        CreateDivider(content, -35f);
        CreateText("DeviceLabel", content, "IoT 기기", 19f, TextAlignmentOptions.Left,
            new Vector2(110f, 34f), new Vector2(-170f, -75f), font, Gold);
        _deviceStatus = CreateText("DeviceStatus", content, "●  연결된 기기 없음", 16f,
            TextAlignmentOptions.Left, new Vector2(220f, 34f), new Vector2(5f, -75f), font, SecondaryText);

        _refreshButton = CreateFlatButton("RefreshDeviceButton", content, new Vector2(100f, 36f),
            new Vector2(173f, -75f), font, out _refreshButtonLabel);
        _refreshButtonLabel.text = "연결 확인";
        _refreshButton.onClick.AddListener(RefreshDeviceSignal);

        SetMasterVolumeWithoutNotify(initialMasterVolume);
        SetMusicVolumeWithoutNotify(initialMusicVolume);
        SetEffectsVolumeWithoutNotify(initialEffectsVolume);
    }

    public void FocusDefault()
    {
        if (_masterVolumeSlider != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_masterVolumeSlider.gameObject);
        }
    }

    public void SetMasterVolumeWithoutNotify(float volume)
    {
        SetSliderWithoutNotify(_masterVolumeSlider, _masterVolumeValue, volume);
    }

    public void SetMusicVolumeWithoutNotify(float volume)
    {
        SetSliderWithoutNotify(_musicVolumeSlider, _musicVolumeValue, volume);
    }

    public void SetEffectsVolumeWithoutNotify(float volume)
    {
        SetSliderWithoutNotify(_effectsVolumeSlider, _effectsVolumeValue, volume);
    }

    public void RefreshDeviceSignal()
    {
        _refreshVersion++;
        int requestVersion = _refreshVersion;

        SetDeviceStatusText("●  연결 확인 중...", Cyan);
        SetRefreshButtonState(false, "확인 중...");

        if (_refreshTimeout != null)
        {
            StopCoroutine(_refreshTimeout);
        }

        DeviceRefreshRequested?.Invoke();
        _refreshTimeout = StartCoroutine(ShowNoSignalIfUnanswered(requestVersion));
    }

    /// <summary>IoT 서비스에서 확인한 기기 상태를 설정창에 반영한다.</summary>
    public void ReportDeviceStatus(bool connected, string deviceName = "", int signalPercent = -1)
    {
        _refreshVersion++;

        if (_refreshTimeout != null)
        {
            StopCoroutine(_refreshTimeout);
            _refreshTimeout = null;
        }

        SetRefreshButtonState(true, "다시 확인");

        if (!connected)
        {
            SetDeviceStatusText("●  연결된 기기 없음", SecondaryText);
            return;
        }

        string label = string.IsNullOrWhiteSpace(deviceName) ? "IoT 기기 연결됨" : deviceName.Trim();
        if (signalPercent >= 0)
        {
            label += $"  ·  {Mathf.Clamp(signalPercent, 0, 100)}%";
        }

        SetDeviceStatusText($"●  {label}", Connected);
    }

    private void HandleMasterVolumeChanged(float volume)
    {
        SetMasterVolumeWithoutNotify(volume);
        _onMasterVolumeChanged?.Invoke(volume);
    }

    private void HandleMusicVolumeChanged(float volume)
    {
        SetMusicVolumeWithoutNotify(volume);
        _onMusicVolumeChanged?.Invoke(volume);
    }

    private void HandleEffectsVolumeChanged(float volume)
    {
        SetEffectsVolumeWithoutNotify(volume);
        _onEffectsVolumeChanged?.Invoke(volume);
    }

    private IEnumerator ShowNoSignalIfUnanswered(int requestVersion)
    {
        yield return new WaitForSecondsRealtime(1.2f);

        if (requestVersion == _refreshVersion)
        {
            SetDeviceStatusText("●  연결 신호 없음", SecondaryText);
            SetRefreshButtonState(true, "다시 확인");
        }

        _refreshTimeout = null;
    }

    private void SetDeviceStatusText(string text, Color color)
    {
        if (_deviceStatus == null)
        {
            return;
        }

        _deviceStatus.text = text;
        _deviceStatus.color = color;
    }

    private void SetRefreshButtonState(bool interactable, string label)
    {
        if (_refreshButton != null)
        {
            _refreshButton.interactable = interactable;
        }

        if (_refreshButtonLabel != null)
        {
            _refreshButtonLabel.text = label;
        }
    }

    private static void UseOriginalAssetSizes(Transform box, Button closeButton)
    {
        if (box is RectTransform boxRect)
        {
            boxRect.anchoredPosition = Vector2.zero;
        }

        Image boxImage = box.GetComponent<Image>();
        if (boxImage != null)
        {
            boxImage.type = Image.Type.Simple;
            boxImage.preserveAspect = true;
            boxImage.SetNativeSize();
        }

        Transform title = box.Find("Title");
        if (title is RectTransform titleRect)
        {
            // 상단 금색 프레임과 겹치지 않도록 제목 전체를 컨테이너 안쪽에 둔다.
            titleRect.anchoredPosition = new Vector2(0f, -85f);
            titleRect.sizeDelta = new Vector2(430f, 58f);
        }

        TextMeshProUGUI titleText = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        if (titleText != null)
        {
            titleText.fontSize = 43f;
        }

        if (closeButton != null && closeButton.transform is RectTransform closeRect)
        {
            // 원본 438 x 94 버튼을 축소하지 않고 하단 프레임 안에 완전히 수용한다.
            closeRect.anchoredPosition = new Vector2(0f, 70f);

            if (closeButton.targetGraphic is Image closeImage)
            {
                closeImage.type = Image.Type.Simple;
                closeImage.preserveAspect = true;
                closeImage.SetNativeSize();
            }

            TextMeshProUGUI closeLabel = closeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (closeLabel != null)
            {
                closeLabel.fontSize = 34f;
            }
        }
    }

    private static void CreateVolumeRow(Transform parent, string label, string name, float y, TMP_FontAsset font,
        out Slider slider, out TextMeshProUGUI valueText, UnityEngine.Events.UnityAction<float> handler)
    {
        CreateText(name + "Label", parent, label, 20f, TextAlignmentOptions.Left,
            new Vector2(120f, 34f), new Vector2(-165f, y), font, PrimaryText);

        slider = CreateSlider(name + "VolumeSlider", parent, new Vector2(200f, 32f), new Vector2(30f, y));
        slider.onValueChanged.AddListener(handler);

        valueText = CreateText(name + "Value", parent, "100%", 17f, TextAlignmentOptions.Right,
            new Vector2(60f, 34f), new Vector2(190f, y), font, SecondaryText);
    }

    private static void CreateSectionTitle(Transform parent, string value, Vector2 position, TMP_FontAsset font)
    {
        CreateText(value + "Section", parent, value, 18f, TextAlignmentOptions.Left,
            new Vector2(100f, 28f), position, font, Gold);
    }

    private static void CreateDivider(Transform parent, float y)
    {
        CreateImage("Divider", parent, new Vector2(440f, 2f), new Vector2(0f, y),
            new Color(Gold.r, Gold.g, Gold.b, 0.42f));
    }

    private static void SetSliderWithoutNotify(Slider slider, TextMeshProUGUI valueText, float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (slider != null)
        {
            slider.SetValueWithoutNotify(volume);
        }

        if (valueText != null)
        {
            valueText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform sliderRect = CreateRect(name, parent, size, position);
        Image hitArea = sliderRect.gameObject.AddComponent<Image>();
        // 완전 투명(0)은 CanvasRenderer에서 컬링될 수 있으므로 입력만 받을 정도로 유지한다.
        hitArea.color = new Color(1f, 1f, 1f, 0.001f);
        hitArea.raycastTarget = true;

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform trackRect = CreateImage("Track", sliderRect, new Vector2(size.x - 20f, 8f),
            Vector2.zero, Track).rectTransform;

        RectTransform fillArea = CreateRect("Fill Area", sliderRect, new Vector2(size.x - 20f, 8f), Vector2.zero);
        RectTransform fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.zero, Cyan).rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        RectTransform handleArea = CreateRect(
            "Handle Slide Area", sliderRect, new Vector2(size.x - 20f, 16.8f), Vector2.zero);
        // 기존 16 x 24 손잡이의 70% 크기
        Image handle = CreateImage("Handle", handleArea, new Vector2(11.2f, 0f), Vector2.zero, Gold);
        handle.raycastTarget = true;

        slider.fillRect = fill;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        trackRect.SetAsFirstSibling();
        return slider;
    }

    private static Button CreateFlatButton(string name, Transform parent, Vector2 size, Vector2 position,
        TMP_FontAsset font, out TextMeshProUGUI label)
    {
        RectTransform rect = CreateRect(name, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonOff;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.55f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        label = CreateText("Label", rect, "꺼짐", 16f, TextAlignmentOptions.Center,
            size - new Vector2(12f, 8f), Vector2.zero, font, PrimaryText);
        label.raycastTarget = false;
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize,
        TextAlignmentOptions alignment, Vector2 size, Vector2 position, TMP_FontAsset font, Color color)
    {
        RectTransform rect = CreateRect(name, parent, size, position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        RectTransform rect = CreateRect(name, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }
}

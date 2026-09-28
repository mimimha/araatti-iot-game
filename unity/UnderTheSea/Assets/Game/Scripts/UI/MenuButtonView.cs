using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 중앙 메뉴 버튼 하나의 겉모습을 담당한다.
///
/// 담당하는 것
///   - Base / SelectedDecoration / Label 참조 관리
///   - 선택 장식 켜고 끄기
///   - 눌렀을 때 잠깐 작아지는 효과
///   - 마우스가 올라오거나 클릭했을 때 Controller 에게 알리기
///
/// 선택을 누가 할지는 StartMenuController 가 정한다. 이 스크립트는 스스로 선택하지 않는다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuButtonView : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private Image baseImage;
    [SerializeField] private GameObject selectedDecoration;
    [SerializeField] private TextMeshProUGUI label;

    [Header("눌림 효과")]
    [Tooltip("눌렀을 때 줄어드는 비율")]
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.95f;
    [Tooltip("줄어들었다가 돌아오기까지 걸리는 시간(초)")]
    [SerializeField, Range(0.02f, 0.5f)] private float pressedDuration = 0.1f;

    /// <summary>마우스 포인터가 이 버튼 위로 들어왔다.</summary>
    public event Action<MenuButtonView> PointerEntered;

    /// <summary>이 버튼이 클릭되었다.</summary>
    public event Action<MenuButtonView> Clicked;

    private RectTransform _rect;
    private Vector3 _originalScale = Vector3.one;
    private Coroutine _pressRoutine;
    private bool _initialized;

    public Button Button => button;
    public TextMeshProUGUI Label => label;

    public bool IsInteractable => button == null || button.interactable;

    /// <summary>선택 장식이 켜져 있는지. 검증 도구가 사용한다.</summary>
    public bool IsSelected => selectedDecoration != null && selectedDecoration.activeSelf;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _rect = (RectTransform)transform;
        _originalScale = _rect.localScale;
        _initialized = true;

        WarnIfMissing(button, nameof(button));
        WarnIfMissing(baseImage, nameof(baseImage));
        WarnIfMissing(selectedDecoration, nameof(selectedDecoration));
        WarnIfMissing(label, nameof(label));

        if (button != null && baseImage != null && button.targetGraphic != baseImage)
        {
            button.targetGraphic = baseImage;
        }
    }

    private void OnDisable()
    {
        // 비활성화될 때 줄어든 채로 남지 않도록 되돌린다.
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        if (_rect != null)
        {
            _rect.localScale = _originalScale;
        }
    }

    /// <summary>선택 장식을 켜거나 끈다. 버튼의 위치와 크기는 바뀌지 않는다.</summary>
    public void SetSelected(bool selected)
    {
        if (selectedDecoration == null)
        {
            return;
        }

        if (selectedDecoration.activeSelf != selected)
        {
            selectedDecoration.SetActive(selected);
        }
    }

    /// <summary>눌린 느낌을 준다. Time.timeScale 이 0 이어도 동작한다.</summary>
    public void PlayPressedEffect()
    {
        Initialize();

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
        }

        _pressRoutine = StartCoroutine(PressRoutine());
    }

    private IEnumerator PressRoutine()
    {
        _rect.localScale = _originalScale * pressedScale;

        float elapsed = 0f;
        while (elapsed < pressedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _rect.localScale = _originalScale;
        _pressRoutine = null;
    }

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsInteractable)
        {
            PointerEntered?.Invoke(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsInteractable)
        {
            Clicked?.Invoke(this);
        }
    }

    private void WarnIfMissing(UnityEngine.Object reference, string fieldName)
    {
        if (reference == null)
        {
            Debug.LogWarning($"[MenuButtonView] '{name}' 의 {fieldName} 참조가 비어 있습니다. Inspector 에서 연결해 주세요.", this);
        }
    }

#if UNITY_EDITOR
    /// <summary>Inspector 우클릭 메뉴에서 참조가 제대로 연결됐는지 확인할 수 있다.</summary>
    [ContextMenu("참조 검사")]
    private void ValidateFromContextMenu()
    {
        _initialized = false;
        Initialize();
        Debug.Log($"[MenuButtonView] '{name}' 참조 검사 완료", this);
    }
#endif
}

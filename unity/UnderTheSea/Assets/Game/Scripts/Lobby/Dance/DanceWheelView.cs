using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderTheSea.Lobby.Dance
{
    /// <summary>
    /// **로비 춤 선택 휠.** PEAK 의 가방 휠 같은 모양으로 춤 5개 중 하나를 고른다.
    ///
    /// <code>
    ///   Q          열기 / 닫기
    ///   마우스      가리키는 칸이 밝아지고 커진다 — 방향만 보므로 칸 위에 정확히 없어도 된다
    ///   좌클릭      가리키는 칸의 춤을 고르고 닫는다
    ///   1 ~ 5      그 번호의 춤을 바로 고르고 닫는다
    ///   Esc        그냥 닫는다
    ///   완드       왼손 버튼 2 로 열고 · 오른손 스틱으로 가리키고 · 왼손 버튼 2 로 확정 (IotLobbyInteract)
    /// </code>
    ///
    /// 칸은 위에서 시작해 시계 방향으로 1, 2, 3, 4, 5 다. 칸 모양은 <see cref="RingSegmentGraphic"/> 이 그린다.
    ///
    /// <b>지금은 고르기만 한다.</b> 고르면 <see cref="DanceSelected"/> 를 올린다. 춤 동작과 다른 사람에게
    /// 보이게 하는 동기화는 이 이벤트를 듣는 쪽이 맡는다(다음 단계).
    ///
    /// ⚠ 채팅에 글을 쓰는 중에는 키를 받지 않는다(<see cref="ChatFocus.Typing"/>). 안 그러면 "q" 를 치는
    ///    순간 휠이 열린다.
    ///
    /// 프리팹: <c>Resources/DanceWheel.prefab</c> — <c>Tools/아라아띠/로비 춤 휠 프리팹 만들기</c> 로 만든다.
    /// 로비에만 뜨는 것은 <see cref="DanceWheelInstaller"/> 가 정한다.
    /// </summary>
    public sealed class DanceWheelView : MonoBehaviour
    {
        /// <summary>휠을 여닫는 키. 로비에서 Q 를 쓰는 곳이 없다(2026-09 확인). E 는 제단 봉헌이다.</summary>
        public const Key ToggleKey = Key.Q;

        [Header("연결 (프리팹 만들기가 채운다)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform wheel;
        [SerializeField] private RingSegmentGraphic[] slots = Array.Empty<RingSegmentGraphic>();
        [SerializeField] private TMP_Text[] slotLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text centerLabel;

        [Tooltip("칸마다 크기를 키울 뿌리. 비어 있으면 칸 그래픽을 키운다. 테두리 · 이름 · 번호가 같이 커진다.")]
        [SerializeField] private RectTransform[] slotRoots = Array.Empty<RectTransform>();

        [Tooltip("칸 테두리. 비어 있어도 된다. 가리키면 색이 바뀐다.")]
        [SerializeField] private RingSegmentGraphic[] slotBorders = Array.Empty<RingSegmentGraphic>();

        [Tooltip("열 때 서서히 나타나게 하는 투명도 조절. 비어 있어도 된다.")]
        [SerializeField] private CanvasGroup group;

        [Header("춤")]
        [Tooltip("칸 순서대로의 이름. 위에서부터 시계 방향.")]
        [SerializeField] private string[] danceNames = { "춤 1", "춤 2", "춤 3", "춤 4", "춤 5" };

        [Header("모양")]
        [SerializeField] private Color normalColor = new Color(0.96f, 0.93f, 0.86f, 0.78f);
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.97f);
        [SerializeField] private Color labelColor = new Color(0.32f, 0.24f, 0.16f, 1f);
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color hoverBorderColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("아무 칸도 가리키지 않을 때 가운데에 띄울 말.")]
        [SerializeField] private string idleCenterText = "춤 고르기";

        [Tooltip("열 때 커지며 나타나는 시간(초). 0 이면 바로 뜬다.")]
        [SerializeField, Range(0f, 0.5f)] private float openSeconds = 0.12f;

        [Tooltip("열리기 시작할 때의 크기. 1 까지 커진다.")]
        [SerializeField, Range(0.5f, 1f)] private float openFromScale = 0.85f;

        [Tooltip("가리킨 칸을 이만큼 키운다. 휠 중심 기준이라 바깥으로 튀어나온다.")]
        [SerializeField, Range(1f, 1.3f)] private float hoverScale = 1.07f;

        [Tooltip("휠 중심에서 이 반지름 안쪽이면 아무 칸도 가리키지 않는다(1920×1080 기준 px).")]
        [SerializeField, Min(0f)] private float deadZone = 40f;

        /// <summary>
        /// 춤을 골랐다. 값은 칸 번호(0 부터). 춤 동작 · 동기화가 이것을 듣는다.
        ///
        /// ⚠ 휠이 닫힌 <b>뒤에</b> 올린다. 듣는 쪽이 곧바로 다른 화면을 열어도 휠과 겹치지 않는다.
        /// </summary>
        public static event Action<int> DanceSelected;

        /// <summary>휠이 지금 열려 있는가. 다른 입력(카메라 · 상호작용)이 비켜 줄 때 본다.</summary>
        public static bool IsOpen { get; private set; }

        private int hovered = -1;

        /// <summary>
        /// 완드 스틱이 칸을 가리켰는가. 켜져 있는 동안은 마우스 위치로 가리킴을 덮어쓰지 않는다 —
        /// 안 그러면 가만히 있는 커서가 매 프레임 스틱이 고른 칸을 도로 빼앗는다. 마우스를 움직이면 꺼진다.
        /// </summary>
        private bool devicePointing;

        /// <summary>열리는 연출이 얼마나 지났나(초). 음수면 연출이 끝났다.</summary>
        private float openElapsed = -1f;

        private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        private void Awake()
        {
            for (int i = 0; i < slotLabels.Length; i++)
            {
                if (slotLabels[i] != null)
                {
                    slotLabels[i].text = NameOf(i);
                    slotLabels[i].color = labelColor;
                }
            }

            SetOpen(false);
        }

        private void OnDisable()
        {
            // 로비를 떠나며 숨겨질 때 열린 채로 남지 않게.
            SetOpen(false);
        }

        private void Update()
        {
            AnimateOpen();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || ChatFocus.Typing)
            {
                return;
            }

            if (keyboard[ToggleKey].wasPressedThisFrame)
            {
                SetOpen(!IsOpen);
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
                return;
            }

            for (int i = 0; i < NumberKeys.Length && i < slots.Length; i++)
            {
                if (keyboard[NumberKeys[i]].wasPressedThisFrame)
                {
                    Select(i);
                    return;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0f)
            {
                devicePointing = false;
            }

            if (!devicePointing)
            {
                SetHovered(mouse != null ? SlotUnder(mouse.position.ReadValue()) : -1);
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hovered >= 0)
            {
                Select(hovered);
            }
        }

        // ------------------------------------------------------------
        // 완드 — 키가 아니라 밖에서 부른다 (IotLobbyInteract)
        //
        // 완드는 IPlayerController 로만 들어오고 Input System 을 거치지 않아 키를 흉내낼 수 없다.
        // 그래서 Q · 마우스 · 좌클릭이 하는 일을 대신 불러 주는 자리를 연다. (IOT_INPUT.md 7장)
        // ------------------------------------------------------------

        /// <summary>Q 와 같다 — 닫혀 있으면 연다. 채팅 중이면 무시한다.</summary>
        public void OpenFromDevice()
        {
            if (!IsOpen && !ChatFocus.Typing)
            {
                SetOpen(true);
            }
        }

        /// <summary>
        /// 스틱 방향으로 칸을 가리킨다. 마우스를 휠 가운데서 그쪽으로 옮긴 것과 같다.
        ///
        /// 스틱을 놓아도(가운데로 돌아와도) <b>마지막에 가리킨 칸을 그대로 둔다.</b>
        /// 스틱을 기울인 채로 다른 손가락으로 확정 버튼을 누르기가 어렵다.
        /// </summary>
        /// <param name="direction">-1 ~ 1. 위가 +y 다.</param>
        public void PointFromDevice(Vector2 direction)
        {
            if (!IsOpen || slots.Length == 0 || direction.magnitude < DeviceDeadZone)
            {
                return;
            }

            devicePointing = true;

            // SlotUnder 와 같은 계산이다. 위(90°)에서 시계 방향, 반 칸만큼 돌려 잰다.
            float mathAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float slotSweep = 360f / slots.Length;
            float clockwiseFromTop = Mathf.Repeat(90f - mathAngle + slotSweep * 0.5f, 360f);

            SetHovered(Mathf.Clamp(Mathf.FloorToInt(clockwiseFromTop / slotSweep), 0, slots.Length - 1));
        }

        /// <summary>
        /// 확정. 가리킨 칸이 있으면 그 춤을 고르고, 없으면 그냥 닫는다. 좌클릭 · Esc 를 합친 것이다.
        /// </summary>
        public void ConfirmFromDevice()
        {
            if (!IsOpen)
            {
                return;
            }

            if (hovered >= 0)
            {
                Select(hovered);
            }
            else
            {
                SetOpen(false);
            }
        }

        /// <summary>이보다 덜 기울인 스틱은 가리킴으로 치지 않는다. 손을 떼도 스틱이 조금 남는다.</summary>
        private const float DeviceDeadZone = 0.5f;

        /// <summary>
        /// 화면 좌표가 가리키는 칸. <b>방향만 본다</b> — 칸 바깥이어도 그 방향의 칸이다(PEAK 와 같다).
        /// 중심 가까이(<see cref="deadZone"/>)면 -1.
        /// </summary>
        private int SlotUnder(Vector2 screenPoint)
        {
            if (wheel == null || slots.Length == 0)
            {
                return -1;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, wheel.position);
            Vector2 offset = screenPoint - center;

            float scale = canvas != null ? canvas.scaleFactor : 1f;
            if (offset.magnitude < deadZone * scale)
            {
                return -1;
            }

            // 위(90°)에서 시계 방향으로 잰 각도. 칸 0 은 위쪽 가운데에 걸쳐 있으므로 반 칸만큼 돌려 잰다.
            float mathAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float slotSweep = 360f / slots.Length;
            float clockwiseFromTop = Mathf.Repeat(90f - mathAngle + slotSweep * 0.5f, 360f);

            return Mathf.Clamp(Mathf.FloorToInt(clockwiseFromTop / slotSweep), 0, slots.Length - 1);
        }

        private void SetHovered(int index)
        {
            if (index == hovered)
            {
                return;
            }

            hovered = index;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                bool on = i == hovered;
                slots[i].color = on ? hoverColor : normalColor;

                if (i < slotBorders.Length && slotBorders[i] != null)
                {
                    slotBorders[i].color = on ? hoverBorderColor : borderColor;
                }

                // 뿌리가 있으면 뿌리를 키운다 — 테두리 · 이름 · 번호가 한 덩어리로 커진다.
                RectTransform root = i < slotRoots.Length && slotRoots[i] != null ? slotRoots[i] : null;
                if (root != null)
                {
                    root.localScale = Vector3.one * (on ? hoverScale : 1f);
                }
                else
                {
                    slots[i].rectTransform.localScale = Vector3.one * (on ? hoverScale : 1f);
                    if (i < slotLabels.Length && slotLabels[i] != null)
                    {
                        slotLabels[i].rectTransform.localScale = Vector3.one * (on ? hoverScale : 1f);
                    }
                }
            }

            if (centerLabel != null)
            {
                centerLabel.text = hovered >= 0 ? NameOf(hovered) : idleCenterText;
            }
        }

        /// <summary>
        /// 열릴 때 살짝 작은 크기에서 커지며 나타난다. 시간은 멈춤 · 슬로모션과 무관하게 흐른다.
        /// </summary>
        private void AnimateOpen()
        {
            if (openElapsed < 0f || wheel == null)
            {
                return;
            }

            openElapsed += Time.unscaledDeltaTime;
            float t = openSeconds <= 0f ? 1f : Mathf.Clamp01(openElapsed / openSeconds);

            // 끝으로 갈수록 느려지게(ease-out). 튀어나오는 느낌을 준다.
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            wheel.localScale = Vector3.one * Mathf.Lerp(openFromScale, 1f, eased);

            if (group != null)
            {
                group.alpha = eased;
            }

            if (t >= 1f)
            {
                openElapsed = -1f;
            }
        }

        private void Select(int index)
        {
            SetOpen(false);
            Debug.Log($"[춤] {index + 1}번 '{NameOf(index)}' 을 골랐습니다.", this);
            DanceSelected?.Invoke(index);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;

            if (canvas != null)
            {
                canvas.enabled = open;
            }

            // 열 때는 작게 · 투명하게 시작해서 AnimateOpen 이 키운다. 닫을 때는 바로 사라진다.
            openElapsed = open ? 0f : -1f;
            if (wheel != null)
            {
                wheel.localScale = Vector3.one * (open && openSeconds > 0f ? openFromScale : 1f);
            }
            if (group != null)
            {
                group.alpha = open && openSeconds > 0f ? 0f : 1f;
            }

            // 열 때마다 가리킴을 비운다. 마지막에 가리키던 칸이 밝은 채로 열리지 않게.
            devicePointing = false;
            hovered = int.MinValue;
            SetHovered(-1);
        }

        private string NameOf(int index)
        {
            return index >= 0 && index < danceNames.Length && !string.IsNullOrEmpty(danceNames[index])
                ? danceNames[index]
                : $"춤 {index + 1}";
        }
    }
}

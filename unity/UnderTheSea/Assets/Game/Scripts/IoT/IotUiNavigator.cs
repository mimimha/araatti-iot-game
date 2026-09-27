using System;
using System.Collections.Generic;
using MiniGames.Common;
using MiniGames.Common.UI;
using TMPro;
using UnderTheSea.Lobby;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// **선택지 창을 완드로 누르게 한다.** 창이 떠 있는 동안에만 돈다.
///
///     왼손 스틱     튕기면 그 방향의 버튼으로 강조가 옮겨 간다 (Tab 처럼). 쥐고 있으면 이어서 옮겨 간다
///     왼손 버튼 1   강조된 버튼을 누른다
///     왼손 버튼 2   닫는다 · 취소한다
///
/// 다루는 창 — 떠 있는 것 중 **위에서부터 하나**만 잡는다.
///
///     결과 판        ResultPanelPresenter       [다시 하기] [로비로]
///     인원 선택      CrewPickerScreen           [2인] [3인] … [매칭 취소]
///     매칭 판        CommonMatchingUI           [게임 시작] [매칭 취소]
///     이정표 이동    SignpostTeleportUI         이정표 목록 · [닫기]
///     제단 봉헌      AltarOfferingUIController  [-] [+] [MAX] [봉헌] [닫기]
///
/// **창 파일을 하나도 고치지 않습니다.** 전부 uGUI <see cref="Button"/> 이라, 창마다 따로 붙이지
/// 않고 여기 한 곳에서 버튼을 찾아 누릅니다. 누르는 것은 키 이벤트를 흉내내지 않고
/// <see cref="ExecuteEvents.submitHandler"/> 로 버튼에 직접 알립니다 — 키보드 Enter 로 누른 것과
/// 같은 길이고, 꺼진(interactable 이 아닌) 버튼은 스스로 거절합니다. (IOT_INPUT.md 7장)
///
/// ⚠ **EventSystem 의 선택(<c>SetSelectedGameObject</c>)은 쓰지 않습니다.** 선택을 걸어 두면
///   키보드 Space · Enter 가 그 버튼을 누릅니다. 인원 선택 창은 이동을 잠그지 않아서,
///   키보드로 점프하려던 사람이 버튼을 누르게 됩니다. 강조는 여기서 그리는 테두리로만 보입니다.
///
/// ⚠ **창이 떠 있는 동안 왼손 버튼은 여기가 가져갑니다.** 로비의 상호작용
///   (<see cref="IotLobbyInteract"/>)과 완드 이동(<c>PlayerInputProvider</c>)은
///   <see cref="IsScreenOpen"/> 을 보고 비켜 줍니다. 안 비키면 창의 버튼을 누르려던 왼손 버튼 1 이
///   포탈을 또 누르고, 스틱으로 강조를 옮기는 동안 캐릭터가 걸어갑니다.
///
/// **배 협동 설명 팝업**(<c>ShipCoopTutorialView</c>)은 선택지 창이 아니라 따로 닫습니다 — 버튼이 없고,
/// 게임을 막지 않아서 떠 있는 동안에도 걸어 다닙니다. **오른손 버튼 2** 를 누르면 닫힙니다.
/// 팝업이 Space(상호작용)를 일부러 안 받는 것과 같은 이유로, 완드에서도 누른 김에 게임이 움직이지
/// 않는 버튼을 골랐습니다. 오른손 버튼 2 는 발사 · 망치질이라 대포에 붙거나 망치를 들기 전에는 아무 일도 없습니다.
/// 로비 튜토리얼 [건너뛰기]는 로비 왼손 버튼을 받는 <see cref="IotLobbyInteract"/> 가 합니다.
///
/// 완드가 안 붙어 있으면 아무것도 하지 않습니다. 키보드 · 마우스는 예전 그대로입니다.
/// 서버 빌드에는 만들지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class IotUiNavigator : MonoBehaviour
{
    /// <summary>이만큼 기울이면 한 칸 옮긴다.</summary>
    private const float FlickOn = 0.6f;

    /// <summary>이 안으로 돌아와야 다음 튕김을 받는다. 두 값을 벌려 둬야 경계에서 떨지 않는다.</summary>
    private const float FlickOff = 0.3f;

    /// <summary>쥐고 있을 때 이어서 옮기기 시작하는 시간(초).</summary>
    private const float RepeatDelay = 0.45f;

    /// <summary>이어서 옮길 때의 간격(초).</summary>
    private const float RepeatInterval = 0.16f;

    /// <summary>
    /// 창 부품을 다시 찾는 간격(초). 창은 처음 열 때 코드로 생기는 것이 많아 한 번 찾고 끝낼 수 없다.
    /// 매 프레임 씬을 뒤지면 비싸다.
    /// </summary>
    private const float RescanSeconds = 0.5f;

    private static IotUiNavigator _instance;

    // 창 부품. 떠 있는지는 매 프레임 속성으로 보고, 부품 자체는 가끔 다시 찾는다.
    private ResultPanelPresenter[] _results = Array.Empty<ResultPanelPresenter>();
    private CrewPickerScreen[] _crewPickers = Array.Empty<CrewPickerScreen>();
    private SignpostTeleportUI[] _signposts = Array.Empty<SignpostTeleportUI>();
    private AltarOfferingUIController[] _altars = Array.Empty<AltarOfferingUIController>();
    private ShipCoopTutorialView[] _shipTutorials = Array.Empty<ShipCoopTutorialView>();
    private float _nextRescan;

    /// <summary>지난 프레임에 오른손 버튼 2 를 쥐고 있었나. 배 설명 팝업을 누른 순간에만 닫는다.</summary>
    private bool _shipCloseHeld;

    /// <summary>이번 프레임에 잡은 창. 같은 프레임에 여러 곳이 물어도 한 번만 훑는다.</summary>
    private int _probedFrame = -1;
    private OpenScreen _screen;

    private readonly List<Selectable> _candidates = new List<Selectable>();
    private readonly List<Selectable> _scratch = new List<Selectable>();
    private readonly Vector3[] _corners = new Vector3[4];

    private object _screenOwner;
    private Selectable _selected;

    private bool _stickHeld;
    private float _nextRepeat;

    /// <summary>
    /// 스틱을 한 번 놓을 때까지 옮기지 않는다. 창이 뜬 순간에 켠다 — 걸어가다 버튼 1 로 창을 열면
    /// 스틱이 아직 기울어 있어서, 안 막으면 뜨자마자 강조가 엉뚱한 곳으로 달아난다.
    /// </summary>
    private bool _waitRelease;

    private RectTransform _frame;
    private Image[] _frameEdges = Array.Empty<Image>();

    /// <summary>떠 있는 창 하나. 버튼을 찾을 범위와, 왼손 버튼 2 로 닫는 방법.</summary>
    private readonly struct OpenScreen
    {
        public OpenScreen(object owner, Transform scope, Action close)
        {
            Owner = owner;
            Scope = scope;
            Close = close;
        }

        public object Owner { get; }
        public Transform Scope { get; }
        public Action Close { get; }
        public bool IsOpen => Owner != null;
    }

#if !UNITY_SERVER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject("[IotUiNavigator]");
        DontDestroyOnLoad(host);
        host.AddComponent<IotUiNavigator>();
    }
#endif

    /// <summary>
    /// **완드가 누를 선택지 창이 떠 있는가.** 떠 있으면 왼손 버튼 · 왼손 스틱은 이 창의 것이다.
    ///
    /// 로비 상호작용과 완드 이동이 이것을 보고 비켜 준다. 부르는 순서(Update 순서)와 상관없이
    /// 같은 프레임에는 같은 답이 나온다. 서버 빌드와 완드가 없는 PC 에서는 늘 false 다.
    /// </summary>
    public static bool IsScreenOpen()
    {
        return _instance != null
               && IotPlayerController.IsWandLive(IotPlayerController.Persistent)
               && _instance.Probe().IsOpen;
    }

    private void Awake()
    {
        _instance = this;
        BuildFrame();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    private void Update()
    {
        IotPlayerController wand = IotPlayerController.Persistent;

        if (!IotPlayerController.IsWandLive(wand))
        {
            Release();
            return;
        }

        OpenScreen screen = Probe();

        CloseShipTutorial(wand.Right);

        if (!screen.IsOpen)
        {
            Release();
            return;
        }

        if (!ReferenceEquals(screen.Owner, _screenOwner))
        {
            // 다른 창이 떴다. 앞 창에서 고르던 버튼을 들고 가지 않는다.
            _screenOwner = screen.Owner;
            _selected = null;
            _waitRelease = true;
        }

        CollectCandidates(screen.Scope);
        KeepSelectionValid();

        IHandDevice left = wand.Left;

        // ⚠ 창이 떠 있는 동안에는 쓰든 안 쓰든 눌림을 가져간다. 남겨 두면 창이 닫히는 순간
        //   로비 상호작용이 그것을 받아 포탈을 또 누른다.
        bool submit = left.ConsumeButton1Press();
        bool back = left.ConsumeButton2Press();

        Navigate(left.Stick);

        if (back && screen.Close != null)
        {
            screen.Close();
        }
        else if (submit && _selected != null)
        {
            ExecuteEvents.Execute(_selected.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        DrawFrame();
    }

    // ------------------------------------------------------------
    // 떠 있는 창 찾기
    // ------------------------------------------------------------

    private OpenScreen Probe()
    {
        if (_probedFrame == Time.frameCount)
        {
            return _screen;
        }

        _probedFrame = Time.frameCount;

        if (Time.unscaledTime >= _nextRescan)
        {
            _nextRescan = Time.unscaledTime + RescanSeconds;
            _results = FindObjectsByType<ResultPanelPresenter>(FindObjectsInactive.Include);
            _crewPickers = FindObjectsByType<CrewPickerScreen>(FindObjectsInactive.Include);
            _signposts = FindObjectsByType<SignpostTeleportUI>(FindObjectsInactive.Include);
            _altars = FindObjectsByType<AltarOfferingUIController>(FindObjectsInactive.Include);
            _shipTutorials = FindObjectsByType<ShipCoopTutorialView>(FindObjectsInactive.Include);
        }

        _screen = FindOpenScreen();
        return _screen;
    }

    /// <summary>
    /// 배 협동 설명 팝업이 떠 있으면 오른손 버튼 2 를 누른 순간 닫는다.
    ///
    /// ⚠ <c>ConsumeButton2Press</c> 로 읽지 않고 누르고 있는 상태의 변화로 잡는다. 오른손 버튼 2 는
    ///   배의 발사 · 망치질이라, 눌림을 여기서 가져가면 게임이 그것을 못 받는 날이 생긴다.
    /// </summary>
    private void CloseShipTutorial(IHandDevice right)
    {
        bool held = right.Button2;
        bool pressed = held && !_shipCloseHeld;
        _shipCloseHeld = held;

        if (!pressed)
        {
            return;
        }

        foreach (ShipCoopTutorialView tutorial in _shipTutorials)
        {
            if (tutorial != null && tutorial.IsShowing) tutorial.Hide();
        }
    }

    /// <summary>
    /// 떠 있는 창 가운데 위에 있는 것 하나. 결과 판이 매칭 판보다, 인원 선택이 매칭 판보다 위다.
    /// </summary>
    private OpenScreen FindOpenScreen()
    {
        foreach (ResultPanelPresenter result in _results)
        {
            // 결과 판에는 "닫기" 가 없다. [로비로] 를 눌러야 끝난다.
            if (result != null && result.IsOpen) return new OpenScreen(result, result.transform, null);
        }

        foreach (CrewPickerScreen picker in _crewPickers)
        {
            // 열렸는지 알려 주는 속성이 없다. 닫히면 판 전체가 꺼지므로 살아 있는 버튼으로 가른다.
            if (picker != null && HasLiveButton(picker.transform))
            {
                Transform scope = picker.transform;
                return new OpenScreen(picker, scope, () => ClickLabelled(scope));
            }
        }

        CommonMatchingUI matching = CommonMatchingUI.Current;
        if (matching != null && matching.IsShown && HasLiveButton(matching.transform))
        {
            Transform scope = matching.transform;
            return new OpenScreen(matching, scope, () => ClickLabelled(scope));
        }

        foreach (SignpostTeleportUI signpost in _signposts)
        {
            if (signpost != null && signpost.IsOpen) return new OpenScreen(signpost, signpost.transform, signpost.Close);
        }

        foreach (AltarOfferingUIController altar in _altars)
        {
            // Esc 와 같은 길로 닫는다. 채팅칸이 잠그고 있으면 채팅이 먼저다.
            if (altar != null && altar.IsOpen) return new OpenScreen(altar, ScopeOf(altar.transform), altar.CloseFromEscape);
        }

        return default;
    }

    /// <summary>
    /// 버튼을 찾을 범위. 부품 아래에 버튼이 없으면 그 부품이 놓인 캔버스 전체로 넓힌다 —
    /// 창 판(panelRoot)을 부품 바깥에 두는 구성도 있다.
    /// </summary>
    private Transform ScopeOf(Transform owner)
    {
        if (HasLiveButton(owner))
        {
            return owner;
        }

        Canvas canvas = owner.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas.transform : owner;
    }

    private bool HasLiveButton(Transform scope)
    {
        scope.GetComponentsInChildren(false, _scratch);

        foreach (Selectable selectable in _scratch)
        {
            if (IsPressable(selectable)) return true;
        }

        return false;
    }

    /// <summary>
    /// 라벨이 "취소" · "닫기" 인 버튼을 누른다. 창이 닫는 함수를 열어 두지 않았을 때 쓴다.
    ///
    /// 인원 선택 창의 <c>Close()</c> 는 판만 끄고 취소를 알리지 않아서 매칭 흐름이 멈춘다.
    /// 버튼을 누르면 창이 스스로 하는 일(취소 알림)까지 그대로 한다.
    /// </summary>
    private void ClickLabelled(Transform scope)
    {
        scope.GetComponentsInChildren(false, _scratch);

        foreach (Selectable selectable in _scratch)
        {
            if (!IsPressable(selectable)) continue;

            TMP_Text label = selectable.GetComponentInChildren<TMP_Text>(true);
            string text = label != null ? label.text : string.Empty;

            if (text.Contains("취소") || text.Contains("닫기"))
            {
                ExecuteEvents.Execute(selectable.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                return;
            }
        }
    }

    // ------------------------------------------------------------
    // 강조 옮기기
    // ------------------------------------------------------------

    /// <summary>버튼 · 토글만 누른다. 글자 입력칸은 완드로 칠 수 없으니 건너뛴다.</summary>
    private static bool IsPressable(Selectable selectable)
    {
        return selectable != null
               && (selectable is Button || selectable is Toggle)
               && selectable.isActiveAndEnabled
               && selectable.IsInteractable();
    }

    private void CollectCandidates(Transform scope)
    {
        _candidates.Clear();
        scope.GetComponentsInChildren(false, _scratch);

        foreach (Selectable selectable in _scratch)
        {
            if (IsPressable(selectable)) _candidates.Add(selectable);
        }
    }

    /// <summary>
    /// 강조된 버튼이 꺼지거나 사라졌으면 다른 버튼으로 옮긴다.
    ///
    /// 처음에는 닫기 · 취소가 아닌 첫 버튼을 고른다. 버튼이 꺼져서 옮길 때는 **가장 가까운 것**으로 —
    /// 제단의 [+] 가 최대치에서 꺼지면 바로 옆의 [MAX] · [봉헌] 으로 넘어간다.
    /// </summary>
    private void KeepSelectionValid()
    {
        if (_selected != null && _candidates.Contains(_selected))
        {
            return;
        }

        Selectable previous = _selected;
        _selected = null;

        if (_candidates.Count == 0)
        {
            return;
        }

        if (previous != null)
        {
            Vector2 from = ScreenCenter(previous);
            float best = float.MaxValue;

            foreach (Selectable candidate in _candidates)
            {
                float distance = (ScreenCenter(candidate) - from).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    _selected = candidate;
                }
            }

            return;
        }

        foreach (Selectable candidate in _candidates)
        {
            TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
            string text = label != null ? label.text : string.Empty;

            if (!text.Contains("취소") && !text.Contains("닫기"))
            {
                _selected = candidate;
                return;
            }
        }

        _selected = _candidates[0];
    }

    /// <summary>스틱을 튕기면 한 칸, 쥐고 있으면 잠시 뒤부터 이어서 옮긴다.</summary>
    private void Navigate(Vector2 stick)
    {
        float amount = stick.magnitude;

        if (amount < FlickOff)
        {
            _stickHeld = false;
            _waitRelease = false;
            return;
        }

        if (amount < FlickOn || _waitRelease)
        {
            return;
        }

        float now = Time.unscaledTime;

        if (_stickHeld && now < _nextRepeat)
        {
            return;
        }

        _nextRepeat = now + (_stickHeld ? RepeatInterval : RepeatDelay);
        _stickHeld = true;

        // 대각선은 더 많이 기운 쪽으로만 본다. 반쯤 대각이면 어디로 갈지 사람이 예측할 수 없다.
        Vector2 direction = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y)
            ? new Vector2(Mathf.Sign(stick.x), 0f)
            : new Vector2(0f, Mathf.Sign(stick.y));

        Step(direction);
    }

    /// <summary>
    /// 그 방향에 있는 버튼 가운데 가장 가까운 것으로 옮긴다. 옆으로 벗어난 거리는 더 무겁게 친다.
    ///
    /// Selectable 의 내비게이션 설정을 쓰지 않고 화면 위치로 직접 고른다. 코드로 그린 창들이
    /// 내비게이션을 따로 맞춰 두지 않았고, 그 설정을 믿으면 창마다 동작이 달라진다.
    /// 그 방향에 버튼이 없으면 그대로 둔다(되돌아 감지 않는다).
    /// </summary>
    private void Step(Vector2 direction)
    {
        if (_selected == null)
        {
            return;
        }

        Vector2 from = ScreenCenter(_selected);
        Selectable best = null;
        float bestScore = float.MaxValue;

        foreach (Selectable candidate in _candidates)
        {
            if (candidate == _selected) continue;

            Vector2 gap = ScreenCenter(candidate) - from;
            float along = Vector2.Dot(gap, direction);

            if (along <= 1f) continue;

            float aside = Mathf.Abs(gap.x * direction.y - gap.y * direction.x);
            float score = along + aside * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            _selected = best;
        }
    }

    private Vector2 ScreenCenter(Selectable selectable)
    {
        ScreenRect(selectable, out Vector2 min, out Vector2 max);
        return (min + max) * 0.5f;
    }

    private void ScreenRect(Selectable selectable, out Vector2 min, out Vector2 max)
    {
        RectTransform rect = (RectTransform)selectable.transform;
        rect.GetWorldCorners(_corners);

        Canvas canvas = selectable.GetComponentInParent<Canvas>();
        Camera eye = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;

        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < _corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(eye, _corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
    }

    // ------------------------------------------------------------
    // 강조 테두리
    // ------------------------------------------------------------

    private static readonly Color FrameColor = new Color(1f, 0.85f, 0.3f, 1f);
    private const float FrameThickness = 4f;
    private const float FramePadding = 6f;

    /// <summary>
    /// 강조된 버튼 둘레에 그릴 테두리. 화면 맨 위 캔버스에 따로 둔다.
    ///
    /// 버튼 자체의 색(Selected 상태)을 쓰지 않는 이유는 위의 SetSelectedGameObject ⚠ 와 같다.
    /// 코드로 그린 창들은 Selected 색을 따로 정하지 않아 바뀌어도 눈에 안 띄기도 한다.
    /// </summary>
    private void BuildFrame()
    {
        GameObject canvasHost = new GameObject("Focus", typeof(Canvas));
        canvasHost.transform.SetParent(transform, false);

        Canvas canvas = canvasHost.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // 튜토리얼(32766)보다도 위

        _frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        _frame.SetParent(canvasHost.transform, false);
        _frame.anchorMin = Vector2.zero;
        _frame.anchorMax = Vector2.zero;
        _frame.pivot = Vector2.zero;

        _frameEdges = new[]
        {
            MakeEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FrameThickness)),
            MakeEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FrameThickness)),
            MakeEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FrameThickness, 0f)),
            MakeEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FrameThickness, 0f)),
        };

        _frame.gameObject.SetActive(false);
    }

    private Image MakeEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        RectTransform edge = new GameObject(edgeName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        edge.SetParent(_frame, false);
        edge.anchorMin = anchorMin;
        edge.anchorMax = anchorMax;
        edge.pivot = pivot;
        edge.sizeDelta = size;
        edge.anchoredPosition = Vector2.zero;

        Image image = edge.GetComponent<Image>();
        image.color = FrameColor;
        image.raycastTarget = false;   // 테두리가 마우스 클릭을 가로채면 안 된다
        return image;
    }

    private void DrawFrame()
    {
        if (_selected == null)
        {
            _frame.gameObject.SetActive(false);
            return;
        }

        ScreenRect(_selected, out Vector2 min, out Vector2 max);

        _frame.gameObject.SetActive(true);
        _frame.anchoredPosition = min - Vector2.one * FramePadding;
        _frame.sizeDelta = max - min + Vector2.one * (FramePadding * 2f);

        // 살짝 숨 쉬게 해서 멈춘 화면에서도 어디가 강조인지 눈에 들게 한다.
        Color color = FrameColor;
        color.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);

        foreach (Image edge in _frameEdges)
        {
            edge.color = color;
        }
    }

    /// <summary>창이 없어졌다. 강조를 거두고 다음 창을 처음부터 받을 준비를 한다.</summary>
    private void Release()
    {
        _screenOwner = null;
        _selected = null;

        if (_frame != null && _frame.gameObject.activeSelf)
        {
            _frame.gameObject.SetActive(false);
        }
    }
}

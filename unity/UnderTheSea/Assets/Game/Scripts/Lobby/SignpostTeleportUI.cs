using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnderTheSea.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **이정표에서 F 를 눌렀을 때 뜨는 "어디로 갈까" 창.**
    ///
    /// <code>
    ///   이정표에서 F  →  SignpostTeleport.Requested  →  이 창이 열린다
    ///   목록에서 고르면  →  그 이정표 앞으로 간다
    ///   Esc 또는 [닫기]  →  아무 일 없이 닫힌다
    /// </code>
    ///
    /// <b>화면을 코드로 그린다.</b> 미니게임 인원 선택 창(<c>CrewPickerScreen</c>)과 같은
    /// 방식·같은 모양이다. 프리팹으로 만들었더니 목록 길이에 따라 판이 안 늘어나고 줄이
    /// 틀 밖으로 삐져나왔다. 개수가 달라지는 목록은 코드로 그리는 편이 낫다.
    ///
    /// <b>목록은 씬에서 저절로 모인다.</b> <see cref="SignpostTeleport.Registry"/> 가 놓여 있는
    /// 이정표를 전부 알고 있다. 이정표를 하나 더 놓으면 목록에 저절로 늘고, 이름을 바꾸면
    /// 그대로 따라온다.
    ///
    /// ⚠ <b>같은 이름은 한 번만 보여 준다.</b> 에디터에서 Lobby 를 직접 Play 하면 Fusion 이
    ///    같은 씬을 한 벌 더 올려 이정표가 두 벌이 된다. 그대로 두면 목록이 두 번 나온다.
    ///
    /// ⚠ <b>창이 열린 동안 이동과 다른 상호작용을 막는다.</b> <see cref="ChatFocus"/> 로
    ///    잠근다. 이것은 채팅 전용이 아니라 로비의 공용 입력 잠금이고, 제단 창도 쓴다.
    ///    내 창에서 Esc 를 볼 때는 <see cref="ChatFocus.Typing"/> 이 아니라
    ///    <see cref="ChatFocus.HeldByOther"/> 를 봐야 한다 — 열려 있는 동안 나 자신이 보유자다.
    ///
    /// ⚠ <b>아직 실제로 옮기지는 않는다.</b> 로비 캐릭터의 위치 권한은 서버에 있다
    ///    (<c>NetworkPlayerMover</c> 는 <c>HasStateAuthority</c> 가 아니면 아무것도 안 한다).
    ///    클라이언트가 <c>transform.position</c> 을 바꿔도 다음 틱에 되돌아간다. 서버에
    ///    요청하는 RPC 를 넣어야 하는데 그러면 서버도 다시 구워 올려야 하므로 나중에 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SignpostTeleportUI : MonoBehaviour
    {
        private const string HostName = "[이정표 이동]";

        // CrewPickerScreen 과 같은 색을 쓴다. 같은 게임의 같은 창으로 보여야 한다.
        private static readonly Color Shade = new Color(0f, .02f, .06f, .78f);
        private static readonly Color Panel = new Color(.047f, .149f, .286f, 1f);
        private static readonly Color Row = new Color(.10f, .32f, .55f, 1f);
        private static readonly Color CloseTint = new Color(.28f, .10f, .14f, 1f);
        private static readonly Color Muted = new Color(.72f, .81f, .92f, 1f);

        private const float PanelW = 760f;
        private const float RowW = 620f, RowH = 68f, RowGap = 10f;
        private const float HeadH = 150f, FootH = 120f;

        private static SignpostTeleportUI instance;

        private GameObject root;
        private RectTransform panel;
        private TMP_Text titleText;
        private TMP_Text fromText;
        private RectTransform list;
        private RectTransform closeRect;

        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>지금 서 있는 이정표. 창이 닫혀 있으면 null.</summary>
        private SignpostTeleport origin;

        /// <summary>창이 열려 있는가.</summary>
        public bool IsOpen => origin != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 서버는 창을 열 일이 없다. 캐릭터도 키보드도 없다.
            if (FusionLaunchArguments.IsDedicatedServerProcess() || instance != null)
            {
                return;
            }

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SignpostTeleportUI>();
        }

        private void Awake()
        {
            Build();
            SignpostTeleport.Requested += Open;
        }

        private void OnDestroy()
        {
            SignpostTeleport.Requested -= Open;

            // 창이 열린 채로 사라지면 잠금이 영영 남아 걷지 못하게 된다.
            Release();
        }

        private void Update()
        {
            if (!IsOpen || ChatFocus.HeldByOther(this))
            {
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        // ───────────────────────────── 열고 닫기 ─────────────────────────────

        private void Open(SignpostTeleport from)
        {
            if (from == null || IsOpen)
            {
                return;
            }

            origin = from;
            fromText.text = $"지금 있는 곳 — {from.DisplayName}";

            Fill(from);
            root.SetActive(true);

            ChatFocus.Begin(this);
        }

        /// <summary>창을 닫는다.</summary>
        public void Close()
        {
            root.SetActive(false);
            Release();
        }

        private void Release()
        {
            if (origin == null)
            {
                return;
            }

            ChatFocus.End(this);
            origin = null;
        }

        // ───────────────────────────── 목록 ─────────────────────────────

        private void Fill(SignpostTeleport from)
        {
            foreach (GameObject row in rows) Destroy(row);
            rows.Clear();

            Transform me = UnderTheSea.Network.LocalPlayer.Transform;

            // ⚠ 같은 이름이 두 번 나오지 않게 거른다. 클래스 주석의 경고를 보라.
            var seen = new HashSet<string> { from.DisplayName };

            foreach (SignpostTeleport post in SignpostTeleport.Registry)
            {
                if (post == null || !seen.Add(post.DisplayName))
                {
                    continue;
                }

                string distance = string.Empty;
                if (me != null)
                {
                    Vector3 delta = post.ArrivalPoint - me.position;
                    delta.y = 0f;
                    distance = $"{delta.magnitude:0} m";
                }

                SignpostTeleport chosen = post;   // ⚠ 람다가 붙잡는다. 복사하지 않으면 전부 마지막 값.
                rows.Add(MakeRow(list, post.DisplayName, distance, () => Go(chosen)));
            }

            Resize(rows.Count);
        }

        /// <summary>줄 수에 맞춰 판을 늘린다. 프리팹으로 만들었을 때 삐져나오던 문제를 없앤다.</summary>
        private void Resize(int count)
        {
            float listH = count <= 0 ? RowH : count * RowH + (count - 1) * RowGap;

            list.sizeDelta = new Vector2(RowW, listH);
            panel.sizeDelta = new Vector2(PanelW, HeadH + listH + FootH);

            // 판 안에서 머리·목록·발을 위에서부터 쌓는다.
            float top = panel.sizeDelta.y * 0.5f;

            ((RectTransform)titleText.transform).anchoredPosition = new Vector2(0f, top - 52f);
            ((RectTransform)fromText.transform).anchoredPosition = new Vector2(0f, top - 100f);
            list.anchoredPosition = new Vector2(0f, top - HeadH - listH * 0.5f);
            closeRect.anchoredPosition = new Vector2(0f, top - HeadH - listH - 56f);
        }

        /// <summary>한쪽 방향 페이드에 걸리는 시간(초). 왕복은 그 두 배다.</summary>
        private const float FadeSeconds = 0.25f;

        /// <summary>도착했다고 볼 거리(m). 서버가 옮긴 뒤 내 쪽에 반영되는 것을 기다린다.</summary>
        private const float ArrivedWithin = 2f;

        /// <summary>
        /// 도착을 기다리는 최대 시간(초).
        ///
        /// 서버 왕복은 보통 20~60ms 다. 그보다 훨씬 길게 잡아 두되, 네트워크가 끊겨
        /// 영영 안 오는 경우에도 <b>화면이 까만 채로 남지는 않게</b> 한다.
        /// </summary>
        private const float ArrivalTimeout = 1.5f;

        /// <summary>
        /// 목록에서 한 곳을 골랐을 때. <b>서버에 보내 달라고 청한다.</b>
        ///
        /// 여기서 <c>transform.position</c> 을 직접 바꾸면 안 된다. 로비 캐릭터의 위치는
        /// 서버만 정하므로 다음 틱에 되돌아간다.
        ///
        /// <b>화면을 가리고 그 안에서 옮긴다.</b> 캐릭터는 한 틱 만에 도착하지만 카메라는
        /// 초당 90 유닛으로 뒤따라가므로(<c>ThirdPersonCamera</c>), 로비 끝에서 끝인
        /// 118m 면 1.3초 동안 화면이 날아간다. 거리마다 시간이 달라 순간이동처럼 안 보인다.
        /// 가려 두고 그 안에서 카메라를 제자리에 놓으면 <b>어디로 가든 같은 시간</b>이 된다.
        /// </summary>
        private void Go(SignpostTeleport target)
        {
            if (target == null)
            {
                return;
            }

            string from = origin?.DisplayName;
            Close();

            NetworkObject me = UnderTheSea.Network.LocalPlayer.Object;
            var mover = me != null ? me.GetComponent<NetworkPlayerMover>() : null;

            if (mover == null)
            {
                Debug.LogWarning(
                    $"[이정표] \"{target.DisplayName}\" 으로 가려 했지만 내 캐릭터를 못 찾았습니다.");
                return;
            }

            Debug.Log($"[이정표] \"{from}\" 에서 \"{target.DisplayName}\" 으로 갑니다.");

            var view = me.GetComponent<LocalPlayerView>();
            Vector3 arrival = target.ArrivalPoint;

            // 어두워지기 시작할 때 얼린다. 이 한 줄이 빠지면 페이드 중에 카메라가
            // 날아가기 시작하는 것이 보인다.
            view?.FreezeCamera(true);

            // ⚠ **얼렸으면 반드시 풀어야 한다.** 가리기가 시작되지 않으면 Ride 도 돌지
            //    않으므로 얼음을 풀어 줄 사람이 없다. 그대로 두면 카메라가 캐릭터에서
            //    떨어진 채 영영 굳는다. 실제로 그렇게 됐다.
            if (!ScreenFade.Cover(() => Ride(mover, view, me.transform, arrival), FadeSeconds))
            {
                view?.FreezeCamera(false);
                Debug.LogWarning("[이정표] 이미 이동 연출이 도는 중이라 이번 선택은 무시합니다.");
            }
        }

        /// <summary>
        /// **화면이 까만 동안 벌어지는 일.**
        ///
        /// <code>
        ///   1. 서버에 보내 달라고 청한다
        ///   2. 내 캐릭터가 그 자리에 오는 것을 기다린다
        ///   3. 카메라를 그 자리로 옮기고 얼음을 푼다
        /// </code>
        ///
        /// ⚠ <b>2번을 기다려야 한다.</b> 안 기다리고 바로 카메라를 옮기면 아직 옛 자리에
        ///    있는 캐릭터 뒤에 카메라가 서고, 그 뒤에 캐릭터가 도착하면서 다시 날아간다.
        ///
        /// ⚠ <b>서버가 거절할 수 있다.</b> 좌표가 이정표 앞이 아니면 보내 주지 않는다
        ///    (<see cref="SignpostTeleport.IsKnownArrival"/>). 그때는 영영 도착하지 않으므로
        ///    <see cref="ArrivalTimeout"/> 이 지나면 포기하고 화면을 되돌린다.
        /// </summary>
        private IEnumerator Ride(
            NetworkPlayerMover mover, LocalPlayerView view, Transform me, Vector3 arrival)
        {
            // ⚠ **어떻게 끝나든 얼음은 풀어야 한다.** 중간에 캐릭터가 사라져 빠져나가도,
            //    서버가 거절해 시간이 다 지나가도 마찬가지다. 한 번이라도 안 풀리면
            //    카메라가 캐릭터에서 떨어진 채 굳어 버린다.
            try
            {
                mover.RpcRequestTeleport(arrival);

                float waited = 0f;
                bool arrived = false;

                while (waited < ArrivalTimeout)
                {
                    if (me == null)
                    {
                        // 기다리는 사이 캐릭터가 사라졌다(접속 종료 등). 더 할 것이 없다.
                        yield break;
                    }

                    if ((me.position - arrival).sqrMagnitude <= ArrivedWithin * ArrivedWithin)
                    {
                        arrived = true;
                        break;
                    }

                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!arrived)
                {
                    Debug.LogWarning(
                        $"[이정표] {ArrivalTimeout:0.0}초를 기다렸는데 {arrival.ToString("F2")} 에 " +
                        "도착하지 않았습니다. 서버가 거절했거나 응답이 늦습니다. 화면만 되돌립니다.");
                }

                // 카메라를 제자리에 놓는다. 푸는 것보다 먼저다 — 풀고 나서 옮기면
                // 한 프레임 동안 옛 자리에서 따라가기가 돌아 버린다.
                view?.SnapCameraToMe();
            }
            finally
            {
                view?.FreezeCamera(false);
            }
        }

        // ───────────────────────────── 그리기 ─────────────────────────────

        private void Build()
        {
            root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, worldPositionStays: false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 뒤를 덮어 로비가 눌리지 않게 한다.
            GameObject shade = MakeImage(root.transform, "Shade", Shade);
            Stretch((RectTransform)shade.transform);

            GameObject panelGo = MakeImage(root.transform, "Panel", Panel);
            panel = (RectTransform)panelGo.transform;
            panel.anchoredPosition = Vector2.zero;

            titleText = MakeText(panel, "Title", 40f, Color.white);
            ((RectTransform)titleText.transform).sizeDelta = new Vector2(PanelW - 60f, 56f);
            titleText.text = "어디로 갈까요";

            fromText = MakeText(panel, "From", 22f, Muted);
            ((RectTransform)fromText.transform).sizeDelta = new Vector2(PanelW - 60f, 30f);

            var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(panel, worldPositionStays: false);
            list = (RectTransform)listGo.transform;

            var layout = listGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap;
            layout.childAlignment = TextAnchor.UpperCenter;

            // ⚠ **이 두 줄이 없으면 줄이 0×0 이 된다.** 레이아웃 그룹이 자식 크기를 자기가
            //    정하려 드는데, 우리 줄에는 LayoutElement 가 없어 0 으로 잡힌다.
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject close = MakeButton(panel, "닫기", CloseTint, Close, new Vector2(200f, 64f));
            closeRect = (RectTransform)close.transform;

            Resize(0);
            root.SetActive(false);
        }

        private static GameObject MakeRow(Transform parent, string name, string distance, Action onClick)
        {
            GameObject go = MakeImage(parent, $"Row {name}", Row);
            ((RectTransform)go.transform).sizeDelta = new Vector2(RowW, RowH);

            TMP_Text label = MakeText(go.transform, "Name", 28f, Color.white);
            var labelRect = (RectTransform)label.transform;
            labelRect.sizeDelta = new Vector2(RowW - 200f, RowH);
            labelRect.anchoredPosition = new Vector2(-70f, 0f);
            label.alignment = TextAlignmentOptions.Left;
            label.text = name;

            TMP_Text far = MakeText(go.transform, "Distance", 22f, Muted);
            var farRect = (RectTransform)far.transform;
            farRect.sizeDelta = new Vector2(120f, RowH);
            farRect.anchoredPosition = new Vector2(RowW * 0.5f - 80f, 0f);
            far.alignment = TextAlignmentOptions.Right;
            far.text = distance;

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => onClick?.Invoke());
            return go;
        }

        private static GameObject MakeImage(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.GetComponent<Image>().color = colour;
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text MakeText(Transform parent, string name, float size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static GameObject MakeButton(Transform parent, string label, Color colour,
            Action onClick, Vector2 size)
        {
            GameObject go = MakeImage(parent, $"Button {label}", colour);
            ((RectTransform)go.transform).sizeDelta = size;

            TMP_Text text = MakeText(go.transform, "Label", 28f, Color.white);
            Stretch((RectTransform)text.transform);
            text.text = label;

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => onClick?.Invoke());
            return go;
        }
    }
}

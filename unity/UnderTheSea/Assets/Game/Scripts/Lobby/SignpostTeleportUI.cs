using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using MiniGames.Common;
using MiniGames.Common.UI;
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
    /// 방식이고, 모양은 매칭 화면에서 빌린다(금테 남색 판 · 금색 버튼 · 제목 글꼴).
    /// 프리팹으로 만들었더니 목록 길이에 따라 판이 안 늘어나고 줄이
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

        // 매칭 화면 · 금테 판을 못 찾았을 때만 쓰는 예비 색. 평소에는 빌려 온 모양으로 그린다.
        private static readonly Color Shade = new Color(0f, .02f, .06f, .78f);
        private static readonly Color Panel = new Color(.047f, .149f, .286f, 1f);
        private static readonly Color Row = new Color(.10f, .32f, .55f, 1f);
        private static readonly Color CloseTint = new Color(.28f, .10f, .14f, 1f);
        private static readonly Color Muted = new Color(.72f, .81f, .92f, 1f);

        private const float PanelW = 760f;
        // 줄 폭은 이름 · 거리만 들어갈 만큼. 620 이었을 때는 버튼 안 양옆이 너무 비었다.
        private const float RowW = 480f, RowH = 68f, RowGap = 10f;

        /// <summary>줄 왼쪽 징의 지름과 자리(줄 왼쪽 끝에서 징 가운데까지).</summary>
        private const float StudSize = 14f, StudFromLeft = 56f;
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
            // 그리기는 처음 열 때 한다. 모양을 빌려 올 매칭 화면이 이 시점에는 아직 없을 수 있다.
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

            EnsureBuilt();

            origin = from;
            fromText.text = $"지금 있는 곳 — {from.DisplayName}";

            Fill(from);
            root.SetActive(true);

            ChatFocus.Begin(this);
        }

        /// <summary>창을 닫는다.</summary>
        public void Close()
        {
            if (root != null) root.SetActive(false);
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

            // 도착 방향이 정해진 이정표면 카메라를 그 등 뒤에 세운다. 캐릭터는 서버가 같은 값으로 돌린다.
            float? faceYaw = target.TryGetArrivalYaw(out float yaw) ? yaw : (float?)null;

            // 어두워지기 시작할 때 얼린다. 이 한 줄이 빠지면 페이드 중에 카메라가
            // 날아가기 시작하는 것이 보인다.
            view?.FreezeCamera(true);

            // ⚠ **얼렸으면 반드시 풀어야 한다.** 가리기가 시작되지 않으면 Ride 도 돌지
            //    않으므로 얼음을 풀어 줄 사람이 없다. 그대로 두면 카메라가 캐릭터에서
            //    떨어진 채 영영 굳는다. 실제로 그렇게 됐다.
            if (!ScreenFade.Cover(() => Ride(mover, view, me.transform, arrival, faceYaw), FadeSeconds))
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
            NetworkPlayerMover mover, LocalPlayerView view, Transform me, Vector3 arrival, float? faceYaw)
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

                // 도착 방향이 정해진 이정표면 카메라 좌우 각도를 그쪽으로. 도착했을 때만 —
                // 서버가 거절했으면 캐릭터는 그대로인데 화면만 돌아가 버린다.
                if (arrived && faceYaw.HasValue)
                {
                    view?.FaceCameraYaw(faceYaw.Value);
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
        //
        // 매칭 화면과 같은 창으로 보여야 한다. 색을 따로 정하지 않고 빌려 온다.
        //   판        LobbyPanelSkin 의 9-slice 금테 남색 판 (목록 길이에 따라 세로로 늘어나도 테가 안 찌그러진다)
        //   글자      매칭 판의 큰 제목(흰색) · 작은 게임 이름(금색)을 복제한다
        //   줄 · 닫기  매칭 판의 [게임 시작](금색 글자) · [매칭 취소](흰 글자) 버튼을 복제한다
        // 둘 다 없으면(매칭 화면이 없는 씬) 예전 단색으로 그린다.
        //
        // ⚠ 줄 폭(620 → 480) 말고는 크기 · 자리를 바꾸지 않았다. 판 760 폭, 줄 480×68, 머리 150 · 발 120, 닫기 200×64.

        /// <summary>모양을 빌려 온 매칭 판. 바뀌면(씬이 바뀌어 새 판이 생기면) 다시 그린다.</summary>
        private MatchPanelPresenter styledFrom;

        private static MatchPanelPresenter FindStyle()
        {
            return CommonMatchingUI.Current != null
                ? CommonMatchingUI.Current.GetComponentInChildren<MatchPanelPresenter>(includeInactive: true)
                : null;
        }

        /// <summary>처음 열 때, 또는 빌려 올 매칭 판이 바뀌었을 때만 다시 그린다.</summary>
        private void EnsureBuilt()
        {
            MatchPanelPresenter style = FindStyle();
            if (root != null && styledFrom == style) return;

            if (root != null) Destroy(root);
            rows.Clear();
            Build(style);
        }

        private void Build(MatchPanelPresenter style)
        {
            styledFrom = style;

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

            LobbyPanelSkin skin = LobbyPanelSkin.Load();
            if (skin != null && skin.panelFrame != null)
            {
                Image frame = panelGo.GetComponent<Image>();
                frame.sprite = skin.panelFrame;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = skin.framePixelsPerUnitMultiplier;
                frame.color = Color.white;
            }

            titleText = MakeText(panel, "Title", style != null ? style.TitleTemplate : null, 40f, Color.white);
            ((RectTransform)titleText.transform).sizeDelta = new Vector2(PanelW - 60f, 56f);
            titleText.text = "어디로 갈까요";

            fromText = MakeText(panel, "From", style != null ? style.GameTitleTemplate : null, 22f, Muted);
            ((RectTransform)fromText.transform).sizeDelta = new Vector2(PanelW - 60f, 30f);
            fromText.fontSize = 22f;

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

            Button close = MakeButton(panel, "닫기", primary: false, CloseTint, Close, new Vector2(200f, 64f));
            closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, .5f);
            closeRect.pivot = new Vector2(.5f, .5f);

            Resize(0);
            root.SetActive(false);
        }

        private GameObject MakeRow(Transform parent, string name, string distance, Action onClick)
        {
            Button button = MakeButton(parent, name, primary: true, Row, onClick, new Vector2(RowW, RowH));

            // 줄 버튼 그림은 따로 둔다. 매칭 버튼은 양 끝에 큰 징이 있어 목록에선 무거웠다 —
            // 작게 줄인 징을 왼쪽에만 둔 그림(LobbyPanelSkin.rowButton)을 쓴다.
            LobbyPanelSkin skin = LobbyPanelSkin.Load();
            Image face = button.GetComponent<Image>();
            if (skin != null && skin.rowButton != null && face != null)
            {
                face.sprite = skin.rowButton;
                face.type = Image.Type.Sliced;

                // 그림 높이(94) ÷ 줄 높이(68). 양 끝 둥근 테가 가로 · 세로 같은 비율로 줄어든다.
                face.pixelsPerUnitMultiplier = skin.rowButton.rect.height / RowH;
            }

            // 작은 둥근 징 하나를 왼쪽에만 단다. 정사각형 칸이라 늘 동그랗다.
            if (skin != null && skin.rowStud != null)
            {
                GameObject stud = MakeImage(button.transform, "Stud", Color.white);
                Image studImage = stud.GetComponent<Image>();
                studImage.sprite = skin.rowStud;
                studImage.preserveAspect = true;
                studImage.raycastTarget = false;
                var studRect = (RectTransform)stud.transform;
                studRect.anchorMin = studRect.anchorMax = new Vector2(.5f, .5f);
                studRect.sizeDelta = new Vector2(StudSize, StudSize);
                studRect.anchoredPosition = new Vector2(-RowW * .5f + StudFromLeft, 0f);
            }

            // 버튼 글자를 이정표 이름으로 쓰고 왼쪽에 둔다. 오른쪽엔 거리를 따로 적는다.
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, .5f);
                labelRect.pivot = new Vector2(.5f, .5f);
                // 왼쪽 끝에 작은 징이 있다(줄 왼쪽에서 49~63). 글자는 그 오른쪽 72 에서 시작한다.
                const float nameInset = 72f;
                float nameWidth = RowW - 220f;
                labelRect.sizeDelta = new Vector2(nameWidth, RowH);
                labelRect.anchoredPosition = new Vector2(-RowW * .5f + nameInset + nameWidth * .5f, 0f);
                label.alignment = TextAlignmentOptions.Left;
                label.fontSize = 26f;
                label.text = name;
            }

            TMP_Text far = MakeText(button.transform, "Distance", label, 22f, Muted);
            far.color = Muted;
            far.fontSize = 22f;
            var farRect = (RectTransform)far.transform;
            farRect.anchorMin = farRect.anchorMax = new Vector2(.5f, .5f);
            farRect.pivot = new Vector2(.5f, .5f);
            farRect.sizeDelta = new Vector2(120f, RowH);
            farRect.anchoredPosition = new Vector2(RowW * 0.5f - 116f, 0f);   // 오른쪽 마개 안쪽
            far.alignment = TextAlignmentOptions.Right;
            far.text = distance;

            return button.gameObject;
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

        /// <summary>
        /// 글자를 만든다. <paramref name="template"/> 이 있으면 복제해 글꼴 · 색 · 외곽선을 그대로 쓴다.
        /// </summary>
        private static TMP_Text MakeText(Transform parent, string name, TMP_Text template, float size, Color colour)
        {
            TMP_Text text;
            if (template != null)
            {
                text = Instantiate(template, parent, worldPositionStays: false);
                text.gameObject.SetActive(true);
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
                text.rectTransform.pivot = new Vector2(.5f, .5f);
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, worldPositionStays: false);
                text = go.AddComponent<TextMeshProUGUI>();
                text.fontSize = size;
                text.color = colour;
            }

            text.name = name;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>
        /// 버튼을 만든다. 매칭 판의 버튼을 복제하므로 그림 · 눌림 색 · 글자 모양이 같다.
        ///   <paramref name="primary"/> 참   [게임 시작] 모양 (금색 글자) — 목록 줄
        ///   <paramref name="primary"/> 거짓 [매칭 취소] 모양 (흰 글자)   — 닫기
        /// </summary>
        private Button MakeButton(Transform parent, string label, bool primary, Color fallback,
            Action onClick, Vector2 size)
        {
            Button template = styledFrom == null ? null
                : primary ? styledFrom.PrimaryButtonTemplate : styledFrom.SecondaryButtonTemplate;

            Button button;
            TMP_Text text;
            if (template != null)
            {
                button = Instantiate(template, parent, worldPositionStays: false);
                button.gameObject.SetActive(true);   // 서버 판에서는 [게임 시작] 이 꺼져 있다
                button.interactable = true;
                // 복제본이 원본의 클릭 연결을 물고 오지 않게 통째로 새로 만든다.
                button.onClick = new Button.ButtonClickedEvent();
                text = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
            }
            else
            {
                GameObject go = MakeImage(parent, "Button", fallback);
                button = go.AddComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                text = MakeText(go.transform, "Label", null, 28f, Color.white);
                Stretch(text.rectTransform);
            }

            button.name = $"Button {label}";
            ((RectTransform)button.transform).sizeDelta = size;
            if (text != null) text.text = label;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }
    }
}

using System.Collections;
using TMPro;
using UnderTheSea.Account;
using UnderTheSea.Network;
using UnderTheSea.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **캐릭터를 막 만든 사람이 로비에 처음 들어왔을 때 트는 오프닝 영상.** 한 번만 나온다.
    ///
    /// <code>
    ///   캐릭터 생성 → 채널 선택 → 로비 입장 → 자리 잡히면 오프닝 영상 → 로비 튜토리얼
    ///   이미 있던 캐릭터로 다시 로그인     틀지 않는다
    /// </code>
    ///
    /// <b>누구에게 틀지는 튜토리얼과 같은 방식이다.</b> 캐릭터를 만든 그 순간에만 "봐야 한다" 표시를 세운다
    /// (<see cref="MarkPending"/>, <c>CharacterCustomizationPersistence</c> 가 부른다). 그래서 예전 캐릭터 ·
    /// 다른 PC 에서 처음 로그인한 캐릭터에게는 나오지 않는다. 표시는 <b>이 PC 에만</b> 남는다(<c>LobbyTutorial</c> 과 같은 한계).
    ///
    /// <b>영상은 <c>Assets/Game/Resources/OpeningVideo.mp4</c></b> 다. 같은 이름으로 바꿔 넣으면 그대로 튼다.
    /// 파일이 없으면 아무것도 막지 않고 지나간다(튜토리얼은 바로 뜬다).
    ///
    /// <b>영상이 나오는 동안</b>: 이동 · 상호작용을 막고(<see cref="ChatFocus"/>) 게임 소리를 잠깐 끈다 — 제단 완성 영상
    /// (<see cref="AltarCompletionVideo"/>)과 같다. <b>길게 눌러 건너뛴다</b> — 키보드 Esc · Enter, 완드는 아무 버튼. Space 는 점프라 넣지 않았다(떼기 전에 영상이 끝나면 로비에서 뛴다).
    /// 안내 문구는 지금 쓰는 쪽(키보드 · 완드)에 맞춰 바뀐다. 영상 도중 완드를 꽂거나 빼도 따라간다.
    /// 한 번 눌러 넘어가지 않는 것은 처음 보는 사람이 버튼을 만지다 실수로 넘기지 않게 하려는 것이다.
    ///
    /// UI 는 코드로 만든다. 프리팹이 없어도 되고, 영상만 바꿔 끼우면 된다.
    /// </summary>
    public sealed class OpeningVideo : MonoBehaviour
    {
        /// <summary>Resources 안의 영상 경로. 확장자는 붙이지 않는다.</summary>
        private const string ClipPath = "OpeningVideo";

        /// <summary>
        /// **아직 오프닝을 봐야 하는 캐릭터**를 기억하는 PlayerPrefs 키의 앞머리. 뒤에 캐릭터 id 가 붙는다.
        /// "봤다" 가 아니라 "봐야 한다" 를 적는 이유는 <c>LobbyTutorial.PendingKeyPrefix</c> 와 같다.
        /// </summary>
        public const string PendingKeyPrefix = "OpeningVideoPending_";

        /// <summary>캐릭터 없이도 틀라는 개발용 표시. 에디터 · 개발 빌드에서만 먹는다. (LobbyTutorial.ForceKey 와 같은 쓰임)</summary>
        private const string ForceKey = "OpeningVideoForce";

        /// <summary>검은 화면으로 들어가고 나오는 시간 (초)</summary>
        private const float FadeSeconds = 0.5f;

        /// <summary>로딩 화면이 걷힌 뒤 이만큼 자리 잡혀 있어야 튼다 (초)</summary>
        private const float SettleSeconds = 1f;

        /// <summary>영상 준비를 기다리는 한도 (초). 넘기면 틀지 않고 조작을 돌려준다.</summary>
        private const float PrepareTimeout = 6f;

        /// <summary>건너뛰려면 이만큼 누르고 있어야 한다 (초)</summary>
        private const float SkipHoldSeconds = 1f;

        private const string KeyboardSkipText = "Esc 또는 Enter 를 길게 눌러 건너뛰기";
        private const string WandSkipText = "완드 버튼을 아무거나 길게 눌러 건너뛰기";

        /// <summary>지금 영상을 트는 중인가.</summary>
        public static bool Playing { get; private set; }

        /// <summary>
        /// **곧 틀 것이 있거나 트는 중인가.** 로비 튜토리얼과 제단 완성 영상이 이것을 보고 기다린다.
        ///
        /// 로비에 들어와 자리 잡을 때까지(<see cref="SettleSeconds"/>) 틈이 있어서, 트는 중만 보면
        /// 그 틈에 튜토리얼이 먼저 떠 버린다. 그래서 "봐야 하는 캐릭터" 인 동안 참이다.
        /// 영상 파일이 없으면 기다리게 하지 않는다.
        /// </summary>
        public static bool Blocking => Playing || (instance != null && instance.clip != null && Pending);

        private static OpeningVideo instance;
        private static readonly object InputLock = new object();

        private VideoClip clip;
        private VideoPlayer player;
        private Canvas canvas;
        private CanvasGroup group;
        private Image skipFill;
        private TextMeshProUGUI skipLabel;
        private RenderTexture target;
        private float readySince = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 서버에는 만들지 않는다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(OpeningVideo));
            DontDestroyOnLoad(host);
            host.AddComponent<OpeningVideo>();
        }

        // ------------------------------------------------------------
        // 누구에게 틀지 — 나중에 서버로 옮길 때 갈아끼울 곳은 셋뿐이다
        //   MarkPending (생성할 때 세운다) · Pending (틀지 묻는다) · MarkDone (틀면 지운다)
        // ------------------------------------------------------------

        /// <summary>**캐릭터를 막 만들었다.** 그 캐릭터가 로비에 처음 들어갈 때 오프닝을 튼다.</summary>
        public static void MarkPending(long characterId)
        {
            if (characterId == 0L)
            {
                return;
            }

            PlayerPrefs.SetInt(PendingKeyPrefix + characterId, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 지금 캐릭터가 다음 로비 입장에서 오프닝을 다시 보게 한다. 에디터 메뉴가 쓴다.
        /// 고른 캐릭터가 없으면(로비 씬만 Play) 개발용으로 한 번 틀게 한다.
        /// </summary>
        public static void ClearSeen()
        {
            long id = CurrentCharacterId;

            if (id == 0L)
            {
                PlayerPrefs.SetInt(ForceKey, 1);
                PlayerPrefs.Save();
                return;
            }

            MarkPending(id);
        }

        private static long CurrentCharacterId
        {
            get
            {
                CharacterDto character = AccountServiceLocator.Characters?.CurrentCharacter;
                return character != null ? character.id : 0L;
            }
        }

        private static bool Forced
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return PlayerPrefs.GetInt(ForceKey, 0) != 0;
#else
                return false;
#endif
            }
        }

        private static bool Pending
        {
            get
            {
                if (Forced)
                {
                    return true;
                }

                long id = CurrentCharacterId;
                return id != 0L && PlayerPrefs.GetInt(PendingKeyPrefix + id, 0) != 0;
            }
        }

        private static void MarkDone()
        {
            PlayerPrefs.DeleteKey(ForceKey);

            long id = CurrentCharacterId;
            if (id != 0L)
            {
                PlayerPrefs.DeleteKey(PendingKeyPrefix + id);
            }

            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------
        // 틀기
        // ------------------------------------------------------------

        private void Awake()
        {
            instance = this;
            clip = Resources.Load<VideoClip>(ClipPath);

            if (clip == null)
            {
                Debug.Log($"[오프닝 영상] Resources/{ClipPath} 가 아직 없어 오프닝을 틀지 않습니다. 파일을 넣으면 그대로 틉니다.");
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this)) instance = null;
            if (target != null) target.Release();
        }

        private void Update()
        {
            if (Playing || clip == null)
            {
                return;
            }

            // ① 로비가 자리 잡혔는가 — 로딩 화면이 걷히고, 미니게임이 아니고, 내 캐릭터가 있고, 잠깐 그대로였다.
            bool ready = TransitionStatus.IsReady && !MiniGameTransition.InMiniGame && LocalPlayer.Exists
                         && AltarCompletionVideo.InLobby();

            if (!ready)
            {
                readySince = -1f;
                return;
            }

            if (readySince < 0f) readySince = Time.unscaledTime;
            if (Time.unscaledTime - readySince < SettleSeconds) return;

            // ② 이 캐릭터가 아직 봐야 하는가.
            if (!Pending) return;

            // ③ 다른 연출과 겹치지 않게.
            if (ScreenFade.Busy || AltarCompletionVideo.Playing || AltarOfferCinematic.Playing) return;

            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            Playing = true;
            float listenerVolume = AudioListener.volume;

            // 먼저 적는다 — 영상 도중 튕겨 다시 들어와도 또 틀지 않는다.
            MarkDone();
            ChatFocus.Begin(InputLock);

            try
            {
                EnsureView();
                ClearTarget();
                SetSkipProgress(0f);
                RefreshSkipLabel();
                player.Prepare();

                // 검은 화면으로 들어간다. 그동안 영상이 준비된다.
                canvas.enabled = true;
                yield return Fade(0f, 1f);

                float waited = 0f;
                while (!player.isPrepared && waited < PrepareTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!player.isPrepared)
                {
                    Debug.LogWarning("[오프닝 영상] 영상을 준비하지 못해 건너뜁니다.");
                    yield return Fade(1f, 0f);
                    yield break;
                }

                AudioListener.volume = 0f;
                player.Play();
                Debug.Log("[오프닝 영상] 틉니다.");

                // 끝날 때까지, 또는 길게 눌러 건너뛸 때까지. 길이보다 조금 넉넉히 기다리고 넘기면 끊는다(멈춘 채 갇히지 않게).
                double limit = (player.length > 0 ? player.length : 30) + 3;
                float elapsed = 0f;
                float held = 0f;
                bool started = false;

                while (elapsed < limit)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (player.isPlaying) started = true;
                    else if (started) break;

                    RefreshSkipLabel();
                    held = SkipHeld() ? held + Time.unscaledDeltaTime : 0f;
                    SetSkipProgress(held / SkipHoldSeconds);

                    if (held >= SkipHoldSeconds)
                    {
                        Debug.Log("[오프닝 영상] 건너뛰었습니다.");
                        break;
                    }

                    yield return null;
                }

                yield return Fade(1f, 0f);
            }
            finally
            {
                // 무슨 일이 있어도 소리 · 조작 · 화면을 돌려준다.
                if (player != null) player.Stop();
                if (canvas != null) canvas.enabled = false;
                AudioListener.volume = listenerVolume;
                ChatFocus.End(InputLock);
                Playing = false;
            }
        }

        /// <summary>
        /// 건너뛰기 버튼을 누르고 있는가. 창에 포커스가 있을 때만 본다.
        ///
        /// 완드는 **눌림 순간이 아니라 누르고 있는 상태**를 본다. 순간(Consume) 은 로비의 완드 상호작용이
        /// 매 프레임 가져가서, 여기서 가져가려 하면 둘 중 하나가 놓친다. 상태는 여러 곳이 읽어도 안전하다.
        /// </summary>
        private static bool SkipHeld()
        {
            if (!Application.isFocused)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && (keyboard.escapeKey.isPressed || keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
            {
                return true;
            }

            IotPlayerController wand = IotPlayerController.Persistent;
            if (!IotPlayerController.IsWandLive(wand))
            {
                return false;
            }

            return wand.Left.Button1 || wand.Left.Button2 || wand.Right.Button1 || wand.Right.Button2;
        }

        /// <summary>
        /// 완드가 살아 있으면 완드 안내, 아니면 키보드 안내. 로비 튜토리얼의 안내 전환과 같은 기준이다.
        ///
        /// ⚠ <see cref="IotPlayerController.IsWandLive"/> 로 거른다. 완드가 없을 때 컨트롤러는 키보드로 대신 채우는데
        ///   (키보드 폴백), 그것까지 완드로 치면 키보드로 하는 사람에게 완드 안내가 뜬다.
        /// </summary>
        private void RefreshSkipLabel()
        {
            if (skipLabel == null) return;

            string wanted = IotPlayerController.IsWandLive(IotPlayerController.Persistent) ? WandSkipText : KeyboardSkipText;
            if (skipLabel.text != wanted) skipLabel.text = wanted;
        }

        private void SetSkipProgress(float amount)
        {
            if (skipFill != null) skipFill.fillAmount = Mathf.Clamp01(amount);
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }

            group.alpha = to;
        }

        /// <summary>이전 재생의 마지막 장면이 한 프레임 보이지 않게 검게 지운다.</summary>
        private void ClearTarget()
        {
            if (target == null) return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        // ------------------------------------------------------------
        // 화면 — 처음 틀 때 한 번 만든다
        // ------------------------------------------------------------

        /// <summary>
        /// 검은 바탕 · 영상 · 건너뛰기 안내. 제단 완성 영상(<c>AltarCompletionVideoSetup</c>)과 같은 짜임이다.
        /// 튜토리얼(short.MaxValue - 1)과 전환 암전(short.MaxValue)보다는 아래에 둔다.
        /// </summary>
        private void EnsureView()
        {
            if (canvas != null)
            {
                return;
            }

            var root = new GameObject("View",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, worldPositionStays: false);

            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32400;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            // 영상이 나오는 동안 아래 화면의 클릭을 먹는다.
            Image black = Stretch("Black", root.transform).gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = true;

            int w = Mathf.Max(16, (int)clip.width);
            int h = Mathf.Max(16, (int)clip.height);
            target = new RenderTexture(w, h, 0);
            target.Create();

            RectTransform screenRect = Stretch("Screen", root.transform);
            RawImage screen = screenRect.gameObject.AddComponent<RawImage>();
            screen.texture = target;
            screen.raycastTarget = false;
            AspectRatioFitter fitter = screenRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = w / (float)h;

            BuildSkipHint(root.transform);

            player = gameObject.AddComponent<VideoPlayer>();
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.Direct;

            canvas.enabled = false;
        }

        /// <summary>오른쪽 아래 "길게 눌러 건너뛰기" 와, 누르는 동안 차오르는 막대.</summary>
        private void BuildSkipHint(Transform parent)
        {
            var hint = new GameObject("SkipHint", typeof(RectTransform));
            hint.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)hint.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 40f);
            rect.sizeDelta = new Vector2(620f, 56f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rect, worldPositionStays: false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = new Vector2(0f, 0.3f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = KeyboardSkipText;
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Right;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            skipLabel = label;

            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(rect, worldPositionStays: false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0.55f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(1f, 0f);
            trackRect.sizeDelta = new Vector2(0f, 6f);
            Image trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.25f);
            trackImage.raycastTarget = false;

            RectTransform fillRect = Stretch("Fill", trackRect);
            skipFill = fillRect.gameObject.AddComponent<Image>();
            // 스프라이트가 없는 Image 는 Filled 가 먹지 않는다. 흰 텍스처를 한 장 끼운다.
            skipFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Horizontal;
            skipFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            skipFill.fillAmount = 0f;
            skipFill.color = Color.white;
            skipFill.raycastTarget = false;
        }

        private static RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}

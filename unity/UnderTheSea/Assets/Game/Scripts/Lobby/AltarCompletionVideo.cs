using System.Collections;
using FishingMiniGame.Runtime;
using UnderTheSea.Network;
using UnderTheSea.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **섬 회복도가 100% 가 된 순간의 완성 영상.** 로비에 있는 모두에게 한 번씩 튼다.
    ///
    /// <code>
    ///   그 순간 로비에 있음        곧바로 튼다 (낚시 중이면 낚시를 그만두게 하고)
    ///   그 순간 미니게임에 있음     게임은 그대로. 로비로 돌아와 로딩이 끝나고 화면이 자리 잡으면 튼다
    ///   그 뒤에 접속함             로비에 처음 들어와 자리 잡으면 튼다
    ///   마지막 조각을 바친 사람     공중 카메라 연출 대신 이 영상 (AltarOfferingRelay)
    /// </code>
    ///
    /// <b>"언제 틀지" 는 이벤트가 아니라 상태로 정한다.</b> 서버가 목표에 처음 닿은 시각(<see cref="AltarState.ActivatedAt"/>)을
    /// "이번 완성" 의 번호로 준다. 이 PC 가 그 번호를 본 적이 없고, 로비가 자리 잡혀 있으면 튼다 — 그래서 미니게임에서
    /// 돌아온 사람도, 늦게 접속한 사람도 같은 한 길로 본다. 본 번호는 계정(닉네임)마다 PlayerPrefs 에 적어 둔다.
    /// 목표 아래로 내려갔다가 다시 차면 서버가 새 시각을 주므로 다시 튼다.
    ///
    /// 로비에 있는 사람이 30초 주기 조회를 기다리지 않는 것은 봉헌 성공 알림 덕이다 — 알림을 받은 모두가 곧바로
    /// 제단 상태를 다시 묻는다(<see cref="AltarOfferingRelay"/>). 개발자 모드의 회복도 조정도 같은 알림을 쓴다.
    ///
    /// <b>영상이 나오는 동안</b>: 이동 · 상호작용을 막는다(<see cref="ChatFocus"/>, 키보드 · 완드 모두).
    /// 게임 소리는 잠깐 끈다(<c>AudioListener.volume</c>, 저장되지 않음). 영상 소리는 게임 오디오를 거치지 않고
    /// 바로 나간다(<see cref="VideoAudioOutputMode.Direct"/>).
    ///
    /// 프리팹: <c>Resources/AltarCompletionVideo.prefab</c> — <c>Tools/아라아띠/제단 완성 영상 설치</c> 로 만든다.
    /// </summary>
    public sealed class AltarCompletionVideo : MonoBehaviour
    {
        private const string PrefabPath = "AltarCompletionVideo";
        private const string SeenKeyPrefix = "AltarCompletionVideoSeen.";

        [Header("연결 (설치 메뉴가 채운다)")]
        [SerializeField] private VideoPlayer player;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RawImage screen;

        [Header("시간 (초)")]
        [Tooltip("검은 화면으로 들어가고 나오는 시간.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

        [Tooltip("로딩 화면이 걷힌 뒤 이만큼 자리 잡혀 있어야 튼다. 미니게임에서 돌아오자마자 덮치지 않게.")]
        [SerializeField, Min(0f)] private float settleSeconds = 1.5f;

        [Tooltip("영상 준비를 기다리는 한도. 넘기면 틀지 않고 조작을 돌려준다.")]
        [SerializeField, Min(1f)] private float prepareTimeout = 6f;

        /// <summary>지금 영상을 트는 중인가.</summary>
        public static bool Playing { get; private set; }

        private static AltarCompletionVideo instance;
        private static readonly object InputLock = new object();

        private RenderTexture target;
        private float readySince = -1f;
        private bool inLobby;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 화면이 없는 서버에는 만들지 않는다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || instance != null)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[완성 영상] Resources/{PrefabPath} 가 없어 완성 영상을 틀지 않습니다.");
                return;
            }

            GameObject root = Instantiate(prefab);
            root.name = prefab.name;
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            instance = this;

            if (canvas != null) canvas.enabled = false;
            if (player == null) return;

            player.playOnAwake = false;
            player.isLooping = false;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;

            int w = player.clip != null ? (int)player.clip.width : 1920;
            int h = player.clip != null ? (int)player.clip.height : 1080;
            target = new RenderTexture(Mathf.Max(16, w), Mathf.Max(16, h), 0);
            target.Create();
            player.targetTexture = target;
            if (screen != null) screen.texture = target;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            inLobby = InLobby();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this)) instance = null;
            if (target != null) target.Release();
        }

        private void OnSceneChanged(Scene scene, LoadSceneMode mode) => inLobby = InLobby();
        private void OnSceneUnloaded(Scene scene) => inLobby = InLobby();

        private void Update()
        {
            if (Playing || player == null)
            {
                return;
            }

            // ① 로비가 자리 잡혔는가 — 로딩 화면이 걷히고, 미니게임이 아니고, 내 캐릭터가 있고, 잠깐 그대로였다.
            bool ready = inLobby && TransitionStatus.IsReady && !MiniGameTransition.InMiniGame && LocalPlayer.Exists;
            if (!ready)
            {
                readySince = -1f;
                return;
            }

            if (readySince < 0f) readySince = Time.unscaledTime;
            if (Time.unscaledTime - readySince < settleSeconds) return;

            // ② 아직 안 본 완성이 있는가.
            if (!AltarState.HasValue || !AltarState.AltarActivated) return;
            string completion = AltarState.ActivatedAt;
            if (string.IsNullOrEmpty(completion) || completion == Seen) return;

            // ③ 다른 연출과 겹치지 않게.
            if (ScreenFade.Busy || AltarOfferCinematic.Playing) return;

            StartCoroutine(PlayRoutine(completion));
        }

        /// <summary>
        /// 🛠 개발자 미리보기 — 완성과 무관하게 내 화면에서 한 번 튼다. 본 것으로 적지 않는다.
        /// </summary>
        public static void PlayPreview()
        {
            if (instance != null && !Playing && instance.player != null)
            {
                instance.StartCoroutine(instance.PlayRoutine(null));
            }
        }

        private IEnumerator PlayRoutine(string completion)
        {
            Playing = true;
            float listenerVolume = AudioListener.volume;

            // 먼저 적는다 — 영상 도중 튕겨 다시 들어와도 같은 완성을 또 틀지 않는다.
            if (completion != null) Seen = completion;

            StopOtherActivities();
            ChatFocus.Begin(InputLock);

            try
            {
                ClearTarget();
                player.Prepare();

                // 검은 화면으로 들어간다. 그동안 영상이 준비된다.
                canvas.enabled = true;
                yield return Fade(0f, 1f);

                float waited = 0f;
                while (!player.isPrepared && waited < prepareTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!player.isPrepared)
                {
                    Debug.LogWarning("[완성 영상] 영상을 준비하지 못해 건너뜁니다.");
                    yield return Fade(1f, 0f);
                    yield break;
                }

                AudioListener.volume = 0f;
                player.Play();
                Debug.Log($"[완성 영상] 틉니다 — {(completion != null ? $"완성 {completion}" : "미리보기")}");

                // 끝날 때까지. 길이보다 조금 넉넉히 기다리고 넘기면 끊는다(멈춘 채 갇히지 않게).
                double limit = (player.length > 0 ? player.length : 30) + 3;
                float elapsed = 0f;
                bool started = false;
                while (elapsed < limit)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (player.isPlaying) started = true;
                    else if (started) break;
                    yield return null;
                }

                yield return Fade(1f, 0f);
            }
            finally
            {
                // 무슨 일이 있어도 소리 · 조작 · 화면을 돌려준다.
                player.Stop();
                if (canvas != null) canvas.enabled = false;
                AudioListener.volume = listenerVolume;
                ChatFocus.End(InputLock);
                Playing = false;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null) yield break;

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, fadeSeconds <= 0f ? 1f : t / fadeSeconds);
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>낚시 중이면 그만두게 하고, 봉헌 창이 떠 있으면 닫는다. 영상 뒤에 남아 있으면 안 된다.</summary>
        private static void StopOtherActivities()
        {
            var fishing = FindFirstObjectByType<PlayerFishingAdapter>();
            if (fishing != null && fishing.AbortLocalFishing())
            {
                Debug.Log("[완성 영상] 낚시를 그만두게 했습니다.");
            }

            var panel = FindFirstObjectByType<AltarOfferingUIController>();
            if (panel != null && panel.IsOpen)
            {
                panel.Close();
            }
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

        /// <summary>이 계정이 마지막으로 본 완성 번호. 같은 PC 를 여러 계정이 쓰므로 닉네임마다 따로 적는다.</summary>
        private static string Seen
        {
            get => PlayerPrefs.GetString(SeenKeyPrefix + SceneFlow.Nickname, string.Empty);
            set
            {
                PlayerPrefs.SetString(SeenKeyPrefix + SceneFlow.Nickname, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 로비인가. ⚠ <see cref="SeaHeartCounterInstaller"/> 의 것과 같은 내용이다(그쪽이 private).
        /// </summary>
        private static bool InLobby()
        {
            Scene lobby = SceneManager.GetSceneByName(SceneFlow.Lobby);
            if (lobby.IsValid() && lobby.isLoaded) return true;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name == SceneFlow.Lobby || go.name == "[" + SceneFlow.Lobby + "]") return true;
                }
            }

            return false;
        }
    }
}

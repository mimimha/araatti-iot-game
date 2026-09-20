using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheSea.Audio
{
    /// <summary>
    /// 🔊 게임 전체의 소리를 한 곳에서. **씬을 넘어 살아남는 하나**다. (GAME_STRUCTURE.md 오디오 절)
    ///
    /// <code>
    ///   🎵 배경음악   PlayMusic(clip)  — 두 소스로 교차 페이드. 같은 곡이면 그대로 이어진다
    ///                StopMusic()      — 페이드 아웃
    ///   💥 효과음     PlayOneShot(clip, 세기)  — 2D. 어디서든 같게 들린다
    ///   🌊 루프       Loop("이름", clip) → 손잡이. Target(0~1) 만 정하면 켜지고 · 커지고 · 꺼진다
    ///   🔉 볼륨       MusicVolume · EffectsVolume · MasterVolume — PlayerPrefs 에 저장. 설정 화면이 바꾼다
    /// </code>
    ///
    /// <b>왜 하나인가.</b> 씬마다 AudioSource 를 두면 씬이 바뀔 때 음악이 끊긴다. 로비 → 미니게임 → 로비로
    /// 오가는 게임이라 음악이 씬을 따라 이어져야 한다. 그래서 처음 부르는 순간 스스로 생겨
    /// <c>DontDestroyOnLoad</c> 로 남는다. 씬에 미리 놓을 것이 없다 — <see cref="Instance"/> 만 부르면 된다.
    ///
    /// <b>씬 쪽에서는 이렇게 쓴다.</b>
    /// <code>
    ///   씬 배경음악        SceneMusic 컴포넌트를 씬에 하나 놓고 클립을 꽂는다 (Start 에서 PlayMusic)
    ///   미니게임 연출      게임 상태를 보는 스크립트(예: ShipCoopAudio)가 PlayMusic · PlayOneShot · Loop 를 부른다
    ///   UI 버튼 소리       AudioHub.Instance.PlayOneShot(clip)
    /// </code>
    ///
    /// <b>볼륨은 타이틀 설정과 같은 PlayerPrefs 키를 쓴다.</b> (StartMenuController · SettingsPanelView)
    /// 그쪽이 값을 바꾸면 static 이벤트로 알려주므로 그대로 받는다. 마스터는 Unity 의
    /// <c>AudioListener.volume</c> 그대로다 — 믹서를 새로 들이지 않았다. 켤 때 한 번 복원한다.
    /// 미니게임을 바로 실행한 빌드는 타이틀을 안 거쳐 아무도 복원하지 않기 때문이다.
    ///
    /// <b>서버에서는 아무것도 만들지 않는다.</b> 그래픽 장치가 없으면(Dedicated Server) 모든 호출이 조용히 빠진다.
    /// 부르는 쪽은 서버인지 신경 쓸 필요가 없다.
    /// </summary>
    public sealed class AudioHub : MonoBehaviour
    {
        // 타이틀 설정(StartMenuController)과 같은 키. 한쪽만 바꾸면 설정이 두 개가 된다.
        private const string MutePrefKey = "AraAtti.Audio.Muted";
        private const string MasterVolumePrefKey = "AraAtti.Audio.MasterVolume";
        private const string MusicVolumePrefKey = "AraAtti.Audio.MusicVolume";
        private const string EffectsVolumePrefKey = "AraAtti.Audio.EffectsVolume";

        private static AudioHub _instance;
        private static bool _quitting;

        /// <summary>하나뿐인 허브. 없으면 만든다. 앱이 끝나는 중이면 null.</summary>
        public static AudioHub Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    var go = new GameObject("AudioHub");
                    _instance = go.AddComponent<AudioHub>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        /// <summary>소리를 낼 수 있는 곳인가. 서버(그래픽 장치 없음)에서는 false.</summary>
        public bool CanHear { get; private set; }

        // 배경음악 — 두 소스로 교차 페이드.
        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _bgmFront;
        private float _bgmFade = 1f;
        private float _bgmFadeSeconds = 1.5f;
        private AudioClip _bgmWant;

        /// <summary>지금 틀고 있는(또는 올라오는) 곡. 없으면 null.</summary>
        public AudioClip CurrentMusic => _bgmWant;

        private AudioSource _sfx;

        /// <summary>
        /// 루프 하나의 손잡이. <see cref="Target"/> 을 0 ~ 1 로 정해 두면 허브가 페이드해서 켜고 끈다.
        /// </summary>
        public sealed class LoopHandle
        {
            internal AudioSource Source;
            internal float Now;
            internal float FadeSeconds = 0.6f;

            /// <summary>목표 크기 0 ~ 1. 0 이면 서서히 꺼진다.</summary>
            public float Target;

            /// <summary>클립을 바꾼다. 같은 클립이면 아무 일도 없다.</summary>
            public void SetClip(AudioClip clip)
            {
                if (Source == null || Source.clip == clip) return;
                bool wasPlaying = Source.isPlaying;
                Source.Stop();
                Source.clip = clip;
                if (wasPlaying && clip != null) Source.Play();
            }
        }

        private readonly Dictionary<string, LoopHandle> _loops = new Dictionary<string, LoopHandle>();

        private float _musicVolume = 1f;
        private float _effectsVolume = 1f;

        /// <summary>음악 볼륨 0 ~ 1. 바꾸면 저장된다.</summary>
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicVolumePrefKey, _musicVolume);
            }
        }

        /// <summary>효과음(루프 포함) 볼륨 0 ~ 1. 바꾸면 저장된다.</summary>
        public float EffectsVolume
        {
            get => _effectsVolume;
            set
            {
                _effectsVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(EffectsVolumePrefKey, _effectsVolume);
            }
        }

        /// <summary>마스터 볼륨 = AudioListener.volume. 바꾸면 저장된다.</summary>
        public float MasterVolume
        {
            get => AudioListener.volume;
            set
            {
                float v = Mathf.Clamp01(value);
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(MasterVolumePrefKey, v);
                PlayerPrefs.SetInt(MutePrefKey, v <= 0.001f ? 1 : 0);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            CanHear = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

            RestoreVolumes();

            if (!CanHear)
            {
                return;   // 서버. 소스를 만들지 않는다. 모든 호출이 조용히 빠진다
            }

            _bgmA = MakeSource("BGM A", loop: true);
            _bgmB = MakeSource("BGM B", loop: true);
            _bgmFront = _bgmA;
            _sfx = MakeSource("SFX", loop: false);
        }

        private void OnEnable()
        {
            StartMenuController.MusicVolumeChanged += OnMusicVolumeChanged;
            StartMenuController.EffectsVolumeChanged += OnEffectsVolumeChanged;
        }

        private void OnDisable()
        {
            StartMenuController.MusicVolumeChanged -= OnMusicVolumeChanged;
            StartMenuController.EffectsVolumeChanged -= OnEffectsVolumeChanged;
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
        }

        private void RestoreVolumes()
        {
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, 1f));
            _effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumePrefKey, 1f));

            bool muted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, 1f));
            AudioListener.volume = muted ? 0f : master;
        }

        private void OnMusicVolumeChanged(float v) => _musicVolume = Mathf.Clamp01(v);
        private void OnEffectsVolumeChanged(float v) => _effectsVolume = Mathf.Clamp01(v);

        private AudioSource MakeSource(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;   // 2D
            source.volume = 0f;
            return source;
        }

        // ------------------------------------------------------------
        // 🎵 배경음악
        // ------------------------------------------------------------

        /// <summary>
        /// 배경음악을 튼다. 이미 그 곡이면 그대로 이어진다. 다른 곡이면 <paramref name="fadeSeconds"/> 동안 교차한다.
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeSeconds = 1.5f)
        {
            if (!CanHear || clip == _bgmWant)
            {
                return;
            }

            _bgmWant = clip;
            _bgmFadeSeconds = Mathf.Max(fadeSeconds, 0.01f);

            AudioSource back = _bgmFront == _bgmA ? _bgmB : _bgmA;

            if (clip != null)
            {
                back.clip = clip;
                back.volume = 0f;
                back.Play();
            }

            _bgmFront = back;
            _bgmFade = 0f;
        }

        /// <summary>배경음악을 페이드 아웃한다.</summary>
        public void StopMusic(float fadeSeconds = 1.5f)
        {
            PlayMusic(null, fadeSeconds);
        }

        // ------------------------------------------------------------
        // 💥 효과음
        // ------------------------------------------------------------

        /// <summary>한 번 낸다. 2D. <paramref name="level"/> 에 효과음 볼륨이 곱해진다.</summary>
        public void PlayOneShot(AudioClip clip, float level = 1f)
        {
            if (!CanHear || clip == null || _sfx == null)
            {
                return;
            }

            _sfx.PlayOneShot(clip, Mathf.Clamp01(level) * _effectsVolume);
        }

        // ------------------------------------------------------------
        // 🌊 루프
        // ------------------------------------------------------------

        /// <summary>
        /// 이름으로 루프 손잡이를 얻는다. 없으면 만든다. 같은 이름은 같은 손잡이다 (씬이 바뀌어도).
        /// 부르는 쪽은 매 프레임 <see cref="LoopHandle.Target"/> 만 정한다.
        /// </summary>
        public LoopHandle Loop(string key, AudioClip clip, float fadeSeconds = 0.6f)
        {
            if (!_loops.TryGetValue(key, out LoopHandle handle))
            {
                handle = new LoopHandle { FadeSeconds = fadeSeconds };

                if (CanHear)
                {
                    handle.Source = MakeSource($"Loop {key}", loop: true);
                }

                _loops[key] = handle;
            }

            handle.FadeSeconds = fadeSeconds;
            handle.SetClip(clip);
            return handle;
        }

        /// <summary>모든 루프를 끈다. 미니게임을 떠날 때 부른다.</summary>
        public void StopAllLoops()
        {
            foreach (LoopHandle handle in _loops.Values)
            {
                handle.Target = 0f;
            }
        }

        // ------------------------------------------------------------

        private void Update()
        {
            if (!CanHear)
            {
                return;
            }

            DriveMusic();
            DriveLoops();
        }

        private void DriveMusic()
        {
            _bgmFade = Mathf.MoveTowards(_bgmFade, 1f, Time.unscaledDeltaTime / _bgmFadeSeconds);

            AudioSource other = _bgmFront == _bgmA ? _bgmB : _bgmA;

            _bgmFront.volume = (_bgmFront.clip != null && _bgmWant != null ? _bgmFade : 0f) * _musicVolume;
            other.volume = (1f - _bgmFade) * _musicVolume;

            if (other.isPlaying && _bgmFade >= 1f)
            {
                other.Stop();
            }

            if (_bgmWant == null && _bgmFront.isPlaying && _bgmFade >= 1f)
            {
                _bgmFront.Stop();
            }
        }

        private void DriveLoops()
        {
            foreach (LoopHandle loop in _loops.Values)
            {
                if (loop.Source == null || loop.Source.clip == null)
                {
                    continue;
                }

                loop.Now = Mathf.MoveTowards(loop.Now, Mathf.Clamp01(loop.Target), Time.unscaledDeltaTime / Mathf.Max(loop.FadeSeconds, 0.01f));
                loop.Source.volume = loop.Now * _effectsVolume;

                if (loop.Now > 0.001f && !loop.Source.isPlaying)
                {
                    loop.Source.Play();
                }
                else if (loop.Now <= 0.001f && loop.Source.isPlaying)
                {
                    loop.Source.Stop();
                }
            }
        }
    }
}

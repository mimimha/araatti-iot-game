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

        /// <summary>효과음 · 곡 요청을 로그로 찍는다. 소리가 안 들릴 때 켠다.</summary>
        public bool logPlays = false;

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
            internal float NextPlayAt;

            /// <summary>목표 크기 0 ~ 1. 0 이면 서서히 꺼진다.</summary>
            public float Target;

            /// <summary>
            /// 이 간격(초)마다 클립을 다시 튼다. 0 이면 이음새 없는 루프(AudioSource.loop).
            /// 짧은 소리(삐걱 · 두드림)를 루프로 쓰면 너무 빠르게 반복되니, 클립 길이보다 길게 줘서 사이를 띈다.
            /// </summary>
            public float RepeatEvery;

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

        // ------------------------------------------------------------
        // 🔉 크기 맞추기 — 출처가 다른 클립은 원본 크기가 제멋대로다.
        //    클립의 실제 크기(RMS)를 한 번 재서 목표 크기로 맞추는 배율을 기억해 둔다.
        //    부르는 쪽의 level 은 "그 소리가 남들보다 얼마나 커야 하나" 만 정하면 된다.
        // ------------------------------------------------------------

        /// <summary>맞출 목표 크기 (RMS). 0.1 ≈ −20 dBFS. 효과음이 서로 같은 크기로 들린다.</summary>
        private const float TargetRms = 0.1f;

        private readonly Dictionary<AudioClip, float> _gains = new Dictionary<AudioClip, float>();

        /// <summary>
        /// 클립 앞의 빈 구간 (초). 효과음은 여기서부터 튼다.
        ///
        /// ⚠ 팩에서 받은 효과음은 앞에 0.1 ~ 0.3초 무음이 붙은 것이 많다. 그대로 틀면 "뚜껑이 닫히기 시작한 뒤
        ///    한참 있다 소리가 난다" 처럼 늦게 들린다. 크기를 잴 때 함께 재서, 첫 소리가 나는 지점부터 튼다.
        /// </summary>
        private readonly Dictionary<AudioClip, float> _leadIns = new Dictionary<AudioClip, float>();

        /// <summary>이 크기(절대값)부터 "소리가 시작됐다" 로 본다.</summary>
        private const float LeadInThreshold = 0.02f;

        /// <summary>앞 무음을 건너뛰어 틀 때 쓰는 소스들. PlayOneShot 은 시작 지점을 못 정해서 따로 둔다.</summary>
        private readonly List<AudioSource> _trimmed = new List<AudioSource>();
        private int _trimmedNext;
        private const int TrimmedVoices = 6;

        /// <summary>이 클립의 앞 빈 구간 (초). 크기 측정과 같이 잰다.</summary>
        public float LeadInFor(AudioClip clip)
        {
            if (clip == null) return 0f;
            GainFor(clip);
            return _leadIns.TryGetValue(clip, out float lead) ? lead : 0f;
        }

        /// <summary>
        /// 이 클립을 목표 크기로 맞추는 배율. 처음 한 번 재고 기억한다.
        /// 데이터를 못 읽는 클립(스트리밍 · 압축 유지)은 1.
        /// </summary>
        public float GainFor(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (_gains.TryGetValue(clip, out float gain)) return gain;

            gain = 1f;

            try
            {
                int count = clip.samples * clip.channels;

                if (count > 0 && clip.loadType == AudioClipLoadType.DecompressOnLoad)
                {
                    if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();

                    var data = new float[count];

                    if (clip.GetData(data, 0))
                    {
                        // 앞 무음 — 첫 소리가 나는 표본까지. 채널이 섞여 있으니 표본 수를 채널 수로 나눈다.
                        int firstLoud = count;
                        for (int i = 0; i < count; i++)
                        {
                            if (Mathf.Abs(data[i]) >= LeadInThreshold) { firstLoud = i; break; }
                        }

                        float leadIn = firstLoud >= count ? 0f : (float)(firstLoud / clip.channels) / clip.frequency;
                        _leadIns[clip] = Mathf.Max(0f, leadIn - 0.005f);   // 첫 소리 직전 5ms 는 남긴다 (딱 끊기지 않게)

                        // 조용한 앞뒤를 빼려고 큰 순서 상위 30% 표본의 RMS 를 쓴다 — 짧은 충격음도 공정하게.
                        double sum = 0;
                        int used = 0;
                        int step = Mathf.Max(1, count / 200000);   // 긴 클립은 건너뛰며 잰다
                        var peaks = new List<float>(count / step + 1);

                        for (int i = 0; i < count; i += step) peaks.Add(Mathf.Abs(data[i]));
                        peaks.Sort();

                        int from = (int)(peaks.Count * 0.7f);
                        for (int i = from; i < peaks.Count; i++) { sum += peaks[i] * peaks[i]; used++; }

                        float rms = used > 0 ? Mathf.Sqrt((float)(sum / used)) : 0f;

                        if (rms > 1e-4f)
                        {
                            gain = Mathf.Clamp(TargetRms / rms, 0.15f, 8f);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioHub] {clip.name} 크기를 못 재서 그대로 튼다: {e.Message}");
            }

            _gains[clip] = gain;
            return gain;
        }

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

            // ⚠ 실행 인자 -mute: 이 실행만 무음. 저장은 안 한다.
            //    한 PC 에 클라를 두 개 띄워 테스트하면 남의 소리(물 뜨기 · 뚜껑 · 대포)가 두 창에서 같은 순간 나서
            //    두 겹으로 들린다. 실제 4대 플레이에선 없는 일이지만 테스트가 헷갈려서, 둘째 클라는 이걸로 띄운다.
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "-mute", StringComparison.OrdinalIgnoreCase))
                {
                    AudioListener.volume = 0f;
                    Debug.Log("[AudioHub] -mute — 이 실행은 무음");
                    break;
                }
            }

            if (!CanHear)
            {
                return;   // 서버. 소스를 만들지 않는다. 모든 호출이 조용히 빠진다
            }

            _bgmA = MakeSource("BGM A", loop: true);
            _bgmB = MakeSource("BGM B", loop: true);
            _bgmFront = _bgmA;
            _sfx = MakeSource("SFX", loop: false);

            // ⚠ 효과음 소스는 볼륨 1 이어야 한다. MakeSource 는 페이드용으로 0 에서 시작하는데,
            //    PlayOneShot 의 세기는 **소스 볼륨에 곱해진다.** 0 인 채로 두었더니 요청은 다 찍히는데
            //    아무 소리도 안 났다. 세기는 PlayOneShot 인자로만 준다.
            _sfx.volume = 1f;

            AudioConfiguration config = AudioSettings.GetConfiguration();
            Debug.Log(
                $"[AudioHub] 켰다. 리스너 볼륨 {AudioListener.volume:F2} · 음악 {_musicVolume:F2} · 효과음 {_effectsVolume:F2} · " +
                $"출력 {config.speakerMode} {config.sampleRate}Hz · 드라이버 {AudioSettings.driverCapabilities} · 일시정지 {AudioListener.pause}");
        }

        /// <summary>
        /// 지금 소리가 날 수 있는 상태인지 한 번 찍는다. 리스너가 0개거나 다 꺼져 있으면 아무 소리도 안 난다.
        /// 소리가 안 들릴 때 부른다 (ShipCoopAudio.Start).
        /// </summary>
        public void LogState(string who)
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int on = 0;
            var names = new System.Text.StringBuilder();

            foreach (AudioListener l in listeners)
            {
                bool active = l.enabled && l.gameObject.activeInHierarchy;
                if (active) on++;
                names.Append(l.gameObject.name).Append(active ? "(켜짐) " : "(꺼짐) ");
            }

            Debug.Log(
                $"[AudioHub] {who} — 들을 수 있나 {CanHear} · 리스너 {listeners.Length}개 중 켜진 것 {on}개: {names}" +
                $"· 리스너 볼륨 {AudioListener.volume:F2} · 일시정지 {AudioListener.pause} · 루프 {_loops.Count}개 · 곡 {(_bgmWant != null ? _bgmWant.name : "없음")}");
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

            if (logPlays)
            {
                Debug.Log($"[AudioHub] 곡 → {(clip != null ? clip.name : "없음")} ({fadeSeconds:F1}초 교차)");
            }
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

            float gain = GainFor(clip);
            float volume = Mathf.Clamp(level, 0f, 2f) * gain * _effectsVolume;
            float leadIn = LeadInFor(clip);

            if (leadIn > 0.01f)
            {
                // 앞 무음을 건너뛴다. PlayOneShot 은 시작 지점을 못 정하니 전용 소스를 돌려 쓴다.
                AudioSource voice = TrimmedVoice();
                voice.clip = clip;
                voice.volume = Mathf.Min(1f, volume);
                voice.time = Mathf.Min(leadIn, Mathf.Max(0f, clip.length - 0.01f));
                voice.Play();
            }
            else
            {
                _sfx.PlayOneShot(clip, volume);
            }

            if (logPlays)
            {
                Debug.Log($"[AudioHub] 효과음 {clip.name} × {volume:F2} (세기 {level:F2} · 크기보정 {gain:F2})");
            }
        }

        /// <summary>앞 무음을 건너뛰어 틀 소스. 여섯 개를 돌려 쓴다 — 짧은 소리가 겹쳐도 끊기지 않게.</summary>
        private AudioSource TrimmedVoice()
        {
            if (_trimmed.Count < TrimmedVoices)
            {
                AudioSource made = MakeSource($"SFX {_trimmed.Count + 1}", loop: false);
                made.volume = 1f;
                _trimmed.Add(made);
                return made;
            }

            AudioSource voice = _trimmed[_trimmedNext];
            _trimmedNext = (_trimmedNext + 1) % _trimmed.Count;
            return voice;
        }

        // ------------------------------------------------------------
        // 🌊 루프
        // ------------------------------------------------------------

        /// <summary>
        /// 이름으로 루프 손잡이를 얻는다. 없으면 만든다. 같은 이름은 같은 손잡이다 (씬이 바뀌어도).
        /// 부르는 쪽은 매 프레임 <see cref="LoopHandle.Target"/> 만 정한다.
        /// </summary>
        /// <param name="repeatEvery">0 이면 이음새 없는 루프. 양수면 그 간격(초)마다 클립을 다시 튼다 (짧은 소리를 띄어서 반복).</param>
        public LoopHandle Loop(string key, AudioClip clip, float fadeSeconds = 0.6f, float repeatEvery = 0f)
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
            handle.RepeatEvery = Mathf.Max(0f, repeatEvery);
            if (handle.Source != null) handle.Source.loop = handle.RepeatEvery <= 0f;
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

            _bgmFront.volume = Mathf.Min(1f, (_bgmFront.clip != null && _bgmWant != null ? _bgmFade : 0f) * GainFor(_bgmFront.clip) * _musicVolume);
            other.volume = Mathf.Min(1f, (1f - _bgmFade) * GainFor(other.clip) * _musicVolume);

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
                loop.Source.volume = Mathf.Min(1f, loop.Now * GainFor(loop.Source.clip) * _effectsVolume);

                bool wanted = loop.Now > 0.001f;

                if (loop.RepeatEvery > 0f)
                {
                    // 띄어서 반복 — 간격이 되면 다시 튼다. 꺼질 때는 나던 소리는 끝까지 두고 다음 것만 안 튼다.
                    if (wanted && Time.unscaledTime >= loop.NextPlayAt)
                    {
                        loop.Source.Play();
                        loop.NextPlayAt = Time.unscaledTime + loop.RepeatEvery;
                    }
                    else if (!wanted)
                    {
                        loop.NextPlayAt = 0f;
                    }

                    continue;
                }

                if (wanted && !loop.Source.isPlaying)
                {
                    loop.Source.Play();
                }
                else if (!wanted && loop.Source.isPlaying)
                {
                    loop.Source.Stop();
                }
            }
        }
    }
}

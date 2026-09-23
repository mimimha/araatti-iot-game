using UnderTheSea.Audio;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 🔊 광산의 소리 **연출가.** 판 상태를 보고 <see cref="AudioHub"/> 에 "이 곡 · 이 소리" 를 부탁한다.
    /// (AUDIO.md 4장 · 본보기는 배 협동의 <c>ShipCoopAudio</c> · 무쌍의 <c>WarriorsAudio</c>)
    ///
    /// <code>
    ///   🎵 배경음악   대기 → 본편(카운트다운 ~ 마지막 턴) → "채굴 종료" 에서 멈춤 → 성적표에서 스팅어(성공 · 실패)
    ///   🌊 루프       동굴 울림. 판이 끝나면 걷힌다
    ///   💥 효과음     카운트다운 · 시작 · 턴 시작 · 시간 경고 · 돌 금 · 돌 깨짐 · 복구 · 힌트 · 헛스윙
    /// </code>
    ///
    /// <b>⚠ 서버 판정에 걸지 않는다.</b> 판정은 서버에서 돌고, 데디케이티드 서버는 소리를 못 낸다
    /// (AUDIO.md 4.2). 그래서 <b>복제되는 값이 바뀐 순간</b>을 매 프레임 잡는다.
    ///
    /// <code>
    ///   카운트다운 3·2·1  MineMatchState.Countdown 의 정수 초가 3 이 됨 — 한 번만 (아래)
    ///   시작              Phase 가 Countdown → Reveal (도안이 뜨는 순간)
    ///   턴 시작           Turn 중 CurrentSlot 이 바뀜 (첫 턴 포함)
    ///   시간 경고         Turn 중 TurnTimeLeft 가 warnFromSeconds 이하인 동안 루프 (아래)
    ///   돌 금 · 깨짐      MineGrid.OnCellHit — 서버는 Hit, 클라이언트는 ShowCell 에서 똑같이 울린다
    ///   복구              RestoresLeft 가 줄어듦
    ///   힌트              HintLeft 가 0 에서 커짐
    ///   스팅어            ShowingMineResult 가 켜짐 · ResultSuccess 로 성공/실패
    ///   헛스윙            MineInputProvider.LocalSwing — 내 화면에서만 (아래)
    /// </code>
    ///
    /// <b>카운트다운과 시간 경고는 매초 틀지 않는다.</b> 받은 클립이 이미 여러 박을 담고 있다
    /// (삑·삑·삑·삐— 한 벌, 째깍이 이어지는 7초). 매초 틀면 앞 소리 위에 겹겹이 쌓인다.
    /// 그래서 카운트다운은 "3" 에서 한 번 틀어 박자를 그대로 흘려보내고, 시간 경고는 루프로 켰다가 끈다.
    ///
    /// <b>헛스윙은 로컬 사건이다.</b> 서버는 턴이 아닌 사람의 휘두름과 도안이 떠 있는 동안의 휘두름을
    /// 조용히 버리므로 복제되는 값이 없다. 누른 사람 본인만 들으면 되므로 입력 순간에 내 화면에서 바로 낸다.
    /// 판단은 서버와 같은 조건(<c>IsMyTurn</c> · <c>ShowingTarget</c>)을 내 복사본으로 본다.
    ///
    /// <b>클립이 비어 있으면 그 소리만 조용히 건너뛴다.</b> 클립은 <c>Assets/Game/Audio/Mine/</c> 에
    /// <b>필드 이름과 같은 파일 이름</b>으로 두면 설치 도구(<c>MineAudioInstaller</c>)가 채운다.
    ///
    /// ⚠ 네트워크 씬(<c>MineNet.unity</c>) 전용이다. 1인 검증 씬에는 <c>MineMatchState</c> 가 없어
    ///   아무 소리도 내지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineAudio : MonoBehaviour
    {
        // ------------------------------------------------------------
        // 🎵 배경음악
        // ------------------------------------------------------------

        [Header("🎵 배경음악")]
        [Tooltip("사람이 모이기를 기다리는 동안.")]
        [SerializeField] private AudioClip bgmWaiting;

        [Tooltip("카운트다운부터 마지막 턴까지.")]
        [SerializeField] private AudioClip bgmPlaying;

        [Tooltip("성공 스팅어. 성적표가 뜰 때 한 번.")]
        [SerializeField] private AudioClip stingerClear;

        [Tooltip("실패 스팅어. 성적표가 뜰 때 한 번.")]
        [SerializeField] private AudioClip stingerFail;

        [Tooltip("곡을 바꿀 때 겹치는 시간(초). 배 협동과 같은 1.5초.")]
        [SerializeField, Range(0.1f, 5f)] private float crossfadeSeconds = 1.5f;

        // ------------------------------------------------------------
        // 🌊 루프
        // ------------------------------------------------------------

        [Header("🌊 루프")]
        [Tooltip("동굴 울림 · 물방울. 판이 끝날 때까지 얕게 깔린다.")]
        [SerializeField] private AudioClip caveLoop;

        [Tooltip("루프의 기본 크기(0~1). 효과음 볼륨이 곱해진다.")]
        [SerializeField, Range(0f, 1f)] private float loopLevel = 0.45f;

        // ------------------------------------------------------------
        // 💥 효과음
        // ------------------------------------------------------------

        [Header("💥 효과음 — 판 진행")]
        [Tooltip("시작 카운트다운. \"3\" 이 되는 순간 한 번 튼다 — 3 · 2 · 1 박자가 담긴 클립 하나를 그대로 흘려보낸다.")]
        [SerializeField] private AudioClip countTick;

        [Tooltip("카운트다운이 끝나고 도안이 뜨는 순간.")]
        [SerializeField] private AudioClip countGo;

        [Tooltip("누군가의 턴이 시작되는 순간. 첫 턴 포함.")]
        [SerializeField] private AudioClip turnStart;

        [Tooltip("턴 시간이 얼마 안 남았을 때 루프로 깔린다. 째깍이 이어지는 클립을 쓴다.")]
        [SerializeField] private AudioClip timeWarn;

        [Tooltip("몇 초 남았을 때부터 경고음을 켤 것인가.")]
        [SerializeField, Range(1, 10)] private int warnFromSeconds = 5;

        [Header("💥 효과음 — 채굴")]
        [Tooltip("단단한 돌에 금만 갔다.")]
        [SerializeField] private AudioClip stoneCrack;

        [Tooltip("돌이 깨졌다.")]
        [SerializeField] private AudioClip stoneBreak;

        [Tooltip("복구 블록으로 되메웠다.")]
        [SerializeField] private AudioClip restorePlace;

        [Tooltip("누군가 힌트를 켰다.")]
        [SerializeField] private AudioClip hintOpen;

        [Tooltip("팔 수 없을 때 휘둘렀다 — 내 턴이 아니거나 도안이 떠 있다. 내 화면에서만.")]
        [SerializeField] private AudioClip swingMiss;

        // ------------------------------------------------------------
        // 🔉 크기
        // ------------------------------------------------------------

        [Header("🔉 크기 — 1 이 기준, 자주 나는 소리는 작게")]
        [Tooltip("효과음의 기본 크기(0~1). 효과음 볼륨이 곱해진다.")]
        [SerializeField, Range(0f, 1f)] private float sfxLevel = 0.85f;

        [Tooltip("돌 금 · 깨짐. 가장 자주 난다.")]
        [SerializeField, Range(0f, 2f)] private float stoneLevel = 0.8f;

        [Tooltip("헛스윙. 관전자가 연타하면 계속 나므로 작게.")]
        [SerializeField, Range(0f, 2f)] private float swingMissLevel = 0.5f;

        [Tooltip("접속 직후 이 시간(초)만큼은 효과음을 내지 않는다. 늦게 들어오면 이미 파인 칸이 한꺼번에 " +
                 "도착해 \"깨진 순간\" 으로 잡혀 소리가 우르르 난다.")]
        [SerializeField, Range(0f, 5f)] private float quietSecondsOnJoin = 1.5f;

        // ------------------------------------------------------------
        // 들고 있는 것
        // ------------------------------------------------------------

        private AudioHub _hub;
        private AudioHub.LoopHandle _cave;
        private AudioHub.LoopHandle _warn;

        private MineMatchState _match;
        private MineNetPlayer _me;
        private MineGrid _grid;

        /// <summary>판과 내 캐릭터는 늦게 생긴다. 찾을 때까지 이 간격(초)으로 다시 찾는다.</summary>
        private const float RescanSeconds = 0.5f;
        private float _nextRescan;

        /// <summary>이 시각까지는 효과음을 안 낸다.</summary>
        private float _quietUntil;

        // 본 값들. -1 은 "아직 한 번도 안 봤다" 라 첫 스냅숏에 몰아 울리지 않는다.
        private MineMatchPhase _phaseSeen;
        private bool _phaseInit;
        private bool _countTickPlayed;

        /// <summary>늦게 봤을 때 앞을 잘라 만든 카운트다운 클립. 판마다 새로 만들고 앞의 것은 버린다.</summary>
        private AudioClip _countTickCut;
        private int _slotSeen = -1;
        private int _restoresSeen = -1;
        private bool _hintSeen;
        private bool _stingerPlayed;

        // ------------------------------------------------------------
        // 수명
        // ------------------------------------------------------------

        private void Awake()
        {
            _hub = AudioHub.Instance;

            if (_hub == null || !_hub.CanHear)
            {
                // 서버(그래픽 장치 없음), 또는 앱 종료 중. 아무것도 만들지 않는다.
                enabled = false;
                return;
            }

            // 이름은 "게임.용도" 로. 다른 미니게임의 루프와 겹치면 서로 클립을 빼앗는다.
            _cave = _hub.Loop("mine.cave", caveLoop, 1.2f);

            // 짧게 페이드 — 켜는 순간 째깍이 바로 들려야 하고, 턴이 넘어가면 바로 멎어야 한다.
            // 꺼졌다 다시 켜지면 클립 처음부터 튼다 (AudioSource.Stop → Play).
            _warn = _hub.Loop("mine.warn", timeWarn, 0.1f);
        }

        private void OnEnable()
        {
            MineInputProvider.LocalSwing += HandleLocalSwing;
        }

        private void OnDisable()
        {
            MineInputProvider.LocalSwing -= HandleLocalSwing;
        }

        private void Start()
        {
            _quietUntil = Time.time + quietSecondsOnJoin;

            // 격자는 씬에 처음부터 있다. 서버는 Hit, 클라이언트는 ShowCell 에서 OnCellHit 이 울린다.
            _grid = FindAnyObjectByType<MineGrid>(FindObjectsInactive.Include);
            if (_grid != null) _grid.OnCellHit += HandleCellHit;

            Rescan(force: true);
        }

        private void OnDestroy()
        {
            if (_grid != null) _grid.OnCellHit -= HandleCellHit;
            if (_countTickCut != null) Destroy(_countTickCut);

            // 씬을 떠난다. 루프는 끄고 음악은 페이드 아웃 — 다음 씬의 SceneMusic 이 새 곡을 올린다.
            if (_hub == null) return;

            _hub.StopAllLoops();
            _hub.StopMusic(crossfadeSeconds);
        }

        // ------------------------------------------------------------
        // 찾기 — 판과 내 캐릭터는 늦게 생긴다
        // ------------------------------------------------------------

        private void Rescan(bool force)
        {
            // 판은 정적 참조라 매 프레임 읽는다. 0.5초 간격에 묶어 두면 판이 생기자마자 시작하는
            // 카운트다운(-crew 1 · 마지막 사람)을 최대 0.5초 늦게 보고, 3 · 2 · 1 소리가 화면보다 밀린다.
            _match = MineMatchState.Current;

            if (!force && Time.time < _nextRescan) return;
            _nextRescan = Time.time + RescanSeconds;

            if (_me != null && _me.Object != null && _me.Object.IsValid) return;

            _me = null;

            foreach (MineNetPlayer one in FindObjectsByType<MineNetPlayer>(FindObjectsInactive.Include))
            {
                // ⚠ **내 캐릭터만** 본다. 헛스윙은 내가 팔 수 있는지로 정한다.
                if (one == null || one.Object == null || !one.Object.IsValid || !one.HasInputAuthority) continue;

                _me = one;
                break;
            }
        }

        // ------------------------------------------------------------
        // 매 프레임
        // ------------------------------------------------------------

        private void Update()
        {
            if (_hub == null) return;

            Rescan(force: false);

            if (_match == null || _match.Object == null || !_match.Object.IsValid)
            {
                // 네트워크 판이 아직 없거나(부트 중) 1인 검증 씬이다. 조용히 기다린다.
                _cave.Target = 0f;
                _warn.Target = 0f;
                return;
            }

            MineMatchPhase phase = _match.Phase;

            WatchCountdown(phase);
            WatchPhase(phase);
            WatchTurn(phase);
            WatchRestores();
            WatchHint();
            WatchResult(phase);

            ChooseMusic(phase);
            _cave.Target = phase == MineMatchPhase.Finished ? 0f : loopLevel;
        }

        // ------------------------------------------------------------
        // 판 진행
        // ------------------------------------------------------------

        /// <summary>
        /// 시작 카운트다운. **판마다 한 번** 낸다 — 클립 하나에 3 · 2 · 1 박자가 들어 있다
        /// (0 · 1 · 2초에 삑, 3초에 긴 삑). 남은 시간이 3 일 때 처음부터 틀면 화면과 맞는다.
        ///
        /// ⚠ **늦게 본 만큼 클립 중간부터 튼다.** 씬을 불러오는 동안 서버의 카운트다운이 먼저 흐르므로
        ///   클라이언트는 3 을 못 보고 2.3 쯤에서 처음 본다(실측 0.69초). 그대로 처음부터 틀면 소리가 통째로 밀린다.
        /// </summary>
        private void WatchCountdown(MineMatchPhase phase)
        {
            if (phase != MineMatchPhase.Countdown)
            {
                _countTickPlayed = false;
                return;
            }

            if (_countTickPlayed || countTick == null) return;

            float late = 3f - _match.Countdown;
            if (late < 0f) return;   // 카운트다운이 3초보다 길면 3 이 될 때까지 기다린다

            _countTickPlayed = true;

            // ⚠ 접속 직후 조용한 시간(quietSecondsOnJoin)을 **무시한다.** 인원이 차는 순간 카운트다운이
            //   시작되므로, 마지막 사람에게는 카운트다운이 늘 접속 직후 1.5초 안에 온다 (-crew 1 이면 언제나).
            //   조용한 시간은 쌓인 값이 한꺼번에 도착해 우르르 나는 것을 막는 것이고, 이것은 한 번뿐이라 상관없다.
            PlayCountTickFrom(late);

            Debug.Log($"[MineAudio] 카운트다운 소리 — 남은 {_match.Countdown:F2}초에 봤다. 클립 {late:F2}초 지점부터 튼다", this);
        }

        /// <summary>
        /// 카운트다운 클립을 <paramref name="offset"/> 초 지점부터 튼다.
        ///
        /// 허브의 <c>PlayOneShot</c> 은 시작 지점을 못 정한다. 그래서 앞을 잘라 낸 복사본을 만들어 넘긴다.
        /// ⚠ 자른 자리가 삑 사이 무음이면 **다음 삑까지 기다렸다가** 그 삑부터 튼다 — 허브는 앞 무음을
        ///   건너뛰고 트므로(<c>AudioHub.LeadInFor</c>), 무음째 넘기면 다음 삑이 앞당겨져 박자가 어긋난다.
        /// </summary>
        private void PlayCountTickFrom(float offset)
        {
            if (offset < 0.03f || countTick.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                Play(countTick, ignoreQuiet: true);
                return;
            }

            int channels = countTick.channels;
            int frequency = countTick.frequency;
            int start = Mathf.FloorToInt(offset * frequency);
            if (start >= countTick.samples) return;

            var rest = new float[(countTick.samples - start) * channels];
            if (!countTick.GetData(rest, start))
            {
                Play(countTick, ignoreQuiet: true);
                return;
            }

            // 다음 소리가 시작되는 표본. 허브가 "소리 시작" 으로 보는 크기와 같은 기준이다.
            int first = -1;
            for (int i = 0; i < rest.Length; i++)
            {
                if (Mathf.Abs(rest[i]) >= 0.02f) { first = i / channels; break; }
            }
            if (first < 0) return;   // 남은 부분에 소리가 없다

            int length = rest.Length / channels - first;
            var cut = new float[length * channels];
            System.Array.Copy(rest, first * channels, cut, 0, cut.Length);

            if (_countTickCut != null) Destroy(_countTickCut);
            _countTickCut = AudioClip.Create(countTick.name + " (cut)", length, channels, frequency, false);
            _countTickCut.SetData(cut, 0);

            // 허브는 클립마다 크기를 재서 맞춘다. 자른 클립도 원본과 같은 크기로 들리도록 배율을 되돌려 준다.
            float level = _hub.GainFor(countTick) / Mathf.Max(_hub.GainFor(_countTickCut), 1e-4f);

            StartCoroutine(PlayCountTickAfter(_countTickCut, (float)first / frequency, level));
        }

        private System.Collections.IEnumerator PlayCountTickAfter(AudioClip clip, float wait, float level)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            Play(clip, level, ignoreQuiet: true);
        }

        /// <summary>카운트다운이 끝나 도안이 뜨는 순간.</summary>
        private void WatchPhase(MineMatchPhase phase)
        {
            if (!_phaseInit)
            {
                _phaseInit = true;
                _phaseSeen = phase;
                return;
            }

            if (phase == _phaseSeen) return;

            MineMatchPhase was = _phaseSeen;
            _phaseSeen = phase;

            if (was == MineMatchPhase.Countdown && phase == MineMatchPhase.Reveal) Play(countGo);
        }

        /// <summary>턴 시작 · 남은 시간 경고.</summary>
        private void WatchTurn(MineMatchPhase phase)
        {
            if (phase != MineMatchPhase.Turn)
            {
                _slotSeen = -1;
                _warn.Target = 0f;
                return;
            }

            int slot = _match.CurrentSlot;

            if (slot != _slotSeen)
            {
                _slotSeen = slot;
                Play(turnStart);
            }

            // 목표만 정한다. 다음 턴이 열려 시간이 다시 차면 스스로 꺼진다.
            float left = _match.TurnTimeLeft;
            _warn.Target = left > 0f && left <= warnFromSeconds ? sfxLevel : 0f;
        }

        /// <summary>복구 블록이 하나 줄어든 순간. 새 판에서 다시 채워질 때는 조용히 따라간다.</summary>
        private void WatchRestores()
        {
            int left = _match.RestoresLeft;

            if (_restoresSeen >= 0 && left < _restoresSeen) Play(restorePlace);

            _restoresSeen = left;
        }

        /// <summary>누군가 힌트를 켠 순간.</summary>
        private void WatchHint()
        {
            bool on = _match.HintLeft > 0f;

            if (on && !_hintSeen) Play(hintOpen);

            _hintSeen = on;
        }

        /// <summary>성적표가 뜨는 순간 스팅어 한 번. 판마다 한 번뿐이다.</summary>
        private void WatchResult(MineMatchPhase phase)
        {
            if (phase != MineMatchPhase.Finished)
            {
                _stingerPlayed = false;
                return;
            }

            if (_stingerPlayed || !_match.ShowingMineResult) return;

            _stingerPlayed = true;
            Play(_match.ResultSuccess ? stingerClear : stingerFail);
        }

        /// <summary>
        /// 상태에 맞는 곡. 매 프레임 불러도 된다 — 같은 곡이면 허브가 무시한다.
        /// "채굴 종료" 부터는 멈춘다. 결과 화면은 스팅어만 (AUDIO.md 4.3).
        /// </summary>
        private void ChooseMusic(MineMatchPhase phase)
        {
            switch (phase)
            {
                case MineMatchPhase.Waiting:
                    _hub.PlayMusic(bgmWaiting, crossfadeSeconds);
                    break;

                case MineMatchPhase.Countdown:
                case MineMatchPhase.Reveal:
                case MineMatchPhase.Turn:
                    _hub.PlayMusic(bgmPlaying, crossfadeSeconds);
                    break;

                default:
                    _hub.StopMusic(crossfadeSeconds);
                    break;
            }
        }

        // ------------------------------------------------------------
        // 채굴
        // ------------------------------------------------------------

        /// <summary>돌을 친 순간. 되메우기나 판 초기화로는 울리지 않는다 (<see cref="MineGrid.OnCellHit"/>).</summary>
        private void HandleCellHit(int x, int y, bool broke)
        {
            Play(broke ? stoneBreak : stoneCrack, stoneLevel);
        }

        /// <summary>
        /// 휘두름 키를 눌렀다. **서버가 버릴 휘두름이면** 헛스윙 소리를 낸다.
        /// 조건은 <c>MineNetPlayerActions</c> 와 같다 — 내 턴이 아니거나, 도안이 떠 있다.
        /// 판이 살아 있는 동안(카운트다운 · 공개 · 턴)만 낸다. 대기와 결과 화면에서는 조용하다.
        /// </summary>
        private void HandleLocalSwing()
        {
            if (_hub == null || _match == null || _match.Object == null || !_match.Object.IsValid) return;

            MineMatchPhase phase = _match.Phase;
            bool live = phase == MineMatchPhase.Countdown
                     || phase == MineMatchPhase.Reveal
                     || phase == MineMatchPhase.Turn;
            if (!live) return;

            bool canDig = _me != null && _me.IsMyTurn && !_match.ShowingTarget;
            if (canDig) return;

            Play(swingMiss, swingMissLevel);
        }

        // ------------------------------------------------------------

        private void Play(AudioClip clip, float level = 1f, bool ignoreQuiet = false)
        {
            if (clip == null) return;
            if (!ignoreQuiet && Time.time < _quietUntil) return;
            _hub.PlayOneShot(clip, sfxLevel * level);
        }
    }
}

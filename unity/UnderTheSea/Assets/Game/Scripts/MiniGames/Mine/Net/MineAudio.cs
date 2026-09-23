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
    ///   카운트다운 3·2·1  MineMatchState.Countdown 의 정수 초가 바뀜
    ///   시작              Phase 가 Countdown → Reveal (도안이 뜨는 순간)
    ///   턴 시작           Turn 중 CurrentSlot 이 바뀜 (첫 턴 포함)
    ///   시간 경고         Turn 중 TurnTimeLeft 의 정수 초가 warnFromSeconds 이하에서 바뀜
    ///   돌 금 · 깨짐      MineGrid.OnCellHit — 서버는 Hit, 클라이언트는 ShowCell 에서 똑같이 울린다
    ///   복구              RestoresLeft 가 줄어듦
    ///   힌트              HintLeft 가 0 에서 커짐
    ///   스팅어            ShowingMineResult 가 켜짐 · ResultSuccess 로 성공/실패
    ///   헛스윙            MineInputProvider.LocalSwing — 내 화면에서만 (아래)
    /// </code>
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
        [Tooltip("시작 카운트다운 3 · 2 · 1.")]
        [SerializeField] private AudioClip countTick;

        [Tooltip("카운트다운이 끝나고 도안이 뜨는 순간.")]
        [SerializeField] private AudioClip countGo;

        [Tooltip("누군가의 턴이 시작되는 순간. 첫 턴 포함.")]
        [SerializeField] private AudioClip turnStart;

        [Tooltip("턴 시간이 얼마 안 남았을 때 초마다.")]
        [SerializeField] private AudioClip timeWarn;

        [Tooltip("몇 초 남았을 때부터 경고음을 낼 것인가.")]
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
        private int _countdownSeen = -1;
        private int _slotSeen = -1;
        private int _secondsSeen = -1;
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
            if (!force && Time.time < _nextRescan) return;
            _nextRescan = Time.time + RescanSeconds;

            _match = MineMatchState.Current;

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

        /// <summary>시작 카운트다운 3 · 2 · 1. 1초마다 한 번씩만 낸다.</summary>
        private void WatchCountdown(MineMatchPhase phase)
        {
            if (phase != MineMatchPhase.Countdown)
            {
                _countdownSeen = -1;
                return;
            }

            int left = Mathf.CeilToInt(_match.Countdown);
            if (left == _countdownSeen) return;

            _countdownSeen = left;
            if (left >= 1 && left <= 3) Play(countTick);
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
                _secondsSeen = -1;
                return;
            }

            int slot = _match.CurrentSlot;

            if (slot != _slotSeen)
            {
                _slotSeen = slot;
                _secondsSeen = -1;
                Play(turnStart);
            }

            int seconds = Mathf.CeilToInt(_match.TurnTimeLeft);
            if (seconds == _secondsSeen) return;

            _secondsSeen = seconds;
            if (seconds >= 1 && seconds <= warnFromSeconds) Play(timeWarn);
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

        private void Play(AudioClip clip, float level = 1f)
        {
            if (clip == null || Time.time < _quietUntil) return;
            _hub.PlayOneShot(clip, sfxLevel * level);
        }
    }
}

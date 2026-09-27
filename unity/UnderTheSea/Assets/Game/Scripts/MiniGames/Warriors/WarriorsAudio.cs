using UnderTheSea.Audio;
using UnityEngine;
using Warriors.Net;

namespace Warriors
{
    /// <summary>
    /// 🔊 무쌍의 소리 **연출가.** 판 상태를 보고 <see cref="AudioHub"/> 에 "이 곡 · 이 소리" 를 부탁한다.
    /// (AUDIO.md 4장 · 본보기는 배 협동의 <c>ShipCoopAudio</c>)
    ///
    /// <code>
    ///   🎵 배경음악   대기 → 1R → 2R → 3R(긴장) → 결과 스팅어(성공 · 실패) · 결과 화면은 음악을 멈춘다
    ///   🌊 루프       해변 파도(판 내내 같은 크기) · 크라켄 숨소리(3R)
    ///   💥 효과음     베기 3종 · 몬스터 정타/처치 · 내 피격 · 쓰러짐 · 촉수 타격/절단 · 마무리 창 ·
    ///                협동 세트 · 콤보 · 노트 정타/미스 · 크라켄 피격/포효 · 라운드 전환 · 카운트다운
    /// </code>
    ///
    /// <b>소스는 직접 들지 않는다.</b> 재생 · 페이드 · 볼륨은 전부 허브가 한다. 이 파일은 "언제 무엇을" 만 정한다.
    ///
    /// <b>⚠ 서버 판정 <c>event Action</c> 에 걸지 않는다.</b> 판정은 전부 서버에서 도는데, 데디케이티드 서버는
    /// 소리를 낼 수 없으므로 거기 걸면 <b>아무도 못 듣는다</b> (AUDIO.md 4.2 · WARRIORS.md 4장).
    /// 그래서 <b>복제되는 값이 바뀐 순간</b>을 매 프레임 잡는다. 표는 이렇다.
    ///
    /// <code>
    ///   베기 3종        WarriorsInputProvider.LocalSwing   내 화면에서 터지는 로컬 사건 (서버 왕복 없음)
    ///   몬스터 정타     내 WarriorsNetPlayerCombat.Combo 가 늘어남
    ///   몬스터 처치     WarriorsMatchState.Phase1Kills 가 늘어남 (팀 합산)
    ///   내 피격·쓰러짐  내 WarriorsPlayerLife.Hp 감소 · IsDown
    ///   촉수 타격·절단  WarriorsPhase2Director.Slots[i] 의 HitsLeft · Result
    ///   마무리 창       WarriorsPhase2Director.FinishWindowOpenFor(내 레인)
    ///   협동 세트       WarriorsPhase2Director.ComboSetSerial
    ///   노트 정타       WarriorsPhase3Director.HitSerial · HitLane · HitStrength
    ///   노트 미스       WarriorsPhase3Director.Notes[i].State 가 2
    ///   콤보 피니시     WarriorsPhase3Director.FinishSerial · FinishKind (포효가 아니라 완성 보상음)
    ///   라운드·결과     WarriorsMatchState.Phase · Countdown
    /// </code>
    ///
    /// <b>효과음은 되도록 내가 한 일에만 낸다.</b> 베기 · 정타 · 내 피격 · 내 노트 · 내 촉수는 <b>내 것만</b>이다.
    /// 판 전체에 일어나는 일(몬스터 처치 수 · 크라켄 피격 · 라운드 전환 · 결과)만 공통으로 낸다.
    /// 한 PC 에 클라이언트를 둘 띄우면 공통 소리는 두 겹으로 들린다 — 그것이 정상이다.
    ///
    /// <b>클립이 비어 있으면 그 소리만 조용히 건너뛴다.</b> 클립은 <c>Assets/Game/Audio/Warriors/</c> 에
    /// <b>필드 이름과 같은 파일 이름</b>으로 두면 설치 도구(<c>WarriorsAudioInstaller</c>)가 채운다.
    ///
    /// ⚠ 네트워크 씬(<c>WarriorsNet.unity</c>) 전용이다. 1인 검증 씬(<c>WarriorsTest.unity</c>)에는
    ///   <c>WarriorsMatchState</c> 가 없어 아무 소리도 내지 않는다 — 그 씬은 옛 규칙 그대로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsAudio : MonoBehaviour
    {
        // ------------------------------------------------------------
        // 🎵 배경음악
        // ------------------------------------------------------------

        [Header("🎵 배경음악")]
        [Tooltip("사람을 기다리는 동안과 시작 카운트다운.")]
        [SerializeField] private AudioClip bgmWaiting;

        [Tooltip("1라운드 — 해변 방어.")]
        [SerializeField] private AudioClip bgmRound1;

        [Tooltip("2라운드 — 촉수 절단.")]
        [SerializeField] private AudioClip bgmRound2;

        [Tooltip("3라운드 — 크라켄과의 리듬 전투. 가장 긴장되는 곡.")]
        [SerializeField] private AudioClip bgmRound3;

        [Tooltip("성공 스팅어. 한 번 나고 배경음악은 멈춘다 (결과 화면은 조용히).")]
        [SerializeField] private AudioClip stingerClear;

        [Tooltip("실패 스팅어. 한 번 나고 배경음악은 멈춘다.")]
        [SerializeField] private AudioClip stingerFail;

        [Tooltip("곡을 바꿀 때 겹치는 시간(초). 배 협동과 같은 1.5초.")]
        [SerializeField, Range(0.1f, 5f)] private float crossfadeSeconds = 1.5f;

        // ------------------------------------------------------------
        // 🌊 루프
        // ------------------------------------------------------------

        [Header("🌊 루프")]
        [Tooltip("해변 파도. 대기부터 결과까지 판 내내 같은 크기로 잔잔하게 깔린다.")]
        [SerializeField] private AudioClip waveLoop;

        [Tooltip("크라켄의 숨소리 · 낮은 울림. 3라운드 동안만.")]
        [SerializeField] private AudioClip krakenLoop;

        [Tooltip("루프의 기본 크기(0~1). 효과음 볼륨이 곱해진다.")]
        [SerializeField, Range(0f, 1f)] private float loopLevel = 0.45f;

        // ------------------------------------------------------------
        // 💥 효과음 — 내 검
        // ------------------------------------------------------------

        [Header("💥 효과음 — 내 검 (내 화면에서만)")]
        [Tooltip("가로베기(J). 휘두르는 순간.")]
        [SerializeField] private AudioClip swingHorizontal;

        [Tooltip("세로베기(K).")]
        [SerializeField] private AudioClip swingVertical;

        [Tooltip("찌르기(L).")]
        [SerializeField] private AudioClip swingThrust;

        [Tooltip("내 칼이 몬스터에 맞은 순간. 헛치면 안 난다.")]
        [SerializeField] private AudioClip monsterHit;

        [Tooltip("내 콤보가 3의 배수에 닿을 때. 3 · 6 · 9 …")]
        [SerializeField] private AudioClip comboUp;

        // ------------------------------------------------------------
        // 💥 효과음 — 1라운드
        // ------------------------------------------------------------

        [Header("💥 효과음 — 1라운드")]
        [Tooltip("몬스터가 쓰러짐. 누가 벴든 팀 처치 수가 오르면 난다.")]
        [SerializeField] private AudioClip monsterKill;

        // ------------------------------------------------------------
        // 💥 효과음 — 2라운드
        // ------------------------------------------------------------

        [Header("💥 효과음 — 2라운드 (촉수)")]
        [Tooltip("내 촉수에 정타가 들어갔지만 아직 안 잘림. 촉수 한 대가 1타면 나지 않는다.")]
        [SerializeField] private AudioClip tentacleHit;

        [Tooltip("내 촉수가 잘림.")]
        [SerializeField] private AudioClip tentacleCut;

        [Tooltip("내게 협동 마무리 창이 열림 — \"지금! 이어서 베세요\".")]
        [SerializeField] private AudioClip finishWindow;

        [Tooltip("완성 보상음. 2R 협동 세트와 3R 콤보 피니시에 함께 쓴다 — 둘 다 \"묶음을 다 채웠다\" 는 순간.")]
        [SerializeField] private AudioClip comboSet;

        // ------------------------------------------------------------
        // 💥 효과음 — 3라운드
        // ------------------------------------------------------------

        [Header("💥 효과음 — 3라운드 (리듬)")]
        [Tooltip("내 레인 노트를 가로베기로 받아 냄 — 도(C5).")]
        [SerializeField] private AudioClip noteHitHorizontal;

        [Tooltip("세로베기로 받아 냄 — 레(D5).")]
        [SerializeField] private AudioClip noteHitVertical;

        [Tooltip("찌르기로 받아 냄 — 미(E5).")]
        [SerializeField] private AudioClip noteHitThrust;

        [Tooltip("내 레인 노트를 놓침.")]
        [SerializeField] private AudioClip noteMiss;

        [Tooltip("크라켄이 맞음. 누구 노트든 정타가 들어가면 난다.")]
        [SerializeField] private AudioClip krakenHurt;

        [Tooltip("크라켄의 포효. 3라운드가 열릴 때 한 번만 — 피니시마다 내면 반복돼 질린다.")]
        [SerializeField] private AudioClip krakenRoar;

        // ------------------------------------------------------------
        // 💥 효과음 — 나 · 판 진행
        // ------------------------------------------------------------

        [Header("💥 효과음 — 나")]
        [Tooltip("내 HP 가 깎임.")]
        [SerializeField] private AudioClip playerHurt;

        [Tooltip("내가 쓰러짐. 부활은 없다.")]
        [SerializeField] private AudioClip playerDown;

        [Header("💥 효과음 — 판 진행")]
        [Tooltip("라운드가 바뀌는 순간.")]
        [SerializeField] private AudioClip roundChange;

        [Tooltip("시작 카운트다운 3 · 2 · 1.")]
        [SerializeField] private AudioClip countdownTick;

        [Tooltip("카운트다운이 끝나고 1라운드가 시작되는 순간.")]
        [SerializeField] private AudioClip countdownGo;

        // ------------------------------------------------------------
        // 🔉 크기
        // ------------------------------------------------------------

        [Header("🔉 크기 — 1 이 기준, 자주 나는 소리는 작게")]
        [Tooltip("효과음의 기본 크기(0~1). 효과음 볼륨이 곱해진다.")]
        [SerializeField, Range(0f, 1f)] private float sfxLevel = 0.85f;

        [Tooltip("베기. 자주 나지만 **너무 낮추면 2 · 3라운드에서 사라진다** — 그쪽은 타격음과 노트음이 " +
                 "매번 겹쳐서 이 소리를 덮는다. 다른 소리와 비슷한 크기로 깔아 둔다.")]
        [SerializeField, Range(0f, 2f)] private float swingLevel = 1f;

        [Tooltip("몬스터 정타.")]
        [SerializeField, Range(0f, 2f)] private float monsterLevel = 0.8f;

        [Tooltip("몬스터 처치. 정타와 클립이 달라 크기도 따로 잡는다 — 한 값으로 묶으면 한쪽이 튄다.")]
        [SerializeField, Range(0f, 2f)] private float killLevel = 0.4f;

        [Tooltip("노트 정타 · 미스. 정타는 krakenHurt 와 **겹쳐** 난다 — 둘이 합쳐 \"치는 맛\" 이 되므로 " +
                 "너무 낮추면 리듬 타격이 가벼워진다.")]
        [SerializeField, Range(0f, 2f)] private float noteLevel = 0.8f;

        [Tooltip("크라켄 피격 · 포효. 가장 큰 소리.")]
        [SerializeField, Range(0f, 2f)] private float krakenLevel = 1.15f;

        [Tooltip("접속 직후 이 시간(초)만큼은 효과음을 내지 않는다. 서버가 쌓아 둔 값이 한꺼번에 도착해 " +
                 "\"바뀐 순간\" 으로 잡히면 들어오자마자 소리가 우르르 난다.")]
        [SerializeField, Range(0f, 5f)] private float quietSecondsOnJoin = 1.5f;

        // ------------------------------------------------------------
        // 들고 있는 것
        // ------------------------------------------------------------

        private AudioHub _hub;
        private AudioHub.LoopHandle _wave;
        private AudioHub.LoopHandle _kraken;

        private WarriorsMatchState _match;
        private WarriorsPlayerLife _me;
        private WarriorsNetPlayerCombat _myCombat;

        /// <summary>내 레인(플레이어 번호). 아직 모르면 -1.</summary>
        private int _lane = -1;

        /// <summary>플레이어는 늦게 생긴다. 찾을 때까지 이 간격(초)으로 다시 찾는다.</summary>
        private const float RescanSeconds = 0.5f;
        private float _nextRescan;

        /// <summary>이 시각까지는 효과음을 안 낸다.</summary>
        private float _quietUntil;

        // 본 값들. -1 은 "아직 한 번도 안 봤다" 라 첫 스냅숏에 몰아 울리지 않는다.
        private WarriorsMatchPhase _phaseSeen;
        private bool _phaseInit;
        private int _killsSeen = -1;
        private int _comboSeen = -1;
        private int _hpSeen = -1;
        private bool _downSeen;
        private int _countdownSeen = -1;
        private int _comboSetSeen = -1;
        private bool _windowOpenSeen;
        private int _hitSerialSeen = -1;
        private int _finishSerialSeen = -1;
        private bool _stingerPlayed;

        private readonly int[] _slotSerial = new int[WarriorsPhase2Director.SlotCount];
        private readonly int[] _slotHitsLeft = new int[WarriorsPhase2Director.SlotCount];
        private readonly int[] _slotResult = new int[WarriorsPhase2Director.SlotCount];

        private readonly int[] _noteSerial = new int[WarriorsPhase3Director.NoteCapacity];
        private readonly int[] _noteState = new int[WarriorsPhase3Director.NoteCapacity];

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

            // 소리가 안 들릴 때 원인을 가른다 — **재생 요청이 아예 없는지**, 요청은 갔는데 안 들리는지.
            // AUDIO.md 8장이 켜라는 AudioHub.logPlays 는 허브가 런타임에 스스로 생겨 빌드에서 켤 길이 없다.
            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (arg != "-audiolog") continue;

                _hub.logPlays = true;
                Debug.Log("[WarriorsAudio] -audiolog — 재생 요청을 전부 로그로 찍습니다.", this);
                break;
            }

            // 이름은 "게임.용도" 로. 다른 미니게임의 루프와 겹치면 서로 클립을 빼앗는다.
            _wave = _hub.Loop("warriors.wave", waveLoop, 1.2f);
            _kraken = _hub.Loop("warriors.kraken", krakenLoop, 1.0f);
        }

        private void OnEnable()
        {
            // **내 검 소리는 로컬 사건으로 낸다.** 서버가 인정한 번호(AttackSeq)를 기다리면 왕복 시간만큼
            // 늦게 들려 손맛이 죽는다. HUD 의 키캡 펄스도 같은 신호를 쓴다(WarriorsHudPresenter).
            WarriorsInputProvider.LocalSwing += HandleLocalSwing;
        }

        private void OnDisable()
        {
            WarriorsInputProvider.LocalSwing -= HandleLocalSwing;
        }

        private void OnDestroy()
        {
            // 씬을 떠난다. 루프는 끄고 음악은 페이드 아웃 — 다음 씬의 SceneMusic 이 새 곡을 올린다.
            if (_hub == null) return;

            _hub.StopAllLoops();
            _hub.StopMusic(crossfadeSeconds);
        }

        private void Start()
        {
            _quietUntil = Time.time + quietSecondsOnJoin;
            Rescan(force: true);

            int clips = 0;
            foreach (AudioClip c in new[]
                     {
                         bgmWaiting, bgmRound1, bgmRound2, bgmRound3, stingerClear, stingerFail,
                         waveLoop, krakenLoop,
                         swingHorizontal, swingVertical, swingThrust, monsterHit, comboUp, monsterKill,
                         tentacleHit, tentacleCut, finishWindow, comboSet,
                         noteHitHorizontal, noteHitVertical, noteHitThrust, noteMiss, krakenHurt, krakenRoar,
                         playerHurt, playerDown, roundChange, countdownTick, countdownGo,
                     })
            {
                if (c != null) clips++;
            }

            Debug.Log($"[WarriorsAudio] 시작 — 클립 {clips}개 · 판 {(_match != null)} · 내 캐릭터 {(_me != null)}", this);
        }

        // ------------------------------------------------------------
        // 찾기 — 판과 내 캐릭터는 늦게 생긴다
        // ------------------------------------------------------------

        private void Rescan(bool force)
        {
            if (!force && Time.time < _nextRescan) return;
            _nextRescan = Time.time + RescanSeconds;

            _match = WarriorsMatchState.Current;

            // 내 캐릭터가 바뀌는 일은 없지만, 판이 다시 서면 새로 생긴다. 없을 때만 찾는다.
            if (_me != null && _me.IsLive) return;

            _me = null;
            _myCombat = null;
            _lane = -1;

            foreach (WarriorsPlayerLife one in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // ⚠ **내 캐릭터만** 본다. 남의 복사본으로 소리를 내면 남이 맞을 때 내 화면에서 소리가 난다.
                if (one == null || !one.IsLive || !one.HasInputAuthority) continue;

                _me = one;
                _myCombat = one.GetComponent<WarriorsNetPlayerCombat>();
                _lane = one.PlayerIndex;

                // 지금 값에서 시작한다. 늦게 들어왔다고 지나간 피해가 한꺼번에 울리면 안 된다.
                _hpSeen = one.Hp;
                _downSeen = one.IsDown;
                _comboSeen = _myCombat != null ? _myCombat.Combo : 0;
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

            // ⚠ 참조가 남아 있어도 네트워크 판이 이미 내려갔을 수 있다(로비로 돌아가며 Runner 가 닫힐 때).
            //    그때 Phase 를 읽으면 Fusion 이 "Spawned() 전에는 읽을 수 없다" 예외를 던진다.
            if (_match == null || _match.Object == null || !_match.Object.IsValid)
            {
                // 네트워크 판이 아직 없거나(부트 중) 이미 끝났거나, 1인 검증 씬이다. 조용히 기다린다.
                DriveLoops(WarriorsMatchPhase.Waiting);
                return;
            }

            WarriorsMatchPhase phase = _match.Phase;

            WatchCountdown(phase);
            WatchPhase(phase);
            WatchKills();
            WatchMyCombat();
            WatchMyLife();
            WatchTentacles();
            WatchNotes();

            ChooseMusic(phase);
            DriveLoops(phase);
        }

        // ------------------------------------------------------------
        // 판 진행
        // ------------------------------------------------------------

        /// <summary>시작 카운트다운 3 · 2 · 1. 1초마다 한 번씩만 낸다.</summary>
        private void WatchCountdown(WarriorsMatchPhase phase)
        {
            if (phase != WarriorsMatchPhase.Countdown)
            {
                _countdownSeen = -1;
                return;
            }

            int left = Mathf.CeilToInt(_match.Countdown);

            if (left == _countdownSeen) return;

            bool first = _countdownSeen < 0;
            _countdownSeen = left;

            if (!first && left >= 1 && left <= 3) Play(countdownTick);
        }

        /// <summary>라운드가 바뀐 순간 · 판이 끝난 순간.</summary>
        private void WatchPhase(WarriorsMatchPhase phase)
        {
            if (!_phaseInit)
            {
                _phaseInit = true;
                _phaseSeen = phase;
                return;
            }

            if (phase == _phaseSeen) return;

            WarriorsMatchPhase was = _phaseSeen;
            _phaseSeen = phase;

            switch (phase)
            {
                case WarriorsMatchPhase.Phase1:
                    // 카운트다운이 끝나 판이 열리는 순간만 "시작" 이다. 다시 선 판은 그냥 라운드 전환음.
                    Play(was == WarriorsMatchPhase.Countdown ? countdownGo : roundChange);
                    break;

                case WarriorsMatchPhase.Phase2:
                    Play(roundChange);
                    break;

                case WarriorsMatchPhase.Phase3:
                    // 최종 형태가 일어선다. 포효로 라운드를 연다.
                    Play(roundChange);
                    Play(krakenRoar, krakenLevel);
                    break;

                case WarriorsMatchPhase.Cleared:
                    PlayStinger(stingerClear);
                    break;

                case WarriorsMatchPhase.Failed:
                    PlayStinger(stingerFail);
                    break;
            }

            // 판이 다시 서면 스팅어를 다시 낼 수 있어야 한다.
            if (phase < WarriorsMatchPhase.Cleared) _stingerPlayed = false;
        }

        /// <summary>몬스터 처치. 팀 합산이라 두 사람 모두에게 들린다.</summary>
        private void WatchKills()
        {
            int kills = _match.Phase1Kills;

            if (_killsSeen < 0) { _killsSeen = kills; return; }
            if (kills <= _killsSeen) { _killsSeen = kills; return; }

            _killsSeen = kills;
            Play(monsterKill, killLevel);
        }

        // ------------------------------------------------------------
        // 나
        // ------------------------------------------------------------

        /// <summary>내가 휘두른 순간. 로컬 사건이라 지연이 없다.</summary>
        private void HandleLocalSwing(WarriorsAttackDirection direction)
        {
            switch (direction)
            {
                case WarriorsAttackDirection.HorizontalSlash: Play(swingHorizontal, swingLevel); break;
                case WarriorsAttackDirection.VerticalSlash: Play(swingVertical, swingLevel); break;
                case WarriorsAttackDirection.Thrust: Play(swingThrust, swingLevel); break;
            }
        }

        /// <summary>
        /// 내 콤보가 늘면 <b>내가 맞힌 것</b>이다. 한 번에 셋을 베면 3이 오른다 — 그래도 소리는 한 번만 낸다.
        ///
        /// 콤보는 헛치거나 맞거나 3초가 지나면 0 으로 떨어진다. 줄어드는 것은 소리를 내지 않는다.
        /// </summary>
        private void WatchMyCombat()
        {
            // 내 캐릭터가 판보다 먼저 내려갈 수 있다. 내려간 뒤 Combo 를 읽으면 Fusion 예외다.
            if (_myCombat == null || _myCombat.Object == null || !_myCombat.Object.IsValid) return;

            int combo = _myCombat.Combo;

            if (_comboSeen < 0) { _comboSeen = combo; return; }
            if (combo <= _comboSeen) { _comboSeen = combo; return; }

            int was = _comboSeen;
            _comboSeen = combo;

            Play(monsterHit, monsterLevel);

            // 3 · 6 · 9 … 를 넘어설 때 한 번 더. 한 번에 여러 마리를 베어 두 단계를 건너뛰어도 한 번이다.
            if (combo / 3 > was / 3) Play(comboUp);
        }

        /// <summary>내 HP 가 줄었을 때 · 내가 쓰러졌을 때.</summary>
        private void WatchMyLife()
        {
            if (_me == null || !_me.IsLive) return;

            int hp = _me.Hp;

            if (_hpSeen < 0) _hpSeen = hp;
            else if (hp < _hpSeen) Play(playerHurt);

            _hpSeen = hp;

            bool down = _me.IsDown;

            if (down && !_downSeen) Play(playerDown);
            _downSeen = down;
        }

        // ------------------------------------------------------------
        // 2라운드 — 촉수
        // ------------------------------------------------------------

        /// <summary>
        /// 내 촉수가 맞고 · 잘리는 순간. 남의 촉수는 소리를 내지 않는다 — 두 사람이 동시에 베면
        /// 내 화면에서 소리가 두 겹으로 나 누가 벤 것인지 읽히지 않는다.
        /// </summary>
        private void WatchTentacles()
        {
            WarriorsPhase2Director tentacles = WarriorsPhase2Director.Current;

            if (tentacles == null || tentacles.Object == null || !tentacles.Object.IsValid) return;

            for (int i = 0; i < WarriorsPhase2Director.SlotCount; i++)
            {
                WarriorsTentacleSlot slot = tentacles.Slots[i];

                // 같은 자리에 새 촉수가 올라왔다. 앞 촉수의 값과 견주면 안 된다.
                if (slot.Serial != _slotSerial[i])
                {
                    _slotSerial[i] = slot.Serial;
                    _slotHitsLeft[i] = slot.HitsLeft;
                    _slotResult[i] = slot.Result;
                    continue;
                }

                bool mine = slot.Owner == _lane && _lane >= 0;

                // 아직 안 잘렸는데 남은 타격 수가 줄었다 = 정타 한 대.
                if (slot.HitsLeft < _slotHitsLeft[i] && slot.Result == 0 && mine) Play(tentacleHit);
                _slotHitsLeft[i] = slot.HitsLeft;

                // 0 진행 중 · 1 잘렸다 · 2 시간을 놓쳐 반격당했다.
                // 반격은 따로 소리를 두지 않는다 — HP 가 줄어 playerHurt 가 난다.
                if (slot.Result != _slotResult[i])
                {
                    if (slot.Result == 1 && mine) Play(tentacleCut);
                    _slotResult[i] = slot.Result;
                }
            }

            // 마무리 창이 **나에게** 열리는 순간. 열려 있는 동안 계속 울리면 안 되니 가장자리만 본다.
            bool open = _lane >= 0 && tentacles.FinishWindowOpenFor(_lane);

            if (open && !_windowOpenSeen) Play(finishWindow);
            _windowOpenSeen = open;

            // 협동 세트는 두 사람이 함께 만든 것이라 양쪽 모두에게 낸다.
            int set = tentacles.ComboSetSerial;

            if (_comboSetSeen < 0) _comboSetSeen = set;
            else if (set != _comboSetSeen) { _comboSetSeen = set; Play(comboSet); }
        }

        // ------------------------------------------------------------
        // 3라운드 — 리듬
        // ------------------------------------------------------------

        /// <summary>
        /// 그 노트를 어떤 공격으로 받았는지에 따라 음을 고른다 — **가로 도 · 세로 레 · 찌르기 미.**
        /// 화살표가 음계로 읽혀 박자감이 생긴다.
        /// </summary>
        private AudioClip NoteClipFor(int type) => (WarriorsAttackDirection)type switch
        {
            WarriorsAttackDirection.HorizontalSlash => noteHitHorizontal,
            WarriorsAttackDirection.VerticalSlash => noteHitVertical,
            WarriorsAttackDirection.Thrust => noteHitThrust,
            _ => noteHitHorizontal,
        };

        /// <summary>노트 정타 · 미스 · 크라켄 피격 · 콤보 피니시.</summary>
        private void WatchNotes()
        {
            WarriorsPhase3Director rhythm = WarriorsPhase3Director.Current;

            if (rhythm == null || rhythm.Object == null || !rhythm.Object.IsValid) return;

            // **정타.** 서버가 올려 준 번호가 바뀌면 누군가 받아 낸 것이다.
            // 크라켄이 맞는 소리는 팀 공통, 노트를 받아 낸 소리는 내 레인만.
            if (rhythm.HitSerial != _hitSerialSeen)
            {
                bool first = _hitSerialSeen < 0;
                _hitSerialSeen = rhythm.HitSerial;

                if (!first)
                {
                    // 크라켄이 맞는 소리만 여기서. **내 노트 소리는 아래 칸 훑기에서 낸다** —
                    // 거기서만 그 노트를 어떤 공격으로 받았는지(Type) 알 수 있어 음을 고를 수 있다.
                    bool strong = rhythm.HitStrength == 2;
                    Play(krakenHurt, krakenLevel * (strong ? 1f : 0.8f));
                }
            }

            // **미스.** 노트 한 칸의 상태가 2 로 바뀐 순간. 내 레인만.
            for (int i = 0; i < WarriorsPhase3Director.NoteCapacity; i++)
            {
                WarriorsNoteSlot note = rhythm.Notes[i];

                if (note.Serial != _noteSerial[i])
                {
                    _noteSerial[i] = note.Serial;
                    _noteState[i] = note.State;
                    continue;
                }

                if (note.State == _noteState[i]) continue;

                int was = _noteState[i];
                _noteState[i] = note.State;

                // 0 떨어지는 중 · 1 성공 · 2 실패. 내 레인만 낸다.
                if (was != 0 || note.Lane != _lane || _lane < 0) continue;

                if (note.State == 2) Play(noteMiss, noteLevel);
                else if (note.State == 1) Play(NoteClipFor(note.Type), noteLevel);
            }

            // **콤보 피니시.** 묶음을 다 받아 낸 보상음. 둘이 함께면 조금 더 크게.
            //
            // ⚠ 여기서 krakenRoar 를 내지 않는다. 포효는 **3라운드가 열릴 때 한 번**이라야 무게가 산다 —
            //   피니시마다 울리면 몇 초 간격으로 반복돼 금세 질린다. (실측 확인 2026-09-22)
            if (rhythm.FinishSerial != _finishSerialSeen)
            {
                bool first = _finishSerialSeen < 0;
                _finishSerialSeen = rhythm.FinishSerial;

                if (!first) Play(comboSet, rhythm.FinishKind == 2 ? 1.2f : 1f);
            }
        }

        // ------------------------------------------------------------
        // 곡과 루프
        // ------------------------------------------------------------

        /// <summary>
        /// 상태에 맞는 곡. 매 프레임 불러도 된다 — 같은 곡이면 허브가 그대로 이어 간다.
        ///
        /// 결과 화면(<c>Cleared</c> · <c>Failed</c>)은 <b>조용히 둔다.</b> 스팅어 하나만 나고 음악은 멈춘다.
        /// </summary>
        private void ChooseMusic(WarriorsMatchPhase phase)
        {
            switch (phase)
            {
                case WarriorsMatchPhase.Waiting:
                case WarriorsMatchPhase.Countdown:
                    _hub.PlayMusic(bgmWaiting, crossfadeSeconds);
                    break;

                case WarriorsMatchPhase.Phase1:
                    _hub.PlayMusic(bgmRound1, crossfadeSeconds);
                    break;

                case WarriorsMatchPhase.Phase2:
                    _hub.PlayMusic(bgmRound2, crossfadeSeconds);
                    break;

                case WarriorsMatchPhase.Phase3:
                    _hub.PlayMusic(bgmRound3, crossfadeSeconds);
                    break;

                default:
                    _hub.StopMusic(crossfadeSeconds);
                    break;
            }
        }

        /// <summary>루프는 목표만 정한다. 켜고 · 키우고 · 끄는 것은 허브가 한다.</summary>
        private void DriveLoops(WarriorsMatchPhase phase)
        {
            // 파도는 대기부터 결과까지 **같은 크기로 잔잔하게** 깐다. 예전에는 1 · 2라운드만 키우고
            // 나머지는 35% 로 줄였는데, 라운드가 바뀔 때마다 배경이 커졌다 작아져 어수선했다.
            if (_wave != null) _wave.Target = loopLevel;
            if (_kraken != null) _kraken.Target = (phase == WarriorsMatchPhase.Phase3 ? 1f : 0f) * loopLevel;
        }

        // ------------------------------------------------------------
        // 내는 것
        // ------------------------------------------------------------

        /// <summary>
        /// 효과음 하나. 허브가 클립 크기를 같은 기준으로 맞춰 주므로, 여기 level 은 "남들보다 얼마나" 만 정한다.
        /// </summary>
        private void Play(AudioClip clip, float level = 1f)
        {
            if (clip == null || _hub == null) return;

            // ⚠ 접속 직후 잠깐은 내지 않는다. 서버가 쌓아 둔 값(처치 수 · 촉수 상태 · 노트)이 한꺼번에
            //    도착해 "바뀐 순간" 으로 잡히면, 들어오자마자 소리가 우르르 난다.
            if (Time.time < _quietUntil) return;

            _hub.PlayOneShot(clip, sfxLevel * level);
        }

        /// <summary>결과 스팅어. 판마다 한 번이다. 배경음악은 여기서 멈춘다.</summary>
        private void PlayStinger(AudioClip clip)
        {
            if (_stingerPlayed) return;

            _stingerPlayed = true;
            _hub.StopMusic(crossfadeSeconds);

            // 스팅어는 접속 직후 억제와 무관하게 낸다 — 판이 끝나는 순간은 늦게 들어온 사람에게도 사건이다.
            if (clip != null) _hub.PlayOneShot(clip, sfxLevel);
        }
    }
}

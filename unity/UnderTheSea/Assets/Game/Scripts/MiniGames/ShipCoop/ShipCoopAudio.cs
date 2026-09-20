using System.Collections.Generic;
using UnderTheSea.Audio;
using UnityEngine;

/// <summary>
/// 🔊 배 협동의 소리 **연출가.** 게임 상태를 보고 <see cref="AudioHub"/> 에 "이 곡 · 이 소리" 를 부탁한다. (SHIPCOOP.md 4장)
///
/// <code>
///   🎵 배경음악   대기 → 항해 → (사건이 터져 있으면) 긴장 → 결과 스팅어
///   🌊 루프       바다(항상) · 바람(돌풍) · 밧줄(돛 당길 때) · 키(조타할 때) · 물(침수량만큼)
///   💥 효과음     예고 종 · 대포 · 파도 충격 · 암초 충돌/스침 · 적선 피격 · 선체 파손 · 망치 · 수리 완료 ·
///               물 버림 · 배 피격 · 침몰
/// </code>
///
/// <b>소스는 직접 들지 않는다.</b> 재생 · 페이드 · 볼륨은 전부 허브가 한다. 이 파일은 "언제 무엇을" 만 정한다.
/// 다른 미니게임도 같은 모양으로 자기 연출가를 두면 된다.
///
/// <b>서버가 복제해 준 상태를 보고 낸다.</b> <c>event Action</c> 판정 알림은 클라이언트에서 안 터지므로(11장)
/// 쓰지 않고, 매 프레임 상태를 읽어 **바뀐 순간**을 잡는다. 대포 발사만은 <c>CannonTask.Recoiled</c> 가
/// 클라이언트에서도 터지도록 만들어져 있어 그걸 듣는다.
///
/// <b>클립이 비어 있으면 그 소리만 조용히 건너뛴다.</b> 클립은 <c>Assets/Game/Audio/ShipCoop/</c> 에 필드 이름대로
/// 두면 설치 도구(ShipCoopAudioInstaller)가 채운다.
/// </summary>
[DisallowMultipleComponent]
public class ShipCoopAudio : MonoBehaviour
{
    [Header("🎵 배경음악")]
    [Tooltip("출항 전 대기.")]
    [SerializeField] private AudioClip bgmReady;

    [Tooltip("항해 중. 사건이 없을 때.")]
    [SerializeField] private AudioClip bgmSailing;

    [Tooltip("사건이 하나라도 터져 있는 동안. 비워두면 항해 곡을 계속 튼다.")]
    [SerializeField] private AudioClip bgmTension;

    [Tooltip("도착(성공) 스팅어. 한 번 나고 배경음악은 멈춘다.")]
    [SerializeField] private AudioClip stingerClear;

    [Tooltip("침몰 · 시간 초과 스팅어.")]
    [SerializeField] private AudioClip stingerFail;

    [Tooltip("곡을 바꿀 때 겹치는 시간 (초).")]
    [SerializeField, Range(0.1f, 5f)] private float crossfadeSeconds = 1.5f;

    [Tooltip("긴장 곡으로 넘어가기 전에 사건이 이만큼 이어져야 한다 (초). 짧은 사건마다 곡이 널뛰지 않게.")]
    [SerializeField, Range(0f, 5f)] private float tensionDelay = 1f;

    [Header("🌊 루프")]
    [Tooltip("바다. 항해 중 항상. 속도가 빠를수록 조금 커진다.")]
    [SerializeField] private AudioClip seaLoop;

    [Tooltip("바람. 돌풍이 부는 동안.")]
    [SerializeField] private AudioClip windLoop;

    [Tooltip("밧줄 삐걱임. 누가 돛을 당기거나 푸는 동안.")]
    [SerializeField] private AudioClip ropeLoop;

    [Tooltip("키 돌리는 나무 소리. 조타 입력이 있는 동안.")]
    [SerializeField] private AudioClip wheelLoop;

    [Tooltip("물 차는 소리. 침수량만큼 커진다.")]
    [SerializeField] private AudioClip floodLoop;

    [Tooltip("루프의 기본 크기 (0~1). 효과음 볼륨이 곱해진다.")]
    [SerializeField, Range(0f, 1f)] private float loopLevel = 0.5f;

    [Header("💥 효과음 — 사건")]
    [Tooltip("사건 예고가 뜰 때 (종 · 북).")]
    [SerializeField] private AudioClip warnChime;

    [Tooltip("큰 파도가 배를 때리는 순간.")]
    [SerializeField] private AudioClip waveHit;

    [Tooltip("암초에 부딪힘.")]
    [SerializeField] private AudioClip reefHit;

    [Tooltip("암초를 스쳐 지나감 (안도).")]
    [SerializeField] private AudioClip reefDodged;

    [Tooltip("적선에 포탄이 맞음.")]
    [SerializeField] private AudioClip enemyHit;

    [Tooltip("선체가 부서짐 (파손 시작).")]
    [SerializeField] private AudioClip hullCrack;

    [Header("💥 효과음 — 작업")]
    [Tooltip("대포 발사.")]
    [SerializeField] private AudioClip cannonFire;

    [Tooltip("망치 한 번.")]
    [SerializeField] private AudioClip hammerHit;

    [Tooltip("수리 완료.")]
    [SerializeField] private AudioClip repairDone;

    [Tooltip("물을 뱃전에 버림.")]
    [SerializeField] private AudioClip dumpSplash;

    [Header("💥 효과음 — 배")]
    [Tooltip("배 HP 가 깎일 때.")]
    [SerializeField] private AudioClip shipHurt;

    [Tooltip("침몰.")]
    [SerializeField] private AudioClip shipSunk;

    [Tooltip("효과음의 기본 크기 (0~1). 효과음 볼륨이 곱해진다.")]
    [SerializeField, Range(0f, 1f)] private float sfxLevel = 0.9f;

    // ------------------------------------------------------------

    private AudioHub _hub;
    private AudioHub.LoopHandle _sea, _wind, _rope, _wheel, _flood;

    private ShipCoopGame _game;
    private ShipVoyage _voyage;
    private ShipHealth _health;
    private ShipFlooding _flooding;
    private SailTask _sail;
    private HelmTask _helm;
    private CannonTask _cannon;
    private VoyageEvent[] _events = System.Array.Empty<VoyageEvent>();

    private readonly Dictionary<VoyageEvent, VoyageEvent.Stage> _stageSeen = new Dictionary<VoyageEvent, VoyageEvent.Stage>();
    private readonly Dictionary<RepairTask, int> _repairHits = new Dictionary<RepairTask, int>();
    private readonly HashSet<RepairTask> _repairDone = new HashSet<RepairTask>();
    private readonly Dictionary<WaterDumpPoint, int> _dumpSeen = new Dictionary<WaterDumpPoint, int>();
    private readonly Dictionary<EnemyShip, int> _enemyHits = new Dictionary<EnemyShip, int>();
    private readonly Dictionary<Reef, bool> _reefTold = new Dictionary<Reef, bool>();
    private readonly List<RepairTask> _repairKeys = new List<RepairTask>();
    private readonly List<WaterDumpPoint> _dumpKeys = new List<WaterDumpPoint>();

    private ShipCoopState _stateSeen = ShipCoopState.Ready;
    private bool _stateInit;
    private bool _musicStopped;   // 결과 뒤. 스팅어만 남긴다
    private float _hpSeen = -1f;
    private float _tensionSince = -1f;
    private float _nextRescan;

    private void Awake()
    {
        _hub = AudioHub.Instance;

        if (_hub == null || !_hub.CanHear)
        {
            enabled = false;   // 서버, 또는 종료 중
            return;
        }

        _sea = _hub.Loop("shipcoop.sea", seaLoop, 1.2f);
        _wind = _hub.Loop("shipcoop.wind", windLoop, 0.8f);
        _rope = _hub.Loop("shipcoop.rope", ropeLoop, 0.3f);
        _wheel = _hub.Loop("shipcoop.wheel", wheelLoop, 0.3f);
        _flood = _hub.Loop("shipcoop.flood", floodLoop, 1f);
    }

    private void Start()
    {
        Rescan(force: true);
    }

    private void OnDisable()
    {
        if (_cannon != null)
        {
            _cannon.Recoiled -= OnCannonFired;
        }

        // 씬을 떠난다. 루프는 끄고 음악은 페이드 아웃 — 다음 씬의 SceneMusic 이 새 곡을 올린다.
        if (_hub != null)
        {
            _hub.StopAllLoops();
            _hub.StopMusic(crossfadeSeconds);
        }
    }

    // ------------------------------------------------------------
    // 찾기 — 수리 지점은 게임 중 생기고 사라지니 가끔 다시 찾는다
    // ------------------------------------------------------------

    private void Rescan(bool force)
    {
        if (!force && Time.time < _nextRescan)
        {
            return;
        }

        _nextRescan = Time.time + 1f;

        if (_game == null) _game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        if (_voyage == null) _voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        if (_health == null) _health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        if (_flooding == null) _flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        if (_sail == null) _sail = FindAnyObjectByType<SailTask>(FindObjectsInactive.Include);
        if (_helm == null) _helm = FindAnyObjectByType<HelmTask>(FindObjectsInactive.Include);

        if (_cannon == null)
        {
            _cannon = FindAnyObjectByType<CannonTask>(FindObjectsInactive.Include);

            if (_cannon != null)
            {
                _cannon.Recoiled += OnCannonFired;
            }
        }

        if (_events.Length == 0 || force)
        {
            _events = FindObjectsByType<VoyageEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        foreach (RepairTask repair in FindObjectsByType<RepairTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_repairHits.ContainsKey(repair))
            {
                _repairHits[repair] = repair.Hits;
                if (repair.IsRepaired) _repairDone.Add(repair);
            }
        }

        foreach (WaterDumpPoint dump in FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_dumpSeen.ContainsKey(dump))
            {
                _dumpSeen[dump] = dump.DumpCount;
            }
        }
    }

    // ------------------------------------------------------------
    // 매 프레임 — 바뀐 것을 잡는다
    // ------------------------------------------------------------

    private void Update()
    {
        Rescan(force: false);

        WatchGame();
        WatchEvents();
        WatchTasks();
        WatchShip();

        ChooseMusic();
        DriveLoops();
    }

    private void WatchGame()
    {
        if (_game == null)
        {
            return;
        }

        ShipCoopState state = _game.State;

        if (!_stateInit)
        {
            _stateInit = true;
            _stateSeen = state;
            return;
        }

        if (state == _stateSeen)
        {
            return;
        }

        _stateSeen = state;

        switch (state)
        {
            case ShipCoopState.Cleared:
                _musicStopped = true;
                Play(stingerClear);
                break;

            case ShipCoopState.Sunk:
                _musicStopped = true;
                Play(shipSunk);
                Play(stingerFail);
                break;

            case ShipCoopState.TimeOver:
                _musicStopped = true;
                Play(stingerFail);
                break;

            default:
                // 대기 · 항해로 돌아왔다 — 판을 되돌린 것. 음악을 다시 튼다.
                _musicStopped = false;
                break;
        }
    }

    private void WatchEvents()
    {
        bool anyRunning = false;

        for (int i = 0; i < _events.Length; i++)
        {
            VoyageEvent e = _events[i];
            if (e == null) continue;

            VoyageEvent.Stage now = e.CurrentStage;
            anyRunning |= now == VoyageEvent.Stage.Running;

            if (!_stageSeen.TryGetValue(e, out VoyageEvent.Stage was))
            {
                was = VoyageEvent.Stage.None;
            }

            if (now != was)
            {
                _stageSeen[e] = now;

                if (now == VoyageEvent.Stage.Warning)
                {
                    Play(warnChime);
                }

                if (now == VoyageEvent.Stage.Running)
                {
                    switch (e)
                    {
                        case BigWave _: Play(waveHit); break;
                        case HullDamage _: Play(hullCrack); break;
                    }

                    if (e is Reef reef) _reefTold[reef] = false;
                }

                if (now == VoyageEvent.Stage.None && e is Reef ended)
                {
                    TellReef(ended);
                }
            }

            WatchEventExtra(e);
        }

        // 긴장 곡 — 사건이 이어진 뒤에만.
        if (anyRunning)
        {
            if (_tensionSince < 0f) _tensionSince = Time.time;
        }
        else
        {
            _tensionSince = -1f;
        }
    }

    /// <summary>단계가 안 바뀌어도 값이 바뀌는 것들 — 적선 피격 수, 암초 판정.</summary>
    private void WatchEventExtra(VoyageEvent e)
    {
        switch (e)
        {
            case EnemyShip enemy:
            {
                _enemyHits.TryGetValue(enemy, out int seen);

                if (enemy.Hits > seen)
                {
                    Play(enemyHit);
                }

                _enemyHits[enemy] = enemy.Hits;
                break;
            }

            case Reef reef:
                // 판정은 사건이 끝나기 전에 복제되어 올 수 있다. 오면 바로 낸다.
                if (reef.IsActive && (reef.WasHit || reef.WasDodged))
                {
                    TellReef(reef);
                }
                break;
        }
    }

    private void TellReef(Reef reef)
    {
        if (_reefTold.TryGetValue(reef, out bool told) && told)
        {
            return;
        }

        if (reef.WasHit)
        {
            Play(reefHit);
            _reefTold[reef] = true;
        }
        else if (reef.WasDodged)
        {
            Play(reefDodged);
            _reefTold[reef] = true;
        }
    }

    private void WatchTasks()
    {
        // 망치질 · 수리 완료. 순회 중 사전을 바꾸지 않으려고 키를 복사한다.
        _repairKeys.Clear();
        _repairKeys.AddRange(_repairHits.Keys);

        foreach (RepairTask repair in _repairKeys)
        {
            if (repair == null)
            {
                _repairHits.Remove(repair);
                continue;
            }

            int seen = _repairHits[repair];

            if (repair.Hits > seen)
            {
                Play(hammerHit);
            }

            _repairHits[repair] = repair.Hits;

            if (repair.IsRepaired && !_repairDone.Contains(repair))
            {
                _repairDone.Add(repair);
                Play(repairDone);
            }
        }

        // 물 버림.
        _dumpKeys.Clear();
        _dumpKeys.AddRange(_dumpSeen.Keys);

        foreach (WaterDumpPoint dump in _dumpKeys)
        {
            if (dump == null)
            {
                _dumpSeen.Remove(dump);
                continue;
            }

            if (dump.DumpCount > _dumpSeen[dump])
            {
                Play(dumpSplash);
            }

            _dumpSeen[dump] = dump.DumpCount;
        }
    }

    private void WatchShip()
    {
        if (_health == null)
        {
            return;
        }

        float hp = _health.CurrentHp;

        if (_hpSeen >= 0f && hp < _hpSeen - 0.5f && hp > 0f)
        {
            Play(shipHurt);
        }

        _hpSeen = hp;
    }

    private void OnCannonFired()
    {
        Play(cannonFire);
    }

    // ------------------------------------------------------------
    // 🎵 곡 고르기 — 재생은 허브가
    // ------------------------------------------------------------

    private void ChooseMusic()
    {
        AudioClip want = null;

        if (!_musicStopped && _game != null)
        {
            switch (_game.State)
            {
                case ShipCoopState.Ready:
                    want = bgmReady != null ? bgmReady : bgmSailing;
                    break;

                case ShipCoopState.Sailing:
                    bool tense = bgmTension != null && _tensionSince >= 0f && Time.time - _tensionSince >= tensionDelay;
                    want = tense ? bgmTension : bgmSailing;
                    break;
            }
        }

        if (want != null)
        {
            _hub.PlayMusic(want, crossfadeSeconds);
        }
        else if (_hub.CurrentMusic != null)
        {
            _hub.StopMusic(crossfadeSeconds);
        }
    }

    // ------------------------------------------------------------
    // 🌊 루프 — 목표만 정한다. 페이드는 허브가
    // ------------------------------------------------------------

    private void DriveLoops()
    {
        bool sailing = _game != null && _game.State == ShipCoopState.Sailing;

        // 바다 — 항해 중 늘. 속도가 빠르면 조금 크게.
        float speed01 = _voyage != null ? Mathf.Clamp01(Mathf.Abs(_voyage.Speed) / 5f) : 0.5f;
        _sea.Target = (sailing ? Mathf.Lerp(0.6f, 1f, speed01) : 0.3f) * loopLevel;

        // 바람 — 돌풍이 부는 동안.
        _wind.Target = (_voyage != null && _voyage.SquallBlowing ? 1f : 0f) * loopLevel;

        // 밧줄 — 당기거나 푸는 동안.
        _rope.Target = (_sail != null && Mathf.Abs(_sail.Pull) > 0.05f ? 1f : 0f) * loopLevel;

        // 키 — 조타 입력이 있는 동안.
        _wheel.Target = (_helm != null && Mathf.Abs(_helm.Steer) > 0.05f ? 1f : 0f) * loopLevel;

        // 물 — 침수량만큼.
        _flood.Target = (_flooding != null ? _flooding.Level01 : 0f) * loopLevel;
    }

    private void Play(AudioClip clip)
    {
        _hub.PlayOneShot(clip, sfxLevel);
    }
}

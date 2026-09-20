using System.Collections.Generic;
using UnderTheSea.Audio;
using UnityEngine;

/// <summary>
/// 🔊 배 협동의 소리 **연출가.** 게임 상태를 보고 <see cref="AudioHub"/> 에 "이 곡 · 이 소리" 를 부탁한다. (SHIPCOOP.md 4장)
///
/// <code>
///   🎵 배경음악   대기 → 항해 → 결과(성공 · 실패 공통, 따로 곡을 안 두고 항해 곡을 endLevel 만큼 낮춰 이어서)
///   🌊 루프       바다(항상, 아주 얕게) · 바람(돌풍) · 밧줄(돛 당길 때) · 키(조타할 때) · 물(침수량만큼)
///   🐦 갈매기     루프 아니고 항해 중 30~40초마다 한 번, 셋을 돌아가며
///   💥 효과음     예고 종 · 대포 · 파도 충격 · 암초 충돌/스침 · 적선 피격 · 선체 파손 · 망치 · 수리 완료 ·
///               물 버림 · 배 피격 · 침몰
/// </code>
///
/// <b>효과음은 내가 하는 일에만 낸다.</b> 물 뜨기 · 버리기 · 상자 뚜껑 · 망치 · 수리 완료 · 대포 · 적선 피격 · 발걸음은
/// **내 캐릭터**가 했을 때만. 바람 · 바다 · 배경음악과 배 전체에 일어나는 사건(예고 · 파도 · 암초 · 파손 · HP · 침몰)만 공통이다.
/// 남의 망치 · 뚜껑까지 다 들리면 시끄럽고, 한 PC 에 클라 둘을 띄우면 두 겹으로 들려 헷갈린다.
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
    [Header("🎵 배경음악 — 3단계")]
    [Tooltip("출항 전 대기.")]
    [SerializeField] private AudioClip bgmReady;

    [Tooltip("항해 중.")]
    [SerializeField] private AudioClip bgmSailing;

    [Tooltip("결과 화면. 따로 곡을 두지 않는다 — 항해 곡(bgmSailing)을 그대로 이어서 endLevel 만큼만 낮춰 튼다.")]
    [SerializeField, Range(0f, 2f)] private float endLevel = 0.7f;

    [Tooltip("곡을 바꿀 때 겹치는 시간 (초).")]
    [SerializeField, Range(0.1f, 5f)] private float crossfadeSeconds = 1.5f;

    [Header("🌊 루프")]
    [Tooltip("바다. 항해 중 항상. 속도가 빠를수록 조금 커진다.")]
    [SerializeField] private AudioClip seaLoop;

    [Tooltip("바다 루프의 크기 (0~1). 들릴락 말락 하게 아주 작게 깔 때 쓴다. loopLevel 에 곱해진다.")]
    [SerializeField, Range(0f, 1f)] private float seaLevel = 0.12f;

    [Tooltip("바람. 항해 중 늘 얕게 깔리고, 돌풍이 불면 커진다.")]
    [SerializeField] private AudioClip windLoop;

    [Tooltip("평소 바람 크기 (돌풍 아닐 때, 0~1). 돌풍이면 1.")]
    [SerializeField, Range(0f, 1f)] private float windBase = 0.3f;

    [Tooltip("밧줄 삐걱임. 누가 돛을 당기거나 푸는 동안.")]
    [SerializeField] private AudioClip ropeLoop;

    [Tooltip("밧줄 소리를 클립 길이의 몇 배 간격으로 반복하나. 1 이면 바로 이어서, 2 면 소리 길이만큼 쉬고 다시.")]
    [SerializeField, Range(1f, 4f)] private float ropeRepeat = 2f;

    [Tooltip("키 돌리는 나무 소리. 조타 입력이 있는 동안.")]
    [SerializeField] private AudioClip wheelLoop;

    [Tooltip("물 차는 소리. 침수량만큼 커진다.")]
    [SerializeField] private AudioClip floodLoop;

    [Tooltip("루프의 기본 크기 (0~1). 효과음 볼륨이 곱해진다.")]
    [SerializeField, Range(0f, 1f)] private float loopLevel = 0.5f;

    [Header("🐦 갈매기 — 루프가 아니라 이따금 한 번씩")]
    [Tooltip("항해 중 돌아가며 뿌릴 갈매기 소리 셋. 순서대로 번갈아 튼다 (같은 것이 두 번 연달아 나지 않는다).")]
    [SerializeField] private AudioClip seagull1;
    [SerializeField] private AudioClip seagull2;
    [SerializeField] private AudioClip seagull3;

    [Tooltip("다음 갈매기 소리까지 최소 간격 (초).")]
    [SerializeField, Range(5f, 120f)] private float seagullMinInterval = 30f;

    [Tooltip("다음 갈매기 소리까지 최대 간격 (초). 이 사이에서 무작위로 정한다.")]
    [SerializeField, Range(5f, 120f)] private float seagullMaxInterval = 40f;

    [Tooltip("갈매기 소리 크기 (1 이 기준).")]
    [SerializeField, Range(0f, 2f)] private float seagullLevel = 0.7f;

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

    [Tooltip("양동이로 물을 뜸 (물을 집는 순간).")]
    [SerializeField] private AudioClip waterScoop;

    [Tooltip("포탄 상자 뚜껑이 열리거나 닫힐 때 (각각 한 번).")]
    [SerializeField] private AudioClip boxLid;

    [Header("💥 효과음 — 내 캐릭터")]
    [Tooltip("한 걸음. 내 캐릭터만. 남의 발소리는 안 낸다.")]
    [SerializeField] private AudioClip footstep;

    [Tooltip("이만큼 걸을 때마다 한 걸음 (m). 키 2.86 캐릭터의 보폭.")]
    [SerializeField, Range(0.3f, 3f)] private float stride = 1.3f;

    [Header("💥 효과음 — 배")]
    [Tooltip("배 HP 가 깎일 때.")]
    [SerializeField] private AudioClip shipHurt;

    [Tooltip("침몰.")]
    [SerializeField] private AudioClip shipSunk;

    [Tooltip("효과음의 기본 크기 (0~1). 효과음 볼륨이 곱해진다. 허브가 클립마다 크기를 같은 기준으로 맞춘 뒤의 값이다.")]
    [SerializeField, Range(0f, 1f)] private float sfxLevel = 0.8f;

    [Header("🔉 상대 크기 — 1 이 기준, 깔리는 소리는 작게")]
    [Tooltip("발걸음. 계속 나는 소리라 작게.")]
    [SerializeField, Range(0f, 2f)] private float footstepLevel = 0.35f;

    [Tooltip("상자 뚜껑.")]
    [SerializeField, Range(0f, 2f)] private float boxLidLevel = 0.7f;

    [Tooltip("뚜껑이 이만큼 남았을 때 닫힘 소리를 낸다 (뚜껑 진행도 0 닫힘 ~ 1 열림).\n" +
             "0 이면 딱 닿는 프레임. 소리가 스피커까지 50~100ms 걸리고 뚜껑은 끝에서 느려지므로 조금 앞서 낸다.\n" +
             "늦게 들리면 올리고, 빠르면 내린다. 0.25 는 각도로 6% 남은 시점이다.")]
    [SerializeField, Range(0f, 0.9f)] private float lidCloseAtOpen01 = 0.25f;


    [Tooltip("물 뜨기 · 버리기.")]
    [SerializeField, Range(0f, 2f)] private float waterLevel = 0.8f;

    [Tooltip("망치.")]
    [SerializeField, Range(0f, 2f)] private float hammerLevel = 0.8f;

    [Tooltip("대포 발사. 가장 큰 소리.")]
    [SerializeField, Range(0f, 2f)] private float cannonLevel = 1.2f;

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

    // 물 뜨기 — 사람마다 들고 있는 것이 바뀌는 순간. 상자 뚜껑 — 열림/닫힘이 바뀌는 순간.
    private readonly Dictionary<CarryTask, Cargo> _carrySeen = new Dictionary<CarryTask, Cargo>();
    private readonly Dictionary<AmmoBox, bool> _lidSeen = new Dictionary<AmmoBox, bool>();
    private readonly List<CarryTask> _carryKeys = new List<CarryTask>();
    private readonly List<AmmoBox> _lidKeys = new List<AmmoBox>();

    // 발걸음 — 내 캐릭터가 걸은 거리를 재서 보폭마다 한 번.
    private ShipCoopHud _hud;
    private Transform _me;
    private Vector3 _meWasAt;
    private float _walked;

    private ShipCoopState _stateSeen = ShipCoopState.Ready;
    private bool _stateInit;
    private float _hpSeen = -1f;
    private float _nextRescan;

    // 갈매기 — 다음에 낼 시각과 다음에 낼 순번 (0,1,2 를 돌아가며).
    private float _nextSeagullAt = -1f;
    private int _seagullIndex;

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
        // 밧줄은 짧은 소리를 띄어서 반복한다 — 클립 길이의 ropeRepeat 배 간격.
        _rope = _hub.Loop("shipcoop.rope", ropeLoop, 0.3f, ropeLoop != null ? ropeLoop.length * ropeRepeat : 0f);
        _wheel = _hub.Loop("shipcoop.wheel", wheelLoop, 0.3f);
        _flood = _hub.Loop("shipcoop.flood", floodLoop, 1f);
    }

    private void Start()
    {
        _quietUntil = Time.time + 2f;
        _nextSeagullAt = Time.time + Random.Range(seagullMinInterval, seagullMaxInterval);
        Rescan(force: true);

        int clips = 0;
        foreach (AudioClip c in new[] { bgmReady, bgmSailing, seaLoop, windLoop, ropeLoop, wheelLoop, floodLoop,
                                        warnChime, waveHit, reefHit, reefDodged, enemyHit, hullCrack, cannonFire, hammerHit, repairDone, dumpSplash,
                                        waterScoop, boxLid, footstep, shipHurt, shipSunk, seagull1, seagull2, seagull3 })
        {
            if (c != null) clips++;
        }

        Debug.Log(
            $"[ShipCoopAudio] 시작 — 클립 {clips}개 · 게임 {(_game != null)} · 대포 {(_cannon != null)} · 돛 {(_sail != null)} · " +
            $"HUD {(_hud != null)} · 사건 {_events.Length}개 · 바람 클립 {(windLoop != null ? windLoop.name : "없음")}", this);
        if (_hub.logPlays)
        {
            _hub.LogState("ShipCoopAudio.Start");
        }
    }


    private void OnEnable()
    {
        // 꺼졌다 켜지면 대포 신호를 다시 건다. OnDisable 에서 뗐기 때문이다.
        if (_cannon != null)
        {
            _cannon.Recoiled -= OnCannonFired;
            _cannon.Recoiled += OnCannonFired;
        }

        if (_hub != null && _hub.logPlays)
        {
            Debug.Log($"[ShipCoopAudio] 켜짐 (대포 {(_cannon != null ? _cannon.GetEntityId().ToString() : "없음")})", this);
        }
    }

    private void OnDisable()
    {
        if (_cannon != null)
        {
            _cannon.Recoiled -= OnCannonFired;
        }

        if (_hub != null && _hub.logPlays)
        {
            Debug.Log("[ShipCoopAudio] 꺼짐", this);
        }
    }

    private void OnDestroy()
    {
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

                if (_hub.logPlays)
                {
                    int cannons = FindObjectsByType<CannonTask>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                    Debug.Log($"[ShipCoopAudio] 대포 {_cannon.name}#{_cannon.GetEntityId()} 에 발사 신호를 걸었다 (발수 {_cannon.FireCount} · 씬의 대포 {cannons}개)", this);
                }
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

        // 사람은 늦게 들어올 수 있다. 상자는 씬에 고정이지만 같은 길로.
        foreach (CarryTask carry in FindObjectsByType<CarryTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_carrySeen.ContainsKey(carry))
            {
                _carrySeen[carry] = carry.Carrying;
            }
        }

        foreach (AmmoBox box in FindObjectsByType<AmmoBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_lidSeen.ContainsKey(box))
            {
                _lidSeen[box] = box.IsOpen;
            }
        }

        if (_hud == null) _hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);
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
        WatchCarry();
        WatchLids();
        WatchSteps();
        WatchShip();
        WatchSeagulls();

        ChooseMusic();
        DriveLoops();
    }

    /// <summary>
    /// 🪣 물을 **처음 뜬** 순간 — 들고 있는 것이 '물' 로 바뀔 때. 서버가 복제한 CarriedCargo 를 CarryTask 가 받는다.
    ///
    /// 바닥에 내려놨다가 다시 집는 것은 안 낸다. 클라이언트는 어디서 집었는지 모르지만, **집는 순간 손 닿는 곳에
    /// 놓인 물통(DroppedCargo · 물)이 있었는지**는 안다. 있었으면 다시 집은 것이다. 놓인 물통은 집히는 순간
    /// 사라지므로 직전 0.5초 안에 있었는지를 본다.
    /// </summary>
    private void WatchCarry()
    {
        _carryKeys.Clear();
        _carryKeys.AddRange(_carrySeen.Keys);

        foreach (CarryTask carry in _carryKeys)
        {
            if (carry == null)
            {
                _carrySeen.Remove(carry);
                continue;
            }

            // 이 사람 손 닿는 곳에 놓인 물통이 있나 — 매 프레임 기억해 둔다.
            if (NearDroppedWater(carry.transform.position))
            {
                _nearDropUntil[carry] = Time.time + 0.5f;
            }

            Cargo now = carry.Carrying;
            Cargo was = _carrySeen[carry];

            if (now != was)
            {
                _carrySeen[carry] = now;

                // ⚠ 같은 사람이 0.5초 안에 다시 '물' 이 되면 한 번으로 친다. 집기 → 놓기 → 집기가 연달아 오거나
                //    들고 있는 것이 두 경로(예측 · 복제)로 두 번 바뀌면 소리가 겹쳐 두 번 났다.
                if (now == Cargo.Water)
                {
                    _scoopAt.TryGetValue(carry, out float last);
                    _nearDropUntil.TryGetValue(carry, out float nearUntil);

                    bool rePick = Time.time < nearUntil;   // 놓인 물통을 다시 집었다

                    if (!rePick && Time.time - last >= 0.5f && IsMine(carry))
                    {
                        Play(waterScoop, waterLevel);
                        _scoopAt[carry] = Time.time;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 📦 **포탄 상자** 뚜껑이 열리거나 닫힐 때 한 번씩. 열림 상태는 StateSync.BoxOpen 으로 복제된다.
    ///
    /// ⚠ 판자 · 양동이 상자도 같은 AmmoBox 클래스라(Kind 만 다르다) 종류를 안 가리면 물을 뜰 때도
    ///    뚜껑 소리가 났다. 포탄 상자만 본다.
    /// ⚠ 열자마자 사거리를 벗어나면 "열림 → 닫힘" 이 연달아 와 두 번 난다. 0.5초 안의 연속 변화는 한 번만.
    /// </summary>
    private void WatchLids()
    {
        _lidKeys.Clear();
        _lidKeys.AddRange(_lidSeen.Keys);

        foreach (AmmoBox box in _lidKeys)
        {
            if (box == null)
            {
                _lidSeen.Remove(box);
                continue;
            }

            // 열림 — 상태가 바뀌는 순간(뚜껑이 올라가기 시작할 때).
            bool open = box.IsOpen;

            if (open != _lidSeen[box])
            {
                _lidSeen[box] = open;

                // 포탄 상자만, 그리고 **내가 그 상자 손 닿는 거리에 있을 때만.** 남이 여는 소리는 안 낸다.
                if (open && box.Kind == Cargo.Ammo && IsMineNear(box.transform.position, box.ReachRange + 1f))
                {
                    Play(boxLid, boxLidLevel);
                }
            }

            // 닫힘 — **뚜껑 연출이 실제로 닫히는 순간**(회전이 0 에 닿을 때). 상태가 바뀌는 순간에 내면
            //    뚜껑은 아직 내려오는 중이라(0.35초) 소리가 먼저 나고, 지연을 손으로 주면 연출과 어긋난다.
            //    그래서 SupplyChestLid 의 진행도를 읽어 1 → 0 에 닿는 프레임에 낸다.
            SupplyChestLid lid = box.GetComponent<SupplyChestLid>();

            if (lid != null)
            {
                float open01 = lid.Open01;
                _lidOpen01.TryGetValue(box, out float was);

                if (was > lidCloseAtOpen01 && open01 <= lidCloseAtOpen01
                    && box.Kind == Cargo.Ammo && IsMineNear(box.transform.position, box.ReachRange + 2f))
                {
                    Play(boxLid, boxLidLevel);
                }

                _lidOpen01[box] = open01;
            }
        }
    }

    /// <summary>상자마다 지난 프레임의 뚜껑 진행도. 1 → 0 에 닿는 순간을 잡는다.</summary>
    private readonly Dictionary<AmmoBox, float> _lidOpen01 = new Dictionary<AmmoBox, float>();

    /// <summary>
    /// 🦶 내 캐릭터의 발걸음. 걸은 거리를 재서 보폭(<see cref="stride"/>)마다 한 번.
    /// 남의 캐릭터는 안 낸다 — 네 명이 다 걸으면 발소리만 들린다.
    /// 배가 움직여 생기는 이동은 걷는 것이 아니므로 배 기준(자리의 부모)으로 재야 하지만,
    /// 사람은 ShipCoopRider 가 배와 함께 옮겨 두므로 월드 위치 차이에서 배의 이동을 빼는 대신
    /// **속도가 걷는 속도(0.8m/s 이상)일 때만** 센다. 배는 그보다 느리게 움직이는 일이 드물지 않아
    /// 완벽하진 않지만, 서 있을 때 발소리가 나는 일은 막는다.
    /// </summary>
    private void WatchSteps()
    {
        if (footstep == null)
        {
            return;
        }

        Transform me = _hud != null && _hud.LocalWorker != null ? _hud.LocalWorker.transform : null;

        if (me != _me)
        {
            _me = me;
            _walked = 0f;
            if (me != null) _meWasAt = me.position;
            if (_hub.logPlays) Debug.Log($"[ShipCoopAudio] 내 캐릭터 {(me != null ? me.name : "없음")} (HUD {(_hud != null)})", this);
            return;
        }

        if (me == null)
        {
            if (_hub.logPlays && Time.time >= _nextStepLog)
            {
                _nextStepLog = Time.time + 3f;
                Debug.Log($"[ShipCoopAudio] 발걸음 계측 — 내 캐릭터 없음 (HUD {(_hud != null)} · LocalWorker {(_hud != null && _hud.LocalWorker != null)})", this);
            }

            return;
        }

        Vector3 delta = me.position - _meWasAt;
        delta.y = 0f;
        _meWasAt = me.position;

        // ⚠ 프레임마다 재면 안 된다. 위치는 네트워크 틱마다만 바뀌어서, 사이 프레임은 이동 0 이다.
        //    프레임 단위로 "멈췄다" 고 보고 걸은 거리를 지우니 발소리가 한 번도 안 났다 (계측: 속도 0.00 만 찍힘).
        //    그래서 0.2초 창으로 모아 재고, 그 창의 평균 속도로 멈춤을 판단한다.
        _windowDist += delta.magnitude;
        _windowTime += Time.deltaTime;

        if (_windowTime < 0.2f)
        {
            return;
        }

        float speed = _windowDist / _windowTime;
        float moved = _windowDist;
        _windowDist = 0f;
        _windowTime = 0f;

        if (_hub.logPlays && Time.time >= _nextStepLog)
        {
            _nextStepLog = Time.time + 3f;
            Debug.Log($"[ShipCoopAudio] 발걸음 계측 — 내 캐릭터 {me.name} · 속도 {speed:F2}m/s · 걸은 거리 {_walked:F2}m · 보폭 {stride:F2}", this);
        }

        if (speed < 0.8f || speed > 12f)
        {
            _walked = 0f;   // 멈췼거나(0.8 미만) 순간이동(정위치 미끄러짐 · 스폰)이다
            return;
        }

        _walked += moved;

        if (_walked >= stride)
        {
            _walked -= stride;

            // ⚠ 앞 발소리가 끝나기 전엔 새 발소리를 안 낸다. 걷기 4m/s · 보폭 1.3m 면 0.33초마다 한 번인데
            //    클립이 그보다 길면 꼬리가 겹쳐 "두 발이 동시에" 로 들렸다. 간격의 하한 = 클립 길이.
            float minGap = Mathf.Max(footstep.length, 0.2f);

            if (Time.time - _lastStepAt >= minGap)
            {
                _lastStepAt = Time.time;
                Play(footstep, footstepLevel);
            }
        }
    }

    private float _lastStepAt = -10f;

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

        // ⚠ 배 침몰 소리(shipSunk)는 배경음악이 아니라 효과음이다. 곡 선택은 ChooseMusic() 이
        //    _game.State 를 직접 보고 정하므로 여기서는 상태별 "그 순간에만 나는" 효과음만 낸다.
        if (state == ShipCoopState.Sunk)
        {
            Play(shipSunk);
        }
    }

    private void WatchEvents()
    {
        for (int i = 0; i < _events.Length; i++)
        {
            VoyageEvent e = _events[i];
            if (e == null) continue;

            VoyageEvent.Stage now = e.CurrentStage;

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
    }

    /// <summary>단계가 안 바뀌어도 값이 바뀌는 것들 — 적선 피격 수, 암초 판정.</summary>
    private void WatchEventExtra(VoyageEvent e)
    {
        switch (e)
        {
            case EnemyShip enemy:
            {
                _enemyHits.TryGetValue(enemy, out int seen);

                // 내가 대포에 붙어 있을 때만 — 내가 쏜 것이 맞은 소리다.
                if (enemy.Hits > seen && IsMineAt(_cannon))
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

            // 내가 그 파손 지점에 붙어 있을 때만 — 내 망치질이다.
            bool mine = IsMineAt(repair);

            if (repair.Hits > seen && mine)
            {
                Play(hammerHit, hammerLevel);
            }

            _repairHits[repair] = repair.Hits;

            if (repair.IsRepaired && !_repairDone.Contains(repair))
            {
                _repairDone.Add(repair);
                if (mine) Play(repairDone);
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

            // 내가 그 뱃전 손 닿는 거리에 있을 때만 — 내가 버린 물이다.
            if (dump.DumpCount > _dumpSeen[dump] && IsMineNear(dump.transform.position, dump.ReachRange + 1f))
            {
                Play(dumpSplash, waterLevel);
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

    /// <summary>
    /// 🐦 갈매기. 루프가 아니라 **이따금 한 번씩** — 항해 중 30~40초마다, 셋을 돌아가며 (같은 것이 연달아 안 나게).
    ///
    /// 배 전체의 배경음이라 판정 없이 공통으로 낸다(IsMine 을 안 본다). 각 화면이 자기 타이머로 독립적으로
    /// 뿌리므로 여러 명이 동시에 들어도 정확히 같은 순간은 아니다 — 분위기용 소리라 맞출 필요가 없다.
    /// </summary>
    private void WatchSeagulls()
    {
        bool sailing = _game != null && _game.State == ShipCoopState.Sailing;

        if (!sailing || Time.time < _nextSeagullAt)
        {
            return;
        }

        AudioClip[] clips = { seagull1, seagull2, seagull3 };

        for (int i = 0; i < clips.Length; i++)
        {
            int idx = (_seagullIndex + i) % clips.Length;

            if (clips[idx] != null)
            {
                Play(clips[idx], seagullLevel);
                _seagullIndex = (idx + 1) % clips.Length;
                break;
            }
        }

        _nextSeagullAt = Time.time + Random.Range(seagullMinInterval, seagullMaxInterval);
    }

    // ------------------------------------------------------------
    // 🙋 "내 것" 판정 — 효과음은 내가 하는 일에만 낸다. 바람 · 바다 · 배경음악 · 배 전체 사건만 공통.
    //    한 PC 에 클라 둘을 띄우면 남의 소리가 두 겹으로 들려 헷갈렸고, 실제 플레이에서도 남의 망치 · 뚜껑까지
    //    다 들리면 시끄럽다. 내 캐릭터는 HUD 가 알고 있다 (ShipCoopLocalView 가 넣어 준다).
    // ------------------------------------------------------------

    private TaskWorker Me => _hud != null ? _hud.LocalWorker : null;

    /// <summary>이 운반이 내 것인가.</summary>
    private bool IsMine(CarryTask carry)
    {
        TaskWorker me = Me;
        return me != null && carry != null && carry.gameObject == me.gameObject;
    }

    /// <summary>내가 이 자리에 붙어 있나.</summary>
    private bool IsMineAt(TaskBase task)
    {
        TaskWorker me = Me;
        return me != null && task != null && ReferenceEquals(me.Current, task);
    }

    /// <summary>내가 이 자리에서 이 거리 안에 있나 (수평).</summary>
    private bool IsMineNear(Vector3 at, float range)
    {
        TaskWorker me = Me;
        if (me == null) return false;

        Vector3 d = me.transform.position - at;
        d.y = 0f;
        return d.sqrMagnitude <= range * range;
    }

    private void OnCannonFired()
    {
        // 내가 대포에 붙어 있을 때만.
        if (!IsMineAt(_cannon))
        {
            return;
        }

        if (_hub.logPlays)
        {
            Debug.Log($"[ShipCoopAudio] 대포 발사 신호 — 클립 {(cannonFire != null ? cannonFire.name : "없음")} · 조용히 {Time.time < _quietUntil}", this);
        }

        Play(cannonFire, cannonLevel);
    }

    // ------------------------------------------------------------
    // 🎵 곡 고르기 — 재생은 허브가
    // ------------------------------------------------------------

    /// <summary>
    /// 배경음악을 3단계로만 고른다 — 대기 · 항해 · 결과(성공 · 실패 공통). 결과에는 따로 곡을 두지 않는다.
    /// 항해 곡(<see cref="bgmSailing"/>)을 그대로 이어서 <see cref="endLevel"/> 만큼만 낮춰 튼다.
    /// 같은 클립을 같은 크기로 다시 요청하면 <c>AudioHub.PlayMusic</c> 이 값싸게 걸러 내므로 매 프레임 불러도 된다.
    /// </summary>
    private void ChooseMusic()
    {
        AudioClip want = null;
        float level = 1f;

        if (_game != null)
        {
            switch (_game.State)
            {
                case ShipCoopState.Ready:
                    want = bgmReady;
                    break;

                case ShipCoopState.Sailing:
                    want = bgmSailing;
                    break;

                case ShipCoopState.Cleared:
                case ShipCoopState.Sunk:
                case ShipCoopState.TimeOver:
                    want = bgmSailing;
                    level = endLevel;
                    break;
            }
        }

        if (want != null)
        {
            _hub.PlayMusic(want, crossfadeSeconds, level);
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
        _sea.Target = (sailing ? Mathf.Lerp(0.6f, 1f, speed01) : 0.3f) * loopLevel * seaLevel;

        // 바람 — 항해 중 늘 얕게, 돌풍이면 크게.
        bool squall = _voyage != null && _voyage.SquallBlowing;
        _wind.Target = (squall ? 1f : (sailing ? windBase : windBase * 0.5f)) * loopLevel;

        // 밧줄 — 당기거나 푸는 동안.
        _rope.Target = (_sail != null && Mathf.Abs(_sail.Pull) > 0.05f ? 1f : 0f) * loopLevel;

        // 키 — 조타 입력이 있는 동안.
        _wheel.Target = (_helm != null && Mathf.Abs(_helm.Steer) > 0.05f ? 1f : 0f) * loopLevel;

        // 물 — 침수량만큼.
        _flood.Target = (_flooding != null ? _flooding.Level01 : 0f) * loopLevel;
    }

    /// <summary>
    /// 효과음 하나. 허브가 클립 크기를 같은 기준으로 맞춰 주므로, 여기 level 은 "남들보다 얼마나" 만 정한다.
    /// 1 이 기준, 발걸음처럼 깔려야 하는 소리는 그보다 작게.
    /// </summary>
    private void Play(AudioClip clip, float level = 1f)
    {
        // ⚠ 접속 직후 2초는 낸 척만 한다. 서버가 쌓아 둔 값(물 버린 횟수 · 상자 열림 · 포탄 수)이 그때 한꺼번에
        //    도착해 "바뀐 순간" 으로 잡히면, 들어오자마자 물 튀김 · 뚜껑 소리가 우르르 난다. 실제로 그랬다.
        if (Time.time < _quietUntil)
        {
            return;
        }

        _hub.PlayOneShot(clip, sfxLevel * level);
    }

    /// <summary>이 시각까지는 효과음을 안 낸다 (접속 직후 값 맞추는 동안).</summary>
    private float _quietUntil;

    private float _nextStepLog;
    private float _windowDist;
    private float _windowTime;

    /// <summary>사람마다 마지막으로 물 뜬 소리를 낸 시각. 겹쳐 두 번 나는 것을 막는다.</summary>
    private readonly Dictionary<CarryTask, float> _scoopAt = new Dictionary<CarryTask, float>();

    /// <summary>사람마다 "손 닿는 곳에 놓인 물통이 있었다" 가 유효한 시각. 그 안에 물을 들면 다시 집은 것.</summary>
    private readonly Dictionary<CarryTask, float> _nearDropUntil = new Dictionary<CarryTask, float>();

    /// <summary>이 자리 손 닿는 곳에 놓인 물통이 있나. 클라이언트 복제본(DroppedCargo.Show)도 같은 목록에 있다.</summary>
    private static bool NearDroppedWater(Vector3 at)
    {
        IReadOnlyList<DroppedCargo> all = DroppedCargo.All;

        for (int i = 0; i < all.Count; i++)
        {
            DroppedCargo lying = all[i];

            if (lying != null && lying.Kind == Cargo.Water && lying.IsInReach(at))
            {
                return true;
            }
        }

        return false;
    }

}

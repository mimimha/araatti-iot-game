using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiniGames.Common;
using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 배의 **공유 상태**를 서버가 정하고 모두에게 보낸다.
    ///
    /// SHIPCOOP.md 11장이 "13 개를 공유해야 한다" 고 적어 둔 것들이다.
    /// <code>
    ///   진행 상태 · 지난 시간 · 페이즈 번호 · 점수
    ///   배 HP
    ///   항해 진행도
    ///   찬 물의 양 · 새는 곳 수
    ///   돛 힘 · 조타 각도 · 대포 포탄 수   (자리별 표시 값. 아래 Gauges 참고)
    /// </code>
    ///
    /// <b>왜 한 곳에 모으는가.</b> 이 값들은 전부 <b>배 한 척의 상태</b>지 사람의 상태가 아니다.
    /// 사람마다 복제하면 네 벌이 오가고, 누구 것이 진짜인지 헷갈린다.
    ///
    /// <b>클라이언트는 계산하지 않는다.</b> <c>ShipCoopGame.Update</c> 와 <c>TaskBase.Update</c> 가
    /// 이미 권위 가드로 막혀 있어, 클라이언트의 값은 가만히 초기값에 멈춰 있다.
    /// 그 멈춘 값을 여기서 서버 값으로 덮는다.
    ///
    /// ⚠ 사건(암초 · 적선 · 스콜)의 연출은 아직 여기 없다. 그건 각 사건이 씬에 놓은
    ///    물건을 켜고 끄는 일이라 따로 다룬다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopStateSync : NetworkBehaviour, global::MiniGames.Common.IMiniGameAdmissionSource
    {
        [Header("테스트용 시작 대기")]
        [Tooltip("이 인원이 모여야 카운트다운을 시작한다.")]
        [SerializeField, Min(1)] private int crewToStart = 2;

        [Header("판이 끝난 뒤")]
        [Tooltip("성공·실패 연출(ShipCoopResultView)을 보여 주는 시간. 이 뒤에 결과 판이 열린다.")]
        [SerializeField, Min(0f)] private float resultHoldSeconds = 3f;

        [Tooltip("인원이 모인 뒤 출항까지 세는 시간(초).")]
        [SerializeField, Min(1f)] private float countdownSeconds = 10f;

        /// <summary>지금 접속해 있는 인원. 대기 안내에 쓴다.</summary>
        [Networked] private int Crew { get; set; }

        /// <summary>출항까지 남은 초. 0 이면 세는 중이 아니다.</summary>
        [Networked] private float Countdown { get; set; }

        /// <summary>
        /// 이미 출항했는가.
        ///
        /// ⚠ <b>늦게 들어온 사람이 게임을 다시 시작시키면 안 된다.</b>
        ///    이 값이 켜진 뒤에는 인원이 몇이 되든 카운트다운을 다시 세지 않는다.
        ///    <c>[Networked]</c> 라 늦게 들어온 사람도 "이미 시작했다" 를 그대로 받는다.
        /// </summary>
        [Networked] private NetworkBool Sailed { get; set; }

        /// <summary>
        /// 결과가 확정된 서버 틱. 0 이면 아직 결과가 없다.
        ///
        /// <b>이 값이 시각의 기준이다.</b> 두 화면이 각자 시계를 재면 연출이 어긋난다.
        /// 서버가 정한 한 순간을 모두가 받아 거기서부터 센다.
        ///
        /// <b>한 번만 열기 위한 열쇠이기도 하다.</b> 복제된 값은 매 틱 보이므로, 클라이언트는
        /// 마지막으로 처리한 틱을 기억해 두고 <b>값이 바뀐 순간에만</b> 결과를 낸다.
        /// </summary>
        [Networked] private int ResultTick { get; set; }

        [Networked] private NetworkBool ResultClear { get; set; }

        [Networked] private int ResultScore { get; set; }

        [Networked] private float ResultPlayTime { get; set; }

        [Networked] private int Phase { get; set; }
        [Networked] private float Elapsed { get; set; }
        [Networked] private int PhaseIndex { get; set; }
        [Networked] private int Score { get; set; }
        [Networked] private float Hp { get; set; }

        /// <summary>
        /// 무적(개발자 모드)인가. **화면에 보이라고 복제한다.**
        ///
        /// 판정은 서버만 하므로 규칙에는 필요 없다. 그런데 개발자 모드 패널은 각자 자기 화면에서 그리고
        /// 자기 컴퓨터의 ShipHealth 를 읽는다 — 복제하지 않으면 **서버는 무적인데 내 화면 패널은 계속
        /// 꺼짐으로 보인다.** 0 을 눌러도 아무 일도 안 일어나는 것처럼 보였던 이유다.
        /// </summary>
        [Networked] private NetworkBool Invincible { get; set; }
        [Networked] private float Progress01 { get; set; }
        [Networked] private float Flood01 { get; set; }
        [Networked] private int Leaks { get; set; }

        /// <summary>돛 힘. 서버의 SailTask 가 바꾸고, 클라이언트의 돛 게이지가 읽는다.</summary>
        [Networked] private float SailPower01 { get; set; }

        /// <summary>
        /// 자리별 표시 값(조타 각도 · 대포 포탄 수)의 최대 개수.
        /// 수리 지점은 서버가 스폰하는 NetworkObject 라 <see cref="ShipCoopHoleSync"/> 가 따로 맡는다.
        /// </summary>
        public const int MaxTaskGauges = 8;

        /// <summary>
        /// 자리별 표시 값.
        ///
        /// <b>왜 필요한가.</b> 조타 각도 · 포탄 수는 <c>TaskBase.Work</c> 가 서버에서만 바꾼다.
        /// 복제하지 않으면 클라이언트 화면의 조타 게이지 · 조타륜 · 배 기울기 · "포탄 1/3" 이
        /// 전부 초기값에 멈춰 있어 <b>포탄을 실어도 실린 것처럼 보이지 않는다.</b>
        /// 자리는 <c>NetworkObject</c> 가 아니므로 <see cref="ShipCoopTaskIds"/> 번호로 가리킨다.
        /// </summary>
        [Networked, Capacity(MaxTaskGauges)]
        private NetworkArray<TaskGauge> Gauges { get; }

        /// <summary>보급 상자의 최대 개수. 넘는 것은 뚜껑이 복제되지 않는다.</summary>
        public const int MaxBoxes = 8;

        /// <summary>
        /// 보급 상자 뚜껑이 열려 있는가 (<see cref="AmmoBox.IsOpen"/>).
        ///
        /// 집기 판정은 서버에서만 돌아 클라이언트는 스스로 열 수 없다. 서버 값을 그대로 옮긴다.
        /// 상자는 NetworkObject 가 아니라 <b>계층 경로순</b>으로 줄을 세워 칸 번호를 정한다. (ShipCoopEventSync 와 같은 방법)
        /// </summary>
        [Networked, Capacity(MaxBoxes)]
        private NetworkArray<NetworkBool> BoxOpen { get; }

        /// <summary>경로순으로 줄 세운 보급 상자들. 모든 컴퓨터에서 같은 순서다.</summary>
        private AmmoBox[] boxes = System.Array.Empty<AmmoBox>();

        /// <summary>갑판에 놓인 물건의 최대 개수. 넘는 것은 복제되지 않는다.</summary>
        public const int MaxDropped = 16;

        /// <summary>
        /// 갑판에 놓인 물건(<see cref="DroppedCargo"/>). 서버가 놓고 서버가 줍는다.
        /// 클라이언트는 이 목록을 보고 복제본을 만들고 · 옮기고 · 치운다. 번호(<c>Id</c>)로 짝짓는다.
        /// </summary>
        [Networked, Capacity(MaxDropped)]
        private NetworkArray<DroppedSlot> Dropped { get; }

        /// <summary>뱃전의 최대 개수.</summary>
        public const int MaxDumps = 8;

        /// <summary>뱃전마다 물을 버린 횟수 (<see cref="WaterDumpPoint.DumpCount"/>). 클라이언트는 늘어난 만큼 💦 를 낸다.</summary>
        [Networked, Capacity(MaxDumps)]
        private NetworkArray<int> DumpCounts { get; }

        /// <summary>경로순으로 줄 세운 뱃전들.</summary>
        private WaterDumpPoint[] dumps = System.Array.Empty<WaterDumpPoint>();

        /// <summary>클라이언트가 만든 복제본. 번호 → 물건.</summary>
        private readonly Dictionary<int, DroppedCargo> mirrors = new Dictionary<int, DroppedCargo>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly List<int> gone = new List<int>();

        private ShipCoopGame game;
        private ShipHealth health;
        private ShipVoyage voyage;
        private ShipFlooding flooding;
        private ShipCoopHud hud;

        public override void Spawned()
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
            health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
            hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);

            boxes = FindObjectsByType<AmmoBox>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(b => PathOf(b.transform), System.StringComparer.Ordinal)
                .ToArray();

            if (boxes.Length > MaxBoxes)
            {
                Debug.LogError(
                    $"[ShipCoopStateSync] 보급 상자가 {boxes.Length}개로 상한({MaxBoxes})을 넘었습니다. " +
                    "넘은 것의 뚜껑은 복제되지 않습니다.", this);
            }

            dumps = FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(d => PathOf(d.transform), System.StringComparer.Ordinal)
                .ToArray();

            if (dumps.Length > MaxDumps)
            {
                Debug.LogError(
                    $"[ShipCoopStateSync] 뱃전이 {dumps.Length}개로 상한({MaxDumps})을 넘었습니다. " +
                    "넘은 것의 물 튀는 연출은 복제되지 않습니다.", this);
            }

            if (game == null)
            {
                Debug.LogError("[ShipCoopStateSync] ShipCoopGame 을 찾지 못했습니다. 상태가 복제되지 않습니다.", this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            UpdateStartGate();
            WriteResultWhenFinished();

            if (game != null)
            {
                Phase = (int)game.State;
                Elapsed = game.Elapsed;
                PhaseIndex = game.CurrentPhaseIndex;
                Score = game.FinalScore;
            }

            if (health != null)
            {
                Hp = health.CurrentHp;
                Invincible = health.Invincible;
            }

            if (voyage != null)
            {
                Progress01 = voyage.Progress01;
                SailPower01 = voyage.SailPower01;
            }

            if (flooding != null)
            {
                Flood01 = flooding.Level01;
                Leaks = flooding.LeakingPoints;
            }

            WriteTaskGauges();

            for (int i = 0; i < boxes.Length && i < MaxBoxes; i++)
            {
                BoxOpen.Set(i, boxes[i] != null && boxes[i].IsOpen);
            }

            for (int i = 0; i < dumps.Length && i < MaxDumps; i++)
            {
                DumpCounts.Set(i, dumps[i] != null ? dumps[i].DumpCount : 0);
            }

            WriteDropped();
        }

        /// <summary>클라이언트: 뱃전마다 버린 횟수를 옮긴다. 늘어났으면 그쪽이 물을 튀긴다.</summary>
        private void ApplyDumps()
        {
            for (int i = 0; i < dumps.Length && i < MaxDumps; i++)
            {
                if (dumps[i] != null)
                {
                    dumps[i].ShowDumped(DumpCounts.Get(i));
                }
            }
        }

        /// <summary>서버: 갑판에 놓인 물건을 칸에 적는다. 남는 칸은 비운다.</summary>
        private void WriteDropped()
        {
            IReadOnlyList<DroppedCargo> all = DroppedCargo.All;
            int slot = 0;

            for (int i = 0; i < all.Count && slot < MaxDropped; i++)
            {
                DroppedCargo lying = all[i];

                if (lying == null)
                {
                    continue;
                }

                Dropped.Set(slot++, new DroppedSlot
                {
                    Id = lying.Id,
                    Kind = (int)lying.Kind,
                    Position = lying.transform.position
                });
            }

            for (; slot < MaxDropped; slot++)
            {
                Dropped.Set(slot, default);
            }
        }

        /// <summary>클라이언트: 칸에 있는 물건은 만들거나 옮기고, 칸에서 사라진 물건은 치운다.</summary>
        private void ApplyDropped()
        {
            seen.Clear();

            for (int i = 0; i < MaxDropped; i++)
            {
                DroppedSlot slot = Dropped.Get(i);

                if (slot.Id == 0)
                {
                    continue;
                }

                seen.Add(slot.Id);

                if (mirrors.TryGetValue(slot.Id, out DroppedCargo mirror) && mirror != null)
                {
                    mirror.transform.position = slot.Position;
                    continue;
                }

                mirrors[slot.Id] = DroppedCargo.Show(slot.Id, (Cargo)slot.Kind, slot.Position);
            }

            gone.Clear();

            foreach (KeyValuePair<int, DroppedCargo> pair in mirrors)
            {
                if (!seen.Contains(pair.Key))
                {
                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; i < gone.Count; i++)
            {
                if (mirrors[gone[i]] != null)
                {
                    Destroy(mirrors[gone[i]].gameObject);
                }

                mirrors.Remove(gone[i]);
            }
        }

        /// <summary>클라이언트: 서버가 정한 뚜껑 상태를 상자에 옮긴다.</summary>
        private void ApplyBoxOpen()
        {
            for (int i = 0; i < boxes.Length && i < MaxBoxes; i++)
            {
                if (boxes[i] != null)
                {
                    boxes[i].ShowOpen(BoxOpen.Get(i));
                }
            }
        }

        /// <summary>루트부터의 이름 경로. 씬 이름은 넣지 않는다 — Fusion 이 씬 이름을 바꾼다.</summary>
        private static string PathOf(Transform target)
        {
            System.Text.StringBuilder path = new System.Text.StringBuilder(target.name);

            for (Transform step = target.parent; step != null; step = step.parent)
            {
                path.Insert(0, '/').Insert(0, step.name);
            }

            return path.ToString();
        }

        /// <summary>서버: 조타 · 대포의 표시 값을 칸에 적는다. 남는 칸은 비운다.</summary>
        private void WriteTaskGauges()
        {
            int slot = 0;
            IReadOnlyList<TaskBase> all = TaskBase.All;

            for (int i = 0; i < all.Count && slot < MaxTaskGauges; i++)
            {
                TaskGauge gauge;

                switch (all[i])
                {
                    case HelmTask helm:
                        gauge = new TaskGauge
                        {
                            Id = ShipCoopTaskIds.IdOf(helm),
                            Value = helm.Heading,
                            Extra = helm.Steer
                        };
                        break;

                    case CannonTask cannon:
                        gauge = new TaskGauge
                        {
                            Id = ShipCoopTaskIds.IdOf(cannon),
                            Count = cannon.Ammo,
                            // 쏜 발수. 클라이언트가 늘어난 만큼 반동 연출을 낸다. (CannonTask.ShowFired)
                            Extra = cannon.FireCount
                        };
                        break;

                    case SailTask sail:
                        // 돛 힘은 SailPower01 로 따로 간다. 여기는 **당기는 중인지**만 — 손 동작 연출용.
                        gauge = new TaskGauge
                        {
                            Id = ShipCoopTaskIds.IdOf(sail),
                            Value = sail.Pull
                        };
                        break;

                    default:
                        // 수리 지점은 ShipCoopHoleSync 로 간다.
                        continue;
                }

                Gauges.Set(slot++, gauge);
            }

            for (; slot < MaxTaskGauges; slot++)
            {
                Gauges.Set(slot, default);
            }
        }

        /// <summary>클라이언트: 칸에 적힌 값을 그 자리에 옮긴다. 자리가 이 화면에 아직 없으면 건너뛴다.</summary>
        private void ApplyTaskGauges()
        {
            for (int i = 0; i < MaxTaskGauges; i++)
            {
                TaskGauge gauge = Gauges.Get(i);

                if (gauge.Id == 0)
                {
                    continue;
                }

                switch (ShipCoopTaskIds.Find(gauge.Id))
                {
                    case HelmTask helm:
                        helm.ShowHeading(gauge.Value, gauge.Extra);
                        break;

                    case CannonTask cannon:
                        cannon.ShowAmmo(gauge.Count);
                        cannon.ShowFired(Mathf.RoundToInt(gauge.Extra));
                        break;

                    case SailTask sail:
                        sail.ShowPull(gauge.Value);
                        break;
                }
            }
        }

        /// <summary>
        /// **언제 출항할지 정한다.** 서버에서만 돈다. (테스트용 대기 규칙)
        ///
        /// <code>
        ///   인원 부족       기다린다
        ///   인원이 모임     10초를 센다
        ///   세는 중에 이탈  센 것을 버리고 다시 기다린다
        ///   다 셈           딱 한 번 출항한다
        /// </code>
        ///
        /// 출항한 뒤에는 아무것도 하지 않는다. 늦게 들어온 사람이 게임을 다시 시작시키면
        /// 이미 반쯤 진행된 항해가 처음으로 돌아간다.
        /// </summary>
        private void UpdateStartGate()
        {
            // 봇도 실제 작업을 맡는 승무원이다. 출항 조건에는 포함하되 세션 정원에는 포함하지 않는다.
            Crew = Runner.ActivePlayers.Count() + ShipCoopBotManager.ActiveBotCount;

            if (game == null)
            {
                return;
            }

            if (Sailed)
            {
                ResetWhenEveryoneLeft();
                return;
            }

            if (Crew < crewToStart)
            {
                if (Countdown > 0f)
                {
                    Debug.Log($"[ShipCoopStart] 인원이 {Crew}명으로 줄어 카운트다운을 취소합니다.");
                    Countdown = 0f;
                }

                return;
            }

            if (Countdown <= 0f)
            {
                Countdown = countdownSeconds;
                Debug.Log($"[ShipCoopStart] {Crew}명이 모였습니다. {countdownSeconds:F0}초 뒤 출항합니다.");
                return;
            }

            Countdown -= Runner.DeltaTime;

            if (Countdown > 0f)
            {
                return;
            }

            Countdown = 0f;
            Sailed = true;

            Debug.Log($"[ShipCoopStart] 카운트다운이 끝났습니다. 출항합니다. (인원 {Crew}명)");
            game.StartVoyage();
        }

        /// <summary>
        /// 지금 새 사람을 받아도 되는가. <c>MiniGameAdmission</c> 이 접속 요청마다 물어본다.
        ///
        /// <b>판단은 여기서 한다.</b> 공통 부품이 <c>Sailed</c> 나 <c>Sunk</c> 같은 이 게임만의
        /// 단어를 알기 시작하면 공통이 아니게 된다.
        ///
        /// <code>
        ///   출항 전            받는다   (모자라서 기다리는 중이든, 카운트다운 중이든)
        ///   출항 뒤 · 항해 중   거절     난입을 허용하면 보상 · 난이도 · 역할 분배가 흔들린다
        ///   끝났음             거절     들어와도 결과 화면만 보이고, 그 사람 때문에 리셋이 막힌다
        /// </code>
        ///
        /// ⚠ <b>"끝났음" 은 <c>ShipCoopGame.Finish()</c> 의 첫 줄에서 정해진다.</b> 점수 계산과
        ///    결과 통지보다 앞선다. 그래서 종료가 확정된 그 프레임부터 이미 거절한다 —
        ///    종료와 동시에 누가 끼어드는 틈이 없다.
        /// </summary>
        public bool CanAdmitNow(out string why)
        {
            if (game == null)
            {
                // 게임을 못 찾았다. 막을 근거가 없으니 받는다. 조용히 막히는 쪽이 더 위험하다.
                why = null;
                return true;
            }

            switch (game.State)
            {
                case ShipCoopState.Sailing:
                    why = "항해 중입니다. 출항 뒤에는 들어올 수 없습니다.";
                    return false;

                case ShipCoopState.Cleared:
                case ShipCoopState.Sunk:
                case ShipCoopState.TimeOver:
                    why = $"이미 끝난 판입니다. ({game.State}) 모두 나가면 다음 판을 받습니다.";
                    return false;

                default:
                    why = null;
                    return true;
            }
        }

        /// <summary>
        /// 판이 끝나고 <b>아무도 남지 않았으면</b> 다음 판을 받을 수 있게 되돌린다.
        ///
        /// <b>왜 필요한가.</b> <c>Sailed</c> 는 한 번 서면 다시 눕지 않는다. 늦게 들어온 사람이
        /// 반쯤 진행된 항해를 처음으로 되돌리는 것을 막으려고 그렇게 만들었다. 그런데 그 탓에
        /// <b>판이 끝나도 서버가 그 상태로 굳는다.</b> 새로 들어오면 들어오자마자 종료 화면이 뜬다.
        /// QA 때마다 Dedicated Server 를 손으로 껐다 켜야 했다.
        ///
        /// <b>왜 "아무도 없을 때" 인가.</b> 한 명이라도 남아 있으면 그 사람은 결과를 보는 중이다.
        /// 거기서 되돌리면 결과 화면을 빼앗는다. 다 나가고 나서야 판이 진짜 끝난 것이다.
        /// Lobby 자동 복귀가 붙으면 사람이 저절로 0 이 되므로 이 조건과 자연스럽게 맞물린다.
        /// </summary>
        private void ResetWhenEveryoneLeft()
        {
            if (Crew > 0)
            {
                return;
            }

            if (game.State == ShipCoopState.Ready || game.State == ShipCoopState.Sailing)
            {
                // 끝나지 않은 판이다. 사람이 없어도 되돌릴 것이 없다.
                return;
            }

            Debug.Log($"[ShipCoopStart] 판이 끝나고 아무도 남지 않았습니다. ({game.State}) 대기 상태로 되돌립니다.");

            game.ResetToWaiting();

            Sailed = false;
            Countdown = 0f;

            // ⚠ 결과도 함께 지운다. 안 지우면 **다음 판이 시작되기도 전에 지난 결과가 다시 뜬다** —
            //    새로 들어온 사람의 클라이언트가 복제된 ResultTick 을 처음 보고 결과로 받아들인다.
            ResultTick = 0;
            ResultClear = false;
            ResultScore = 0;
            ResultPlayTime = 0f;
        }

        /// <summary>내가 마지막으로 내보낸 결과의 틱. <b>내 화면에서만 쓰는 값이다.</b></summary>
        private int shownResultTick;

        private Coroutine resultRelease;

        private ShipCoopResultView resultView;

        /// <summary>
        /// 결과가 확정됐으면 <b>연출을 보여 준 뒤</b> 결과 판으로 넘긴다. 클라이언트에서만 돈다.
        ///
        /// <b>왜 한 번만 여는가.</b> 복제된 값은 매 프레임 보인다. 그대로 두면 결과 판이 매 프레임
        /// 다시 열린다. 그래서 <b>마지막으로 처리한 틱을 기억</b>하고 값이 바뀐 순간에만 움직인다.
        ///
        /// <b>왜 0 일 때 기억을 지우는가.</b> 서버가 판을 되돌리면 <c>ResultTick</c> 이 0 이 된다.
        /// 그때 기억을 비워야 <b>다음 판에서 같은 성공·실패·점수가 나와도</b> 다시 보여 줄 수 있다.
        /// 안 지우면 두 번째 판의 결과가 조용히 묻힌다.
        /// </summary>
        private void PublishResultWhenReady()
        {
            if (ResultTick == 0)
            {
                shownResultTick = 0;
                return;
            }

            if (ResultTick == shownResultTick)
            {
                return;
            }

            shownResultTick = ResultTick;

            if (resultRelease != null)
            {
                StopCoroutine(resultRelease);
            }

            resultRelease = StartCoroutine(ShowResultAfterHold(
                ResultClear, ResultScore, ResultPlayTime));
        }

        /// <summary>
        /// 성공·실패 연출을 <see cref="resultHoldSeconds"/> 만큼 보여 준 뒤 결과 판으로 넘긴다.
        ///
        /// 여는 시각의 기준이 서버 틱이라 <b>두 사람의 화면이 거의 같은 순간에 바뀐다.</b>
        /// </summary>
        private IEnumerator ShowResultAfterHold(bool clear, int score, float playTime)
        {
            yield return new WaitForSeconds(resultHoldSeconds);

            // 연출은 제 몫을 다했다. 결과 판과 겹치지 않게 접는다.
            if (resultView == null)
            {
                resultView = FindAnyObjectByType<ShipCoopResultView>(FindObjectsInactive.Include);
            }

            if (resultView != null)
            {
                resultView.Hide();
            }

            // ⚠ 여기서 반드시 소비해야 한다. 안 그러면 Gateway 가 들고 있다가 Lobby 의
            //    MatchFlowController 에 넘기고, 그쪽은 보상을 적립한다. 아직 지급 단계가 아니다.
            MiniGameResultGateway.SubmitAuthoritative(new MiniGameResult(
                MiniGameId.Ship,
                clear,
                score,
                playTime,
                playerCount: Crew));

            resultRelease = null;
        }

        /// <summary>
        /// 판이 끝난 순간을 <b>서버 틱으로 찍어</b> 모두에게 알린다.
        ///
        /// 점수와 성공 여부는 <c>ShipCoopGame</c> 이 서버에서만 계산한 값을 그대로 옮긴다.
        /// <b>클라이언트가 다시 계산하지 않는다</b> — 두 사람이 다른 점수를 보면 안 된다.
        /// </summary>
        private void WriteResultWhenFinished()
        {
            if (game == null || ResultTick != 0)
            {
                return;
            }

            bool ended = game.State == ShipCoopState.Cleared
                         || game.State == ShipCoopState.Sunk
                         || game.State == ShipCoopState.TimeOver;

            if (!ended)
            {
                return;
            }

            ResultTick = Runner.Tick;
            ResultClear = game.State == ShipCoopState.Cleared;
            ResultScore = game.FinalScore;
            ResultPlayTime = game.Elapsed;

            Debug.Log(
                $"[ShipCoop 결과] 확정 — {(ResultClear ? "성공" : "실패")}, " +
                $"점수 {ResultScore}, {ResultPlayTime:F0}초 (틱 {ResultTick})");
        }

        /// <summary>
        /// 출항 전 안내. 두 화면이 <b>같은 복제 값</b>을 보므로 같은 글이 뜬다.
        ///
        /// 서버에서도 불리지만 <c>ShipCoopServerCleanup</c> 이 HUD 를 꺼 두어 헛돌지 않는다.
        /// </summary>
        private void UpdateStartNotice()
        {
            if (hud == null)
            {
                return;
            }

            if (Sailed)
            {
                // 출항했으면 페이즈 이름이 다시 나와야 한다.
                hud.StartNotice = null;
                return;
            }

            hud.StartNotice = Countdown > 0f
                ? $"{Mathf.CeilToInt(Countdown)}초 뒤 출항"
                : $"{crewToStart}명을 기다리는 중 ({Crew}/{crewToStart})";
        }

        public override void Render()
        {
            UpdateStartNotice();

            // 서버는 자기가 적은 값을 도로 읽을 필요가 없다.
            if (HasStateAuthority)
            {
                return;
            }

            PublishResultWhenReady();

            if (game != null)
            {
                game.ShowState((ShipCoopState)Phase, Elapsed, PhaseIndex, Score);
            }

            if (health != null)
            {
                health.ShowHp(Hp);

                // 판정에는 안 쓴다(클라이언트는 깎지 않는다). 개발자 모드 패널이 보여주기만 한다.
                health.Invincible = Invincible;
            }

            if (voyage != null)
            {
                voyage.SetProgress01(Progress01);
                voyage.SailPower01 = SailPower01;
            }

            if (flooding != null)
            {
                flooding.ShowState(Flood01, Leaks);
            }

            ApplyTaskGauges();
            ApplyBoxOpen();
            ApplyDumps();
            ApplyDropped();
        }
    }

    /// <summary>갑판에 놓인 물건 하나. <c>Id</c> 가 0 이면 빈 칸이다.</summary>
    public struct DroppedSlot : INetworkStruct
    {
        public int Id;
        public int Kind;
        public Vector3 Position;
    }

    /// <summary>
    /// 자리 하나의 표시 값. 자리 종류에 따라 쓰는 칸이 다르다.
    /// <code>
    ///   조타   Value = 뱃머리 각도(도), Extra = 조타 입력(-1 ~ +1)
    ///   대포   Count = 실린 포탄 수
    /// </code>
    /// <c>Id</c> 가 0 이면 빈 칸이다.
    /// </summary>
    public struct TaskGauge : INetworkStruct
    {
        public int Id;
        public float Value;
        public float Extra;
        public int Count;
    }
}

using System.Linq;
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
    public sealed class ShipCoopStateSync : NetworkBehaviour
    {
        [Header("테스트용 시작 대기")]
        [Tooltip("이 인원이 모여야 카운트다운을 시작한다.")]
        [SerializeField, Min(1)] private int crewToStart = 2;

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

        [Networked] private int Phase { get; set; }
        [Networked] private float Elapsed { get; set; }
        [Networked] private int PhaseIndex { get; set; }
        [Networked] private int Score { get; set; }
        [Networked] private float Hp { get; set; }
        [Networked] private float Progress01 { get; set; }
        [Networked] private float Flood01 { get; set; }
        [Networked] private int Leaks { get; set; }

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
            }

            if (voyage != null)
            {
                Progress01 = voyage.Progress01;
            }

            if (flooding != null)
            {
                Flood01 = flooding.Level01;
                Leaks = flooding.LeakingPoints;
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
            Crew = Runner.ActivePlayers.Count();

            if (Sailed || game == null)
            {
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

            if (game != null)
            {
                game.ShowState((ShipCoopState)Phase, Elapsed, PhaseIndex, Score);
            }

            if (health != null)
            {
                health.ShowHp(Hp);
            }

            if (voyage != null)
            {
                voyage.SetProgress01(Progress01);
            }

            if (flooding != null)
            {
                flooding.ShowState(Flood01, Leaks);
            }
        }
    }
}

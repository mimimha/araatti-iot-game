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

        public override void Spawned()
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
            health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);

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

        public override void Render()
        {
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

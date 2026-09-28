#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Fusion;
using MiniGames.Common;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// <b>매칭 규칙을 손으로 돌려 보는 시험대.</b>
    ///
    /// <see cref="LobbyMatchmaker"/> 는 "누가 누구와 같은 판에 들어가는가" 를 정한다.
    /// 틀리면 3인을 고른 사람이 2인 판에 끌려가거나, 두 팀이 한 방에서 만난다.
    /// 둘 다 사람을 붙여 놓고 눈으로 보기 어려운 종류의 사고라, 규칙만 떼어 내어 돌린다.
    ///
    /// 서버도 Photon 도 필요 없다. 빈 방 목록과 시계를 가짜로 물려 준다.
    ///
    /// <b>쓰는 법.</b> Tools > 아라아띠 > 매칭 규칙 시험.
    ///
    /// <b>남겨 두는 도구다.</b> 매칭 규칙을 고칠 때마다 눌러 보면 된다. 특히
    /// "출발한 방을 잠시 붙잡아 둔다" 같은 것은 사람을 붙여서는 재현하기 어렵다 —
    /// 세션 목록이 늦게 갱신되는 그 몇 초를 손으로 맞출 수가 없기 때문이다.
    /// </summary>
    public static class MatchmakerScenarios
    {
        /// <summary>가짜 시계. 시험이 직접 돌린다.</summary>
        private static double clock;

        /// <summary>가짜 DS Pool. 게임마다 방 이름 → 비어 있는가.</summary>
        private static Dictionary<string, bool> pool;

        private static StringBuilder log;
        private static int failures;

        [MenuItem("Tools/아라아띠/매칭 규칙 시험")]
        public static void Run()
        {
            log = new StringBuilder();
            failures = 0;

            Scenario_같은인원끼리_모여서_출발한다();
            Scenario_다른인원은_다른방으로_간다();
            Scenario_방이_없으면_줄을_선다();
            Scenario_취소하면_방이_풀린다();
            Scenario_출발직후에는_그방을_다시_주지_않는다();
            Scenario_허용범위밖_인원은_받지_않는다();

            log.AppendLine();
            log.AppendLine(failures == 0 ? "전부 통과했습니다." : $"틀린 것 {failures}건.");

            if (failures == 0) Debug.Log("[매칭 시험]\n" + log);
            else Debug.LogError("[매칭 시험]\n" + log);
        }

        // ───────────────────────────── 시나리오 ─────────────────────────────

        private static void Scenario_같은인원끼리_모여서_출발한다()
        {
            Begin("같은 인원끼리 모여서 출발한다", mineRooms: 2);

            var maker = NewMaker(out List<LobbyMatchmaker.Party> launched);
            MiniGameConfig mining = Config(MiniGameId.Mining);

            maker.Join(P(1), mining, 3);
            Check("한 명 — 아직 출발하지 않는다", launched.Count == 0);
            Check("한 명 — 방을 미리 잡아 둔다", maker.PhaseOf(P(1)) == LobbyMatchmaker.Phase.Gathering);

            maker.Join(P(2), mining, 3);
            Check("두 명 — 같은 일행이다", maker.PartyOf(P(1)) == maker.PartyOf(P(2)));
            Check("두 명 — 아직 출발하지 않는다", launched.Count == 0);

            maker.Join(P(3), mining, 3);
            Check("세 명 — 출발한다", launched.Count == 1);
            Check("세 명 — 셋이 함께 간다", launched.Count == 1 && launched[0].Members.Count == 3);
            Check("세 명 — 방을 받았다", launched.Count == 1 && launched[0].Session == "mine-1");
        }

        private static void Scenario_다른인원은_다른방으로_간다()
        {
            Begin("인원이 다르면 섞이지 않는다", mineRooms: 2);

            var maker = NewMaker(out _);
            MiniGameConfig mining = Config(MiniGameId.Mining);

            maker.Join(P(1), mining, 2);
            maker.Join(P(2), mining, 4);

            Check("일행이 둘이다", maker.Parties.Count == 2);
            Check("서로 다른 일행이다", maker.PartyOf(P(1)) != maker.PartyOf(P(2)));
            Check("방도 서로 다르다",
                maker.PartyOf(P(1)).Session != maker.PartyOf(P(2)).Session);
        }

        private static void Scenario_방이_없으면_줄을_선다()
        {
            Begin("방이 다 차면 줄을 선다", mineRooms: 2);

            var maker = NewMaker(out _);
            MiniGameConfig mining = Config(MiniGameId.Mining);

            maker.Join(P(1), mining, 2);   // mine-1
            maker.Join(P(2), mining, 3);   // mine-2
            maker.Join(P(3), mining, 4);   // 남은 방이 없다

            Check("세 번째는 기다린다", maker.PhaseOf(P(3)) == LobbyMatchmaker.Phase.Waiting);
            Check("기다리는 사람에게는 방이 없다", maker.PartyOf(P(3)).Session.Length == 0);
        }

        private static void Scenario_취소하면_방이_풀린다()
        {
            Begin("취소하면 방이 풀리고 줄 선 사람이 받는다", mineRooms: 2);

            var maker = NewMaker(out _);
            MiniGameConfig mining = Config(MiniGameId.Mining);

            maker.Join(P(1), mining, 2);
            maker.Join(P(2), mining, 3);
            maker.Join(P(3), mining, 4);
            Check("먼저 — 세 번째는 기다린다", maker.PhaseOf(P(3)) == LobbyMatchmaker.Phase.Waiting);

            maker.Leave(P(1));
            maker.Poll();

            Check("취소한 사람은 매칭에서 빠진다", maker.PhaseOf(P(1)) == LobbyMatchmaker.Phase.None);
            Check("기다리던 사람이 방을 받는다", maker.PhaseOf(P(3)) == LobbyMatchmaker.Phase.Gathering);
        }

        private static void Scenario_출발직후에는_그방을_다시_주지_않는다()
        {
            Begin("출발한 방은 잠시 붙잡아 둔다", mineRooms: 1);

            var maker = NewMaker(out List<LobbyMatchmaker.Party> launched);
            MiniGameConfig mining = Config(MiniGameId.Mining);

            maker.Join(P(1), mining, 2);
            maker.Join(P(2), mining, 2);
            Check("두 명이 출발했다", launched.Count == 1);

            // ⚠ 여기가 핵심이다. 보낸 사람들이 아직 도착하지 않아 세션 목록에는
            //    여전히 "빈 방" 으로 보인다. 그대로 배정하면 두 팀이 한 방에서 만난다.
            maker.Join(P(3), mining, 2);
            Check("바로 뒤에 온 사람은 그 방을 못 받는다",
                maker.PhaseOf(P(3)) == LobbyMatchmaker.Phase.Waiting);

            clock += 25.0;   // 붙잡아 두는 시간(20초)을 넘긴다
            maker.Poll();

            Check("시간이 지나면 방이 풀린다",
                maker.PhaseOf(P(3)) == LobbyMatchmaker.Phase.Gathering);
        }

        private static void Scenario_허용범위밖_인원은_받지_않는다()
        {
            Begin("허용 범위를 벗어난 인원은 서버가 막는다", mineRooms: 2);

            var maker = NewMaker(out _);
            MiniGameConfig ship = Config(MiniGameId.Ship);      // 4~4명
            MiniGameConfig mining = Config(MiniGameId.Mining);  // 2~4명

            Check("배를 2명으로 신청하면 거절한다", !maker.Join(P(1), ship, 2));
            Check("거절당한 사람은 매칭에 없다", maker.PhaseOf(P(1)) == LobbyMatchmaker.Phase.None);

            Check("광산 1명도 거절한다", !maker.Join(P(2), mining, 1));
            Check("광산 5명도 거절한다", !maker.Join(P(2), mining, 5));
            Check("광산 4명은 받는다", maker.Join(P(2), mining, 4));
        }

        // ───────────────────────────── 시험 도구 ─────────────────────────────

        private static void Begin(string title, int mineRooms)
        {
            log.AppendLine();
            log.AppendLine($"── {title}");

            clock = 0.0;
            pool = new Dictionary<string, bool>();

            // 광산 방을 원하는 만큼 띄워 둔 셈 친다. 다른 게임은 방을 넉넉히 둔다.
            for (int i = 1; i <= mineRooms; i++) pool[DsPool.SessionName(MiniGameId.Mining, i)] = true;
            for (int i = 1; i <= DsPool.SizePerGame; i++)
            {
                pool[DsPool.SessionName(MiniGameId.Ship, i)] = true;
                pool[DsPool.SessionName(MiniGameId.Sword, i)] = true;
            }
        }

        private static LobbyMatchmaker NewMaker(out List<LobbyMatchmaker.Party> launched)
        {
            var sent = new List<LobbyMatchmaker.Party>();
            launched = sent;

            var maker = new LobbyMatchmaker(EmptyRooms, () => clock);
            maker.PartyLaunched += party => sent.Add(party);
            return maker;
        }

        private static List<string> EmptyRooms(MiniGameId game, int crew)
        {
            var free = new List<string>();

            for (int i = 1; i <= DsPool.SizePerGame; i++)
            {
                string name = DsPool.SessionName(game, i);
                if (pool.TryGetValue(name, out bool empty) && empty) free.Add(name);
            }

            return free;
        }

        private static MiniGameConfig Config(MiniGameId id)
        {
            MiniGameConfig found = MiniGameCatalog.Find(id);
            if (found == null) Debug.LogError($"[매칭 시험] {id} 설정을 못 찾았습니다.");
            return found;
        }

        private static PlayerRef P(int index) => PlayerRef.FromIndex(index);

        private static void Check(string what, bool ok)
        {
            log.AppendLine($"   {(ok ? "○" : "✕")} {what}");
            if (!ok) failures++;
        }
    }
}
#endif

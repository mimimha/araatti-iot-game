using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배 협동 한 판을 **사람 없이 수백 번 돌려보는** 도구.
///
/// ⚠ **플레이테스트가 아니라 모델입니다.**
///
///    거리 · 속도 · 제한시간 · 사건 간격 · 사건별 피해 · 침수 속도는
///    **씬에서 실제 값을 읽습니다.** 사람의 행동만 규칙으로 흉내 냅니다.
///    경향은 믿을 만하지만 소수점까지 맞다고 보면 안 됩니다.
///
/// 한 판이 끝나는 세 가지
///   도착   — 제한시간 안에 목적지까지 간다
///   시간초과 — 시간이 다 됐는데 못 갔다
///   침몰   — HP 가 0 이 됐다
///
/// 사람 행동 가정 (급한 것부터 사람을 붙인다)
///   1. 파손 지점이 있으면 한 명이 수리하러 간다 — 방치하면 물이 차서 배가 가라앉는다
///   2. 물이 절반 넘게 찼으면 한 명이 퍼낸다
///   3. 돛이 풀렸으면 한 명이 당긴다
///   4. 뱃머리가 틀어졌으면 한 명이 조타를 잡는다
///   5. 남는 사람이 터진 사건을 맡는다
///
/// **자리를 옮기는 데 드는 시간을 셉니다.** 3층짜리 배라 공짜가 아닙니다.
/// 이걸 빼면 한 명이 순간이동하며 모든 자리를 지켜서 혼자서도 100% 가 나옵니다.
///
/// 그래도 **실제 사람보다는 잘합니다.** 판단이 즉각적이고 실수가 없습니다.
/// 여기서 "아슬아슬" 이면 실제로는 "자주 실패" 로 보세요.
///
/// Tools > 아라아띠 > 배 협동 밸런스 시뮬레이션
/// </summary>
public static class ShipCoopBalanceSim
{
    private const float Dt = 0.05f;
    private const int Trials = 400;

    // ------------------------------------------------------------------ 사람 행동 가정
    //
    // 이 네 숫자가 결과를 크게 흔듭니다. 실제 플레이와 어긋나 보이면 여기부터 고치세요.

    /// <summary>사건 하나를 넘기는 데 필요한 사람 시간 = 그 사건 제한시간 × 이 값.</summary>
    private const float WorkFraction = 0.6f;

    /// <summary>
    /// 파손 지점 하나를 막는 데 드는 사람 시간. **씬에서 재서 계산한 값.**
    ///
    /// 자재 상자(4.05, 0.15) → 파손 지점(-2.45, -0.50) 이 6.5m 이고,
    /// **물건을 들면 달릴 수 없어**(1m/s) 6.5초. 상자까지 뛰어가는 데 1.3초,
    /// 5번 내리치는 데 2초. 합쳐서 10초.
    /// </summary>
    private const float RepairSeconds = 10f;

    /// <summary>
    /// 양동이 한 번 비우는 데 드는 사람 시간. **씬에서 재서 계산한 값.**
    ///
    /// 양동이 상자(1.05, -0.70) → 가장 가까운 뱃전(5.05, 2.15) 이 4.9m.
    /// 들고 걸어서 4.9초, 빈손으로 뛰어 돌아오는 데 1.2초. 합쳐서 6.1초.
    /// </summary>
    private const float BailSeconds = 6.1f;

    /// <summary>자리를 옮기는 데 걸리는 시간.</summary>
    private const float SwitchSeconds = 3f;

    /// <summary>파도 · 암초가 터져 있는 동안 뱃머리를 미는 속도 (도/초)</summary>
    private const float PushDegPerSecond = 45f;

    /// <summary>조타를 잡으러 갈지 정하는 기준 각도</summary>
    private const float HelmDeadzone = 15f;

    /// <summary>물이 이만큼 차면 퍼내러 간다.</summary>
    private const float BailThreshold = 0.25f;

    // 자리 번호
    private const int StationSail = 0;
    private const int StationHelm = 1;
    private const int StationRepair = 2;
    private const int StationBail = 3;
    private const int StationEvent = 4;   // 4 + 사건 인덱스

    private class EventDef
    {
        public string Name;
        public float WarnSeconds;
        public float Duration;
        public float SailLoss;
        public float Damage;
        public int ChainCount;
        public bool PushesHeading;
        public bool FillsSail;   // 돌풍 — 바람이 돛을 펴 놓는다. 접어서 버틴다
        public bool CreatesLeak;
    }

    private class Live
    {
        public EventDef Def;
        public float Age;
        public float Work;
        public bool Active => Age >= Def.WarnSeconds;
        public float ActiveAge => Age - Def.WarnSeconds;
    }

    private class Agent
    {
        public int Station = -1;
        public float Switching;
        public bool Claimed;
    }

    private enum Ending { Arrived, TimedOut, Sunk }

    private class Result
    {
        public Ending How;
        public float Seconds;
        public int Solved;
        public int Failed;
        public float AvgSail;
        public float AvgCourse;
        public float HpLeft;
    }

    [MenuItem("Tools/아라아띠/배 협동 밸런스 시뮬레이션")]
    public static void Run()
    {
        Debug.Log(Report());
    }

    public static string Report()
    {
        var voyage = Object.FindFirstObjectByType<ShipVoyage>();
        var game = Object.FindFirstObjectByType<ShipCoopGame>();
        var helm = Object.FindFirstObjectByType<HelmTask>();
        var sail = Object.FindFirstObjectByType<SailTask>();
        var flood = Object.FindFirstObjectByType<ShipFlooding>();
        var hp = Object.FindFirstObjectByType<ShipHealth>();

        if (voyage == null || game == null || helm == null || sail == null || flood == null || hp == null)
        {
            return "[밸런스] 배 협동 씬을 열고 돌리세요.";
        }

        var vs = new SerializedObject(voyage);
        float distance = vs.FindProperty("totalDistance").floatValue;
        float maxSpeed = vs.FindProperty("maxSpeed").floatValue;
        float minSpeed = maxSpeed * vs.FindProperty("minSpeedRatio").floatValue;

        float limit = new SerializedObject(game).FindProperty("timeLimit").floatValue;

        var hs = new SerializedObject(helm);
        float maxHeading = hs.FindProperty("maxHeading").floatValue;
        float turnSpeed = hs.FindProperty("turnSpeed").floatValue;
        float recenter = hs.FindProperty("recenterSpeed").floatValue;

        float pullSpeed = new SerializedObject(sail).FindProperty("pullSpeed").floatValue;

        var fs = new SerializedObject(flood);
        float floodDps = fs.FindProperty("damagePerSecondWhenFull").floatValue;
        float floodRise = fs.FindProperty("risePerPointPerSecond").floatValue;
        float dumpAmount = fs.FindProperty("dumpAmount").floatValue;

        float maxHp = new SerializedObject(hp).FindProperty("maxHp").floatValue;

        // 구멍이 뚫릴 수 있는 자리의 수. 씬에 놓인 수리 지점만큼이다.
        int repairPoints = Mathf.Max(1,
            Object.FindObjectsByType<RepairTask>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

        List<EventDef> defs = ReadEvents();
        (float min, float max, int cap)[] schedule = ReadSchedule();

        var sb = new StringBuilder();
        sb.AppendLine("=== 배 협동 밸런스 시뮬레이션 (도착 · 시간초과 · 침몰 전부) ===");
        sb.AppendLine($"거리 {distance}m · 최대 {maxSpeed}m/s · 최저 {minSpeed}m/s · 제한 {limit}초 · HP {maxHp}");
        sb.AppendLine($"침수: 파손 1개당 초당 +{floodRise:P1} · 만수 시 초당 HP -{floodDps} · 양동이 -{dumpAmount:P0}");
        sb.AppendLine($"수리 지점 {repairPoints}곳 (구멍은 여기까지만 뚫린다)");
        sb.AppendLine($"사람 가정: 수리 {RepairSeconds}초 · 양동이 {BailSeconds}초 · 자리이동 {SwitchSeconds}초");
        sb.AppendLine($"판당 {Trials} 회");
        sb.AppendLine();
        sb.AppendLine("시나리오            클리어   시간초과   침몰    평균도착  남은HP  사건성공률");

        Row(sb, "1명 · 돛만 잡기", 1, true);
        for (int crew = 1; crew <= 4; crew++)
        {
            Row(sb, $"{crew}명", crew, false);
        }

        return sb.ToString();

        void Row(StringBuilder into, string label, int crew, bool sailOnly)
        {
            int arrived = 0, timeout = 0, sunk = 0, solved = 0, failed = 0;
            float sumArrive = 0f, sumHp = 0f;

            for (int t = 0; t < Trials; t++)
            {
                var rng = new System.Random(t * 7919 + crew + (sailOnly ? 500000 : 0));

                Result r = Sim(rng, crew, sailOnly,
                               distance, maxSpeed, minSpeed, limit, maxHp,
                               maxHeading, turnSpeed, recenter, pullSpeed,
                               floodDps, floodRise, dumpAmount, repairPoints,
                               defs, schedule);

                solved += r.Solved;
                failed += r.Failed;
                sumHp += r.HpLeft;

                switch (r.How)
                {
                    case Ending.Arrived: arrived++; sumArrive += r.Seconds; break;
                    case Ending.TimedOut: timeout++; break;
                    case Ending.Sunk: sunk++; break;
                }
            }

            int total = solved + failed;
            string evt = total > 0 ? $"{solved * 100f / total,4:F0}%" : "   -";
            string arriveTime = arrived > 0 ? $"{sumArrive / arrived,5:F0}초" : "    -";

            into.AppendLine($"{label,-20} {arrived * 100f / Trials,5:F1}%   {timeout * 100f / Trials,5:F1}%  " +
                            $"{sunk * 100f / Trials,5:F1}%    {arriveTime}   {sumHp / Trials,5:F0}    {evt}");
        }
    }

    private static Result Sim(System.Random rng, int crew, bool sailOnly,
                              float distance, float maxSpeed, float minSpeed, float limit, float maxHp,
                              float maxHeading, float turnSpeed, float recenter, float pullSpeed,
                              float floodDps, float floodRise, float dumpAmount, int repairPoints,
                              List<EventDef> defs, (float min, float max, int cap)[] schedule)
    {
        var agents = new Agent[crew];
        for (int i = 0; i < crew; i++) agents[i] = new Agent();

        var result = new Result();
        var live = new List<Live>();

        float travelled = 0f;
        float sail01 = 0f;
        float heading = 0f;
        float elapsed = 0f;
        float hp = maxHp;
        float water = 0f;
        int leaks = 0;

        float repairWork = 0f;
        float bailWork = 0f;
        float nextEvent = 6f;                 // graceSeconds

        float sailSum = 0f, courseSum = 0f;
        int steps = 0;

        while (elapsed < limit)
        {
            int phase = PhaseOf(Mathf.Clamp01(travelled / distance));
            var slot = schedule[Mathf.Clamp(phase, 0, schedule.Length - 1)];

            // ── 사건 뽑기 ───────────────────
            nextEvent -= Dt;
            if (nextEvent <= 0f)
            {
                if (live.Count < slot.cap)
                {
                    live.Add(new Live { Def = defs[rng.Next(defs.Count)] });
                }

                nextEvent = Mathf.Lerp(slot.min, slot.max, (float)rng.NextDouble());
            }

            // 돌풍이 부는 중인가. 배치를 정할 때 먼저 알아야 돛 자리의 일이 뒤집힌다.
            bool squallNow = false;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Active && live[i].Def.FillsSail) { squallNow = true; break; }
            }

            // ── 사람 배치 ───────────────────
            foreach (Agent a in agents) a.Claimed = false;

            if (sailOnly)
            {
                Want(agents, StationSail);
            }
            else
            {
                // 가라앉는 것이 가장 급하다. 물은 고쳐야 멈춘다.
                if (leaks > 0) Want(agents, StationRepair);
                if (water > BailThreshold) Want(agents, StationBail);
                // 돌풍 중에는 돛을 **접는** 것이 돛 자리의 일이다. 30% 위면 붙는다.
                if (squallNow ? sail01 > 0.3f : sail01 < 0.99f) Want(agents, StationSail);
                if (Mathf.Abs(heading) > HelmDeadzone) Want(agents, StationHelm);
            }

            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Active) Want(agents, StationEvent + i);
            }

            foreach (Agent a in agents)
            {
                if (!a.Claimed) a.Station = -1;
            }

            bool onSail = false, onHelm = false;

            foreach (Agent a in agents)
            {
                if (a.Switching > 0f)
                {
                    a.Switching -= Dt;
                    continue;
                }

                switch (a.Station)
                {
                    case StationSail: onSail = true; break;
                    case StationHelm: onHelm = true; break;
                    case StationRepair: repairWork += Dt; break;
                    case StationBail: bailWork += Dt; break;
                    default:
                        if (a.Station >= StationEvent)
                        {
                            int idx = a.Station - StationEvent;
                            if (idx < live.Count) live[idx].Work += Dt;
                        }
                        break;
                }
            }

            // ── 수리 · 퍼내기 ───────────────
            if (repairWork >= RepairSeconds && leaks > 0)
            {
                leaks--;
                repairWork = 0f;
            }

            if (bailWork >= BailSeconds)
            {
                water = Mathf.Max(0f, water - dumpAmount);
                bailWork = 0f;
            }

            // ── 사건 진행 ───────────────────
            bool pushed = false, squalling = false;

            for (int i = live.Count - 1; i >= 0; i--)
            {
                Live e = live[i];
                e.Age += Dt;

                if (!e.Active) continue;

                // 선체 파손은 제한시간이 없다. 터지는 순간 구멍이 되고, 고칠 때까지 물이 찬다.
                //
                // ⚠ **구멍은 씬의 수리 지점 수를 넘지 못한다.** 뚫릴 자리가 그것뿐이다.
                //    이걸 안 막으면 구멍이 무한정 쌓여서 침몰률이 실제보다 훨씬 높게 나온다.
                if (e.Def.CreatesLeak)
                {
                    if (leaks < repairPoints) leaks++;
                    live.RemoveAt(i);
                    continue;
                }

                if (e.Def.PushesHeading) pushed = true;
                if (e.Def.FillsSail) squalling = true;

                if (e.Work >= e.Def.Duration * WorkFraction)
                {
                    result.Solved++;
                    live.RemoveAt(i);
                    continue;
                }

                if (e.ActiveAge >= e.Def.Duration)
                {
                    result.Failed++;

                    hp -= e.Def.Damage;
                    sail01 = Mathf.Clamp01(sail01 - e.Def.SailLoss);

                    if (e.Def.PushesHeading)
                    {
                        heading = Mathf.Sign(heading == 0f ? 1f : heading) * maxHeading;
                    }

                    // 사건은 연쇄한다. (5장)
                    for (int c = 0; c < e.Def.ChainCount; c++)
                    {
                        live.Add(new Live { Def = defs[rng.Next(defs.Count)] });
                    }

                    live.RemoveAt(i);
                }
            }

            // ── 침수 ───────────────────────
            if (leaks > 0)
            {
                water = Mathf.Clamp01(water + floodRise * leaks * Dt);
            }

            if (water > 0f)
            {
                hp -= floodDps * water * Dt;
            }

            if (hp <= 0f)
            {
                result.How = Ending.Sunk;
                result.Seconds = elapsed;
                result.HpLeft = 0f;
                result.AvgSail = sailSum / Mathf.Max(1, steps);
                result.AvgCourse = courseSum / Mathf.Max(1, steps);
                return result;
            }

            // ── 돛 · 조타 ──────────────────
            // 돌풍 중이면 돛 자리 사람은 당기는 게 아니라 **푼다**. 바람은 계속 펴 놓는다.
            if (onSail) sail01 = Mathf.Clamp01(sail01 + (squalling ? -pullSpeed : pullSpeed) * Dt);
            if (squalling) sail01 = Mathf.Clamp01(sail01 + 0.3f * Dt);

            if (onHelm)
            {
                heading = Mathf.MoveTowards(heading, 0f, turnSpeed * Dt);
            }
            else if (pushed)
            {
                heading = Mathf.Clamp(heading + PushDegPerSecond * Dt, -maxHeading, maxHeading);
            }
            else if (recenter > 0f)
            {
                heading = Mathf.MoveTowards(heading, 0f, recenter * Dt);
            }

            // ── 나아가기 ───────────────────
            float course = Mathf.Max(0f, Mathf.Cos(heading * Mathf.Deg2Rad));
            float sailed = Mathf.Lerp(minSpeed, maxSpeed, sail01);
            // 돌풍 중에는 돛이 바람을 거꾸로 받는다 — 펴 놓은 만큼 뒤로 간다. (ShipVoyage.Speed)
            float sailSign = squalling ? -1f : 1f;

            travelled += (minSpeed + (sailed - minSpeed) * course * sailSign) * Dt;
            elapsed += Dt;

            sailSum += sail01;
            courseSum += course;
            steps++;

            if (travelled >= distance)
            {
                result.How = Ending.Arrived;
                result.Seconds = elapsed;
                result.HpLeft = hp;
                result.AvgSail = sailSum / steps;
                result.AvgCourse = courseSum / steps;
                return result;
            }
        }

        result.How = Ending.TimedOut;
        result.Seconds = limit;
        result.HpLeft = Mathf.Max(0f, hp);
        result.AvgSail = sailSum / Mathf.Max(1, steps);
        result.AvgCourse = courseSum / Mathf.Max(1, steps);
        return result;
    }

    private static void Want(Agent[] agents, int station)
    {
        foreach (Agent a in agents)
        {
            if (a.Station == station)
            {
                a.Claimed = true;
                return;
            }
        }

        Agent take = null;
        foreach (Agent a in agents)
        {
            if (a.Claimed) continue;
            if (take == null || (a.Station == -1 && take.Station != -1)) take = a;
        }

        if (take == null) return;

        take.Station = station;
        take.Switching = SwitchSeconds;
        take.Claimed = true;
    }

    private static int PhaseOf(float progress)
    {
        if (progress >= 0.85f) return 4;
        if (progress >= 0.60f) return 3;
        if (progress >= 0.35f) return 2;
        if (progress >= 0.05f) return 1;
        return 0;
    }

    private static List<EventDef> ReadEvents()
    {
        var list = new List<EventDef>();

        foreach (var e in Object.FindObjectsByType<VoyageEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(e);
            string name = so.FindProperty("warningText").stringValue;

            list.Add(new EventDef
            {
                Name = name,
                WarnSeconds = so.FindProperty("warnSeconds").floatValue,
                Duration = so.FindProperty("duration").floatValue,
                SailLoss = so.FindProperty("sailLossOnFail").floatValue,
                Damage = so.FindProperty("damageOnFail").floatValue,
                ChainCount = so.FindProperty("chainOnFail").arraySize,
                PushesHeading = name.Contains("파도") || name.Contains("암초"),
                FillsSail = name.Contains("돌풍"),
                CreatesLeak = name.Contains("선체") || name.Contains("침수"),
            });
        }

        return list;
    }

    private static (float, float, int)[] ReadSchedule()
    {
        var slots = new (float, float, int)[5];
        for (int i = 0; i < slots.Length; i++) slots[i] = (12f, 18f, 1);

        foreach (var comp in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (comp.GetType().Name != "EventScheduler") continue;

            var so = new SerializedObject(comp);
            var it = so.GetIterator();

            int phase = -1;
            float min = 12f, max = 18f;

            while (it.NextVisible(true))
            {
                switch (it.name)
                {
                    case "phaseIndex": phase = it.intValue; break;
                    case "minInterval": min = it.floatValue; break;
                    case "maxInterval": max = it.floatValue; break;
                    case "maxConcurrent":
                        if (phase >= 0 && phase < slots.Length)
                        {
                            slots[phase] = (min, max, it.intValue);
                        }
                        break;
                }
            }

            break;
        }

        return slots;
    }
}

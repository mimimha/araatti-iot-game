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
///   4. 뱃머리가 틀어졌으면 한 명이 조타를 잡는다.
///      **거대한 파도가 밀고 있으면 둘이 붙는다** (`HelmTask.pushedCapacity`)
///   5. 남는 사람이 터진 사건을 맡는다
///
/// ⛔ **한 자리에 한 명만 붙이던 시절의 결과는 버리세요.**
///
///    예전 모델은 (가) 한 자리에 한 명만 배정하고, (나) 조타에 누가 있으면
///    미는 힘을 통째로 무시하고, (다) 암초도 미는 사건으로 치고,
///    (라) 거대한 파도의 침수를 한 방울도 안 셌습니다.
///
///    (가)+(나) 때문에 **혼자서도 파도를 늘 이겼고**, 그래서 둘째 사람이
///    필요한 상황이 모델 안에 아예 없었습니다. 이 게임의 핵심 협력 작업(6장)을
///    빼놓고 "4번째 사람이 필요한가" 를 재고 있었던 셈입니다.
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

    /// <summary>
    /// 뱃머리를 미는 속도의 기본값 (도/초). 실제로는 사건에서 읽습니다. (<see cref="EventDef.PushPerSecond"/>)
    ///
    /// ⚠ **미는 것은 거대한 파도뿐입니다.** 암초는 조타각을 읽기만 합니다.
    /// </summary>
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

        /// <summary>뱃머리가 이 각도 안이면 정면으로 받은 것으로 본다. (거대한 파도)</summary>
        public float StraightTolerance;

        /// <summary>정면을 벗어나 있어도 되는 시간. 넘으면 실패한다. (거대한 파도)</summary>
        public float AllowedOffTime;

        /// <summary>뱃머리를 미는 속도 (도/초).</summary>
        public float PushPerSecond;

        // ⚠ **거대한 파도는 HP 를 안 깎습니다. 갑판에 물을 붓습니다.**
        //    씬의 `damageOnFail` 이 0 이라 예전 모델에서는 **실패해도 아무 일이
        //    없었습니다.** 뱃머리만 끝까지 꺾이고 끝이었습니다. 정작 이 사건의
        //    대가인 침수를 한 방울도 안 세고 있었던 것입니다. (BigWave.Flood)

        /// <summary>옆으로 맞았을 때 갑판에 차는 물 (0~1).</summary>
        public float FloodOnFail;

        /// <summary>정면으로 받아냈어도 넘어오는 물 (0~1).</summary>
        public float FloodOnPass;
    }

    private class Live
    {
        public EventDef Def;
        public float Age;
        public float Work;

        /// <summary>정면을 벗어나 있던 시간. 미는 사건만 쓴다. (BigWave.OffTime 과 같다)</summary>
        public float OffTime;

        /// <summary>미는 쪽. +1 이면 우현으로 밀린다.</summary>
        public float PushSide = 1f;

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

        // 밀리는 동안 조타에 붙을 수 있는 인원. 실제 HelmTask 가 정원을 이만큼 늘린다.
        int helmPushedCapacity = Mathf.Max(1, hs.FindProperty("pushedCapacity").intValue);

        float pullSpeed = new SerializedObject(sail).FindProperty("pullSpeed").floatValue;

        var fs = new SerializedObject(flood);
        float floodDps = fs.FindProperty("damagePerSecondWhileWet").floatValue;
        float floodRise = fs.FindProperty("risePerPointPerSecond").floatValue;
        float dumpAmount = fs.FindProperty("dumpAmount").floatValue;
        float floodGrace = fs.FindProperty("damageGraceSeconds").floatValue;

        float maxHp = new SerializedObject(hp).FindProperty("maxHp").floatValue;

        // ⚠ **수리 지점은 씬에 미리 놓여 있지 않습니다. 선체 파손이 터질 때 생깁니다.**
        //    그래서 `FindObjectsByType<RepairTask>` 는 에디터에서 늘 0 이고,
        //    `Max(1, 0)` 때문에 한동안 **구멍이 1개뿐인 것으로 재고 있었습니다.**
        //    진짜 상한은 선체 파손 사건의 `maxPoints` 입니다. 지금 값은 2 라,
        //    구멍이 둘까지 뚫리고 **둘을 동시에 수리할 수 있습니다.**
        int repairPoints = 0;

        foreach (var e in Object.FindObjectsByType<VoyageEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty mp = new SerializedObject(e).FindProperty("maxPoints");
            if (mp != null) repairPoints = Mathf.Max(repairPoints, mp.intValue);
        }

        if (repairPoints <= 0)
        {
            repairPoints = Mathf.Max(1,
                Object.FindObjectsByType<RepairTask>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        // ⚠ **양동이는 자리 정원이 없습니다.** 들고 다니는 물건이고 버리는 곳이 여러 곳이라,
        //    사람만 있으면 동시에 퍼냅니다. 한 명으로 묶어 두면 침수가 실제보다 훨씬 무섭게 나옵니다.
        int dumpPoints = Mathf.Max(1,
            Object.FindObjectsByType<WaterDumpPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

        List<EventDef> defs = ReadEvents();
        (float min, float max, int cap)[] schedule = ReadSchedule();

        var sb = new StringBuilder();
        sb.AppendLine("=== 배 협동 밸런스 시뮬레이션 (도착 · 시간초과 · 침몰 전부) ===");
        sb.AppendLine($"거리 {distance}m · 최대 {maxSpeed}m/s · 최저 {minSpeed}m/s · 제한 {limit}초 · HP {maxHp}");
        sb.AppendLine($"침수: 파손 1개당 초당 +{floodRise:P1} · 물이 있으면 초당 HP -{floodDps} (차고 {floodGrace}초 뒤부터) · 양동이 -{dumpAmount:P0}");
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
                               maxHeading, turnSpeed, recenter, helmPushedCapacity, pullSpeed,
                               floodDps, floodRise, dumpAmount, floodGrace, repairPoints, dumpPoints,
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
                              float maxHeading, float turnSpeed, float recenter, int helmPushedCapacity, float pullSpeed,
                              float floodDps, float floodRise, float dumpAmount, float floodGrace, int repairPoints, int dumpPoints,
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
        float wetSeconds = 0f;                // 물이 찬 채로 지난 시간. ShipFlooding 의 유예와 같다
        // ⚠ **구멍마다 진행도를 따로 잽니다.** 한 사람이 자기 구멍 하나씩 붙는 것이 실제
        //    게임입니다. 합쳐서 하나의 계기로 재면, 구멍 두 개가 동시에 열려도 **번갈아
        //    하나씩만** 끝나는 것으로 나옵니다. (아래 갱신부에 이유가 적혀 있습니다)
        var holeWork = new List<float>();   // 원소 하나 = 뚫린 구멍 하나. 값은 그 구멍에 들인 사람 시간
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
                    live.Add(NewLive(defs, rng));
                }

                nextEvent = Mathf.Lerp(slot.min, slot.max, (float)rng.NextDouble());
            }

            // 돌풍이 부는 중인가. 배치를 정할 때 먼저 알아야 돛 자리의 일이 뒤집힌다.
            bool squallNow = false;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Active && live[i].Def.FillsSail) { squallNow = true; break; }
            }

            // 지금 뱃머리가 밀리고 있는가. **배치 전에 알아야** 조타에 둘을 부를 수 있다.
            bool pushNow = false;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Active && live[i].Def.PushesHeading) { pushNow = true; break; }
            }

            // ── 사람 배치 ───────────────────
            foreach (Agent a in agents) a.Claimed = false;

            if (sailOnly)
            {
                Want(agents, StationSail, 1);
            }
            else
            {
                // 가라앉는 것이 가장 급하다. 물은 고쳐야 멈춘다.
                //
                // ⚠ 구멍이 둘이면 **둘이 동시에** 막는다. 구멍마다 수리 지점이 따로 생긴다.
                if (holeWork.Count > 0) Want(agents, StationRepair, Mathf.Min(holeWork.Count, repairPoints));

                // 물이 찰수록 더 붙는다. 양동이는 정원이 없어서 사람만 있으면 병렬이다.
                int bailWant = 0;
                if (water > BailThreshold) bailWant = 1;
                if (water > 0.50f) bailWant = 2;
                if (water > 0.75f) bailWant = 3;

                if (bailWant > 0) Want(agents, StationBail, Mathf.Min(bailWant, dumpPoints));
                // 돌풍 중에는 돛을 **접는** 것이 돛 자리의 일이다. 30% 위면 붙는다.
                if (squallNow ? sail01 > 0.3f : sail01 < 0.99f) Want(agents, StationSail, 1);

                // ⚠ **밀리는 동안에는 조타에 둘을 부릅니다.** (HelmTask.pushedCapacity)
                //    혼자서는 35 − 45 = −10°/초 로 계속 밀려서 집니다.
                //    밀리기 시작하면 뱃머리가 아직 정면이어도 미리 붙어야 합니다.
                if (pushNow)
                {
                    Want(agents, StationHelm, helmPushedCapacity);
                }
                else if (Mathf.Abs(heading) > HelmDeadzone)
                {
                    Want(agents, StationHelm, 1);
                }
            }

            for (int i = 0; i < live.Count; i++)
            {
                // ⚠ **미는 사건(거대한 파도)은 따로 사람을 붙이는 일이 아닙니다.**
                //    조타를 붙잡는 것이 그 사건의 일 전부입니다. 여기서 또 한 명을
                //    부르면 파도 하나에 세 명이 매달리는 것이 되어 실제와 다릅니다.
                if (live[i].Active && !live[i].Def.PushesHeading) Want(agents, StationEvent + i, 1);
            }

            foreach (Agent a in agents)
            {
                if (!a.Claimed) a.Station = -1;
            }

            bool onSail = false;

            // ⚠ 참/거짓이 아니라 **사람 수**입니다. 조타는 붙은 만큼 빨리 돌아갑니다.
            int helmers = 0;

            // 이번 틱에 수리에 붙어 있는 사람 수. 구멍마다 한 명씩 붙는다.
            int repairers = 0;

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
                    case StationHelm: helmers++; break;
                    case StationRepair: repairers++; break;
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

            // ── 수리 ───────────────────────
            //
            // ⛔ **예전에는 합친 계기 하나로 쟀습니다.** 구멍이 둘 열려서 둘이 붙으면,
            //    계기가 2배 속도로 차고 '한 구멍만' 끝난 뒤 리셋됐습니다. 실제로는
            //    '둘 다 동시에' 끝나야 하는데 말입니다. 그래서 두 번째 구멍이 실제보다
            //    늦게 끝나는 것으로 나왔습니다.
            //
            // ✅ 구멍마다 **따로** 진행도를 잽니다. 붙은 사람 수(repairers)만큼
            //    앞에서부터 순서대로 채웁니다. 다 같은 값이라 어느 구멍이 먼저인지는
            //    안 가린다.
            for (int i = 0; i < repairers && i < holeWork.Count; i++)
            {
                holeWork[i] += Dt;
            }

            for (int i = holeWork.Count - 1; i >= 0; i--)
            {
                if (holeWork[i] >= RepairSeconds) holeWork.RemoveAt(i);
            }

            if (bailWork >= BailSeconds)
            {
                water = Mathf.Max(0f, water - dumpAmount);
                bailWork = 0f;
            }

            // ── 사건 진행 ───────────────────
            bool pushed = false, squalling = false;
            float pushSide = 1f, pushRate = PushDegPerSecond;

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
                    if (holeWork.Count < repairPoints) holeWork.Add(0f);
                    live.RemoveAt(i);
                    continue;
                }

                // ── 거대한 파도 ────────────────
                //
                // 다른 사건과 판정이 다릅니다. **일한 시간이 아니라 뱃머리 각도**로
                // 정해집니다. 조타를 붙잡아 정면을 지키면 넘어가고, 정해진 시간보다
                // 오래 벗어나 있으면 옆으로 맞습니다. (BigWave.OnTick / OnTimeout)
                if (e.Def.PushesHeading)
                {
                    pushed = true;
                    pushSide = e.PushSide;
                    pushRate = e.Def.PushPerSecond;

                    if (Mathf.Abs(heading) > e.Def.StraightTolerance) e.OffTime += Dt;

                    if (e.OffTime > e.Def.AllowedOffTime)
                    {
                        // 옆으로 맞았다. HP 가 아니라 갑판에 물이 쏟아진다.
                        result.Failed++;
                        water = Mathf.Clamp01(water + e.Def.FloodOnFail);
                        live.RemoveAt(i);
                        continue;
                    }

                    if (e.ActiveAge >= e.Def.Duration)
                    {
                        // 버텨냈다. 그래도 물은 넘어온다.
                        result.Solved++;
                        water = Mathf.Clamp01(water + e.Def.FloodOnPass);
                        live.RemoveAt(i);
                    }

                    continue;
                }

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

                    // 미는 사건은 여기까지 오지 않는다. 위에서 각도로 따로 판정한다.

                    // 사건은 연쇄한다. (5장)
                    for (int c = 0; c < e.Def.ChainCount; c++)
                    {
                        live.Add(NewLive(defs, rng));
                    }

                    live.RemoveAt(i);
                }
            }

            // ── 침수 ───────────────────────
            if (holeWork.Count > 0)
            {
                water = Mathf.Clamp01(water + floodRise * holeWork.Count * Dt);
            }

            wetSeconds = water > 0f ? wetSeconds + Dt : 0f;

            if (water > 0f && wetSeconds >= floodGrace)
            {
                hp -= floodDps * Dt;
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

            // ── 조타 ───────────────────────
            //
            // ⛔ **예전에는 이랬습니다. 틀린 모델이었습니다.**
            //
            //      if (onHelm)       heading → 0 (turnSpeed 로)
            //      else if (pushed)  heading 을 민다
            //
            //    `else` 라서 **사람이 한 명이라도 붙으면 미는 힘이 사라졌습니다.**
            //    혼자서도 거대한 파도를 늘 이겼고, 그래서 둘째 사람이 필요한 상황이
            //    모델 안에 존재하지 않았습니다. 이 게임의 핵심 협력 작업(6장)이
            //    통째로 빠진 채로 밸런스를 재고 있었던 것입니다.
            //
            // ✅ 실제 `HelmTask.Work` 는 **둘을 같은 프레임에 다 적용합니다.**
            //    붙은 사람 수만큼 돌리고, 미는 힘은 그와 별개로 계속 작용합니다.
            //
            //      아무도 없음   0 − 45 = −45°/초   밀린다
            //      혼자          35 − 45 = −10°/초  밀린다. 시간은 벌지만 진다
            //      둘이서        70 − 45 = +25°/초  되돌린다
            //
            //    `Mathf.Clamp(steer, -Capacity, Capacity)` 가 1 이 아니라 정원으로
            //    자르기 때문에 두 배가 나옵니다. (HelmTask.cs)

            if (pushed)
            {
                heading += pushSide * pushRate * Dt;
            }

            if (helmers > 0)
            {
                // 사람은 늘 정면 쪽으로 돌린다. 미는 힘과 겨루는 것은 위에서 이미 더해졌다.
                heading = Mathf.MoveTowards(heading, 0f, helmers * turnSpeed * Dt);
            }
            else if (!pushed && recenter > 0f)
            {
                heading = Mathf.MoveTowards(heading, 0f, recenter * Dt);
            }

            heading = Mathf.Clamp(heading, -maxHeading, maxHeading);

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

    /// <summary>사건 하나를 새로 띄운다. 미는 사건은 밀 방향을 여기서 정한다. (BigWave.randomSide)</summary>
    private static Live NewLive(List<EventDef> defs, System.Random rng)
    {
        return new Live
        {
            Def = defs[rng.Next(defs.Count)],
            PushSide = rng.NextDouble() < 0.5 ? -1f : 1f,
        };
    }

    /// <summary>없는 값이면 기본값을 준다. BigWave 에만 있는 필드를 다른 사건에서도 읽으므로 필요하다.</summary>
    private static float Prop(SerializedObject so, string name, float fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.floatValue : fallback;
    }

    /// <summary>
    /// 그 자리에 <paramref name="want"/> 명을 붙인다.
    ///
    /// ⚠ **한동안 한 자리에 한 명만 붙였습니다.** 그래서 조타에 둘이 붙는 상황이
    ///    아예 만들어지지 않았고, 이 게임의 핵심 협력 작업(6장)이 모델에서 통째로
    ///    빠져 있었습니다. 실제 `TaskBase` 는 `capacity` 만큼 **동시에** 받습니다.
    ///
    /// 이미 그 자리에 있는 사람을 먼저 셉니다. 옮기는 시간이 안 드니까요.
    /// </summary>
    private static void Want(Agent[] agents, int station, int want)
    {
        int have = 0;

        foreach (Agent a in agents)
        {
            if (have >= want) break;

            if (a.Station == station && !a.Claimed)
            {
                a.Claimed = true;
                have++;
            }
        }

        while (have < want)
        {
            Agent take = null;

            foreach (Agent a in agents)
            {
                if (a.Claimed) continue;
                if (take == null || (a.Station == -1 && take.Station != -1)) take = a;
            }

            if (take == null) return;   // 더 부를 사람이 없다

            take.Station = station;
            take.Switching = SwitchSeconds;
            take.Claimed = true;
            have++;
        }
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
                // ⚠ **암초는 미는 사건이 아닙니다.** 한동안 여기 같이 넣어 뒀는데,
                //    `ExternalPushPerSecond` 를 쓰는 것은 `BigWave` 하나뿐입니다.
                //    암초는 `_helm.Heading` 을 **읽기만** 합니다 — 피했는지 보려고요.
                //    같이 넣으면 모델이 실제보다 어려워집니다.
                PushesHeading = name.Contains("파도"),
                FillsSail = name.Contains("돌풍"),
                CreatesLeak = name.Contains("선체") || name.Contains("침수"),

                // BigWave 에만 있는 값. 다른 사건에는 없으므로 기본값으로 떨어진다.
                StraightTolerance = Prop(so, "straightTolerance", 12f),
                AllowedOffTime = Prop(so, "allowedOffTime", 1.5f),
                PushPerSecond = Prop(so, "pushPerSecond", PushDegPerSecond),
                FloodOnFail = Prop(so, "floodOnFail", 0f),
                FloodOnPass = Prop(so, "floodOnSucceed", 0f),
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

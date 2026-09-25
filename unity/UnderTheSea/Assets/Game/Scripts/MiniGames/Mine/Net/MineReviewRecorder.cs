#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// <b>반복 시험 기록.</b> 판마다 한 줄 평 요청 · 응답 · 공용 결과 전환을 CSV 로 남기고 통계를 다시 낸다.
    /// (MINE.md 7장 "반복 시험")
    ///
    /// ⚠ <b>출시 빌드에는 없다.</b> 파일 전체가 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로 감싸여 있다.
    /// ⚠ <b>실행 인자 <c>-minereviewrecord</c> 가 있을 때만 쓴다.</b> 없으면 모든 메서드가 바로 돌아간다.
    ///
    /// <code>
    ///   AraAtti-MineServer.exe ... -minereviewrecord                       진짜 한 줄 평(Http) 으로 기록
    ///   AraAtti-MineServer.exe ... -minereviewrecord -minereviewfault pattern   고정 순서 장애로 기록
    /// </code>
    ///
    /// 남기는 곳: 실행 파일 옆 <c>MineReviewRecords/서버를 켠 시각/</c>
    ///   requests.csv  요청 한 건 = 한 줄
    ///   rounds.csv    공용 결과로 넘긴 판 하나 = 한 줄
    ///   summary.md    위 둘로 다시 낸 통계. 이벤트마다 새로 쓴다
    /// 지우려면 폴더째 지운다.
    ///
    /// <b>판정을 기록기가 따로 한다.</b> <c>MineMatchState</c> 가 "적용했다" 고 말하는 것을 믿지 않고,
    /// 공용 결과로 넘기는 순간의 <c>ResultComment</c> 를 <b>그 판에 적용된 문장</b>과 직접 비교한다.
    /// 그래야 틱 비교가 틀렸을 때 잡힌다.
    ///
    /// ⚠ 서버에서만 부른다. 클라이언트 화면에 실제로 무엇이 떴는지는 클라이언트 로그
    ///    <c>[MineHud] 성적표 한 줄 평</c> 이다. 여기서는 복제되는 값으로 판단한다.
    /// </summary>
    public static class MineReviewRecorder
    {
        public const string RecordKey = "-minereviewrecord";

        /// <summary>성적표가 걷히는 시각(판 끝나고). MineMatchState 의 1.5 + 2 + 3.5 와 같다.</summary>
        private const float CardEndSeconds = 7f;

        /// <summary>공용 결과 전환이 이보다 늦으면 "지연" 으로 센다. 프레임 오차를 넉넉히 뺐다.</summary>
        private const float TransitionLimitSeconds = 7.5f;

        /// <summary>ResultComment 칸 크기. MineMatchState.ReviewCapacity 와 같다.</summary>
        private const int Capacity = 128;

        private sealed class RequestRow
        {
            public int Seq;
            public string Service;
            public int Tick;
            public int Score;
            public bool Success;
            public DateTime RequestAt;
            public float RequestRealtime;
            public DateTime? ResponseAt;
            public double LatencyMs;
            public string Outcome = "Pending";
            public int TickAtResponse;
            public float SinceResultAtResponse = -1f;
            public string Comment = string.Empty;
        }

        private sealed class RoundRow
        {
            public int Tick;
            public int ScoreAtRequest;
            public bool SuccessAtRequest;
            public int ScoreAtTransition;
            public bool SuccessAtTransition;
            public DateTime TransitionAt;
            public double TransitionMs;
            public string CommentAtTransition;
            public bool Reset;
            public bool WrongRound;
            public bool FromOtherRound;
            public bool AiNotShown;
        }

        private static readonly List<RequestRow> requests = new List<RequestRow>();
        private static readonly List<RoundRow> rounds = new List<RoundRow>();

        private static bool? enabled;
        private static string directory;
        private static bool writeFailed;

        public static bool Enabled
        {
            get
            {
                if (enabled == null)
                {
                    enabled = FusionLaunchArguments.HasFlag(RecordKey);
                    if (enabled.Value)
                    {
                        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MineReviewRecords",
                            DateTime.Now.ToString("yyyyMMdd-HHmmss")));
                        Debug.LogWarning($"[MineReviewRecorder] ⚠ 반복 시험 기록 중 — {directory}");
                    }
                }

                return enabled.Value;
            }
        }

        /// <summary>요청을 보낼 때. 돌려준 번호를 응답 때 다시 준다. 꺼져 있으면 0.</summary>
        public static int OnRequest(IMineReviewService service, int tick, int score, bool success)
        {
            if (!Enabled) return 0;

            var row = new RequestRow
            {
                Seq = requests.Count + 1,
                Service = service is FaultMineReviewService fault ? "Fault:" + fault.Planned : service.GetType().Name,
                Tick = tick,
                Score = score,
                Success = success,
                RequestAt = DateTime.Now,
                RequestRealtime = Time.realtimeSinceStartup,
            };
            requests.Add(row);
            Flush();
            return row.Seq;
        }

        /// <summary>응답이 왔을 때. 적용했는지는 여기서 다시 가린다 — MineMatchState 와 같은 규칙.</summary>
        public static void OnResponse(int seq, string comment, int currentTick, float sinceResult)
        {
            if (!Enabled || seq <= 0 || seq > requests.Count) return;

            RequestRow row = requests[seq - 1];
            row.ResponseAt = DateTime.Now;
            row.LatencyMs = (Time.realtimeSinceStartup - row.RequestRealtime) * 1000.0;
            row.TickAtResponse = currentTick;
            row.SinceResultAtResponse = sinceResult;
            row.Comment = comment ?? string.Empty;
            row.Outcome = comment == null ? "Failed"
                : string.IsNullOrWhiteSpace(comment) ? "Empty"
                : currentTick != row.Tick ? "OtherRound"
                : "Applied";
            Flush();
        }

        /// <summary>공용 결과로 넘긴 순간. <paramref name="currentTick"/> 이 <paramref name="tick"/> 과 다르면 그 사이 판이 되돌려졌다.</summary>
        public static void OnTransition(int tick, int currentTick, int score, bool success, string resultComment)
        {
            if (!Enabled) return;

            RequestRow request = requests.LastOrDefault(r => r.Tick == tick);
            string applied = requests
                .Where(r => r.Tick == tick && r.Outcome == "Applied")
                .Select(r => Cut(r.Comment))
                .LastOrDefault();

            bool reset = currentTick != tick;
            string shown = reset ? string.Empty : resultComment ?? string.Empty;

            var row = new RoundRow
            {
                Tick = tick,
                ScoreAtRequest = request?.Score ?? score,
                SuccessAtRequest = request?.Success ?? success,
                ScoreAtTransition = score,
                SuccessAtTransition = success,
                TransitionAt = DateTime.Now,
                TransitionMs = request != null ? (Time.realtimeSinceStartup - request.RequestRealtime) * 1000.0 : -1,
                CommentAtTransition = shown,
                Reset = reset,

                // 성적표에 문장이 있는데 이 판에 적용된 문장이 아니다.
                WrongRound = shown.Length > 0 && shown != applied,

                // 그 잘못된 문장이 다른 판 요청이 받은 문장이다.
                // ⚠ WrongRound 일 때만 본다. 진짜 LLM 은 다른 판에 같은 문장을 줄 때가 있다(실측).
                FromOtherRound = shown.Length > 0 && shown != applied
                                 && requests.Any(r => r.Tick != tick && Cut(r.Comment) == shown),

                // 문장은 받아 적용했는데 성적표가 걷히기 전에 못 들어갔다.
                AiNotShown = request != null && request.Outcome == "Applied"
                             && (request.SinceResultAtResponse >= CardEndSeconds || shown.Length == 0),
            };
            rounds.Add(row);
            Flush();
        }

        private static string Cut(string comment) =>
            comment != null && comment.Length > Capacity ? comment.Substring(0, Capacity) : comment ?? string.Empty;

        // ------------------------------------------------------------
        // 파일
        // ------------------------------------------------------------

        private static void Flush()
        {
            if (writeFailed) return;

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "requests.csv"), RequestsCsv(), new UTF8Encoding(true));
                File.WriteAllText(Path.Combine(directory, "rounds.csv"), RoundsCsv(), new UTF8Encoding(true));
                File.WriteAllText(Path.Combine(directory, "summary.md"), Summary(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                // 기록 때문에 게임이 멈추면 안 된다. 한 번만 알리고 더 쓰지 않는다.
                writeFailed = true;
                Debug.LogWarning($"[MineReviewRecorder] 기록 파일을 쓰지 못해 기록을 멈춥니다. — {exception.Message}");
            }
        }

        private static string RequestsCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Seq,Service,Tick,Score,Success,RequestAt,ResponseAt,LatencyMs,Outcome,Applied,Fallback,TickAtResponse,SinceResultAtResponse,Comment");
            foreach (RequestRow r in requests)
            {
                bool applied = r.Outcome == "Applied";
                csv.AppendLine(string.Join(",",
                    r.Seq, r.Service, r.Tick, r.Score, Bool(r.Success),
                    r.RequestAt.ToString("HH:mm:ss.fff"),
                    r.ResponseAt?.ToString("HH:mm:ss.fff") ?? string.Empty,
                    r.ResponseAt != null ? Num(r.LatencyMs) : string.Empty,
                    r.Outcome, Bool(applied), Bool(!applied),
                    r.ResponseAt != null ? r.TickAtResponse.ToString() : string.Empty,
                    r.ResponseAt != null ? r.SinceResultAtResponse.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty,
                    Quote(r.Comment)));
            }
            return csv.ToString();
        }

        private static string RoundsCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Tick,ScoreAtRequest,SuccessAtRequest,ScoreAtTransition,SuccessAtTransition,TransitionAt,ResultTransitionMs,Reset,Source,WrongRound,FromOtherRound,AiNotShown,CommentAtTransition");
            foreach (RoundRow r in rounds)
            {
                csv.AppendLine(string.Join(",",
                    r.Tick, r.ScoreAtRequest, Bool(r.SuccessAtRequest), r.ScoreAtTransition, Bool(r.SuccessAtTransition),
                    r.TransitionAt.ToString("HH:mm:ss.fff"), Num(r.TransitionMs), Bool(r.Reset),
                    r.CommentAtTransition.Length > 0 ? "AI" : "Fallback",
                    Bool(r.WrongRound), Bool(r.FromOtherRound), Bool(r.AiNotShown),
                    Quote(r.CommentAtTransition)));
            }
            return csv.ToString();
        }

        private static string Summary()
        {
            List<RequestRow> answered = requests.Where(r => r.ResponseAt != null).ToList();
            List<RequestRow> got = answered.Where(r => r.Outcome == "Applied" || r.Outcome == "OtherRound").ToList();
            List<double> latency = got.Select(r => r.LatencyMs).OrderBy(v => v).ToList();

            // 요청한 지 15초가 넘었는데 공용 결과 전환이 없다 = 흐름이 멈췄다.
            float now = Time.realtimeSinceStartup;
            int stalled = requests.Count(r => now - r.RequestRealtime > 15f && rounds.All(x => x.Tick != r.Tick));

            var md = new StringBuilder();
            md.AppendLine("# 광산 한 줄 평 게임 통합 반복 시험");
            md.AppendLine();
            md.AppendLine($"- 기록 폴더: `{directory}`");
            md.AppendLine($"- 마지막 갱신: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            md.AppendLine("- 응답시간: 광산 서버가 요청을 보낸 순간부터 콜백이 불릴 때까지 (성공 응답 기준)");
            md.AppendLine("- 백분위: nearest-rank — 오름차순 정렬 뒤 ceil(p/100 × n) 번째 값");
            md.AppendLine($"- 공용 결과 전환 지연 기준: 요청(=판 끝) 뒤 {TransitionLimitSeconds:0.0}초 초과");
            md.AppendLine();
            md.AppendLine("## 요청");
            md.AppendLine();
            md.AppendLine("| 지표 | 결과 |");
            md.AppendLine("|---|---:|");
            md.AppendLine($"| 총 요청 | {requests.Count} |");
            md.AppendLine($"| 응답 옴 | {answered.Count} |");
            md.AppendLine($"| 아직 응답 없음 | {requests.Count - answered.Count} |");
            md.AppendLine($"| 문장 받음 (Applied + OtherRound) | {got.Count} |");
            md.AppendLine($"| 적용 (Applied) | {requests.Count(r => r.Outcome == "Applied")} |");
            md.AppendLine($"| 버림 — 다른 판 (OtherRound) | {requests.Count(r => r.Outcome == "OtherRound")} |");
            md.AppendLine($"| 실패 (Failed) | {requests.Count(r => r.Outcome == "Failed")} |");
            md.AppendLine($"| 빈 문장 (Empty) | {requests.Count(r => r.Outcome == "Empty")} |");
            md.AppendLine($"| 성공률 (문장 받음 / 응답 옴) | {Percent(got.Count, answered.Count)} |");
            md.AppendLine($"| 평균 응답시간 | {Ms(latency.Count > 0 ? latency.Average() : (double?)null)} |");
            md.AppendLine($"| 최소 | {Ms(latency.Count > 0 ? latency[0] : (double?)null)} |");
            md.AppendLine($"| P50 | {Ms(Percentile(latency, 50))} |");
            md.AppendLine($"| P95 | {Ms(Percentile(latency, 95))} |");
            md.AppendLine($"| P99 | {Ms(Percentile(latency, 99))} |");
            md.AppendLine($"| 최대 | {Ms(latency.Count > 0 ? latency[latency.Count - 1] : (double?)null)} |");
            md.AppendLine();
            md.AppendLine("## 게임 안정성");
            md.AppendLine();
            md.AppendLine("| 지표 | 결과 |");
            md.AppendLine("|---|---:|");
            md.AppendLine($"| 공용 결과로 넘긴 판 | {rounds.Count} |");
            md.AppendLine($"| 그 중 AI 문장 | {rounds.Count(r => r.CommentAtTransition.Length > 0)} |");
            md.AppendLine($"| 그 중 고정 문구 (fallback) | {rounds.Count(r => r.CommentAtTransition.Length == 0)} |");
            md.AppendLine($"| 게임 흐름 중단 (요청 15초 뒤에도 전환 없음) | {stalled} |");
            md.AppendLine($"| 공용 결과 전환 지연 (> {TransitionLimitSeconds:0.0}초) | {rounds.Count(r => r.TransitionMs > TransitionLimitSeconds * 1000)} |");
            md.AppendLine($"| 잘못된 판 문장 (WrongRound) | {rounds.Count(r => r.WrongRound)} |");
            md.AppendLine($"| 이전 · 다른 판 문장이 들어옴 (FromOtherRound) | {rounds.Count(r => r.FromOtherRound)} |");
            md.AppendLine($"| 점수 · 승패가 요청 때와 다름 | {rounds.Count(r => !r.Reset && (r.ScoreAtRequest != r.ScoreAtTransition || r.SuccessAtRequest != r.SuccessAtTransition))} |");
            md.AppendLine($"| 문장을 받았는데 성적표에 못 들어감 (AiNotShown) | {rounds.Count(r => r.AiNotShown)} |");
            md.AppendLine($"| 전환 전에 판이 되돌려짐 (Reset) | {rounds.Count(r => r.Reset)} |");
            md.AppendLine($"| 요청 수 − 적용 수 | {requests.Count - requests.Count(r => r.Outcome == "Applied")} |");

            if (rounds.Count > 0)
            {
                List<double> transition = rounds.Where(r => r.TransitionMs >= 0).Select(r => r.TransitionMs).OrderBy(v => v).ToList();
                md.AppendLine($"| 공용 결과 전환 시간 평균 / 최대 | {Ms(transition.Count > 0 ? transition.Average() : (double?)null)} / {Ms(transition.Count > 0 ? transition[transition.Count - 1] : (double?)null)} |");
            }

            md.AppendLine();
            md.AppendLine("## 서비스 · 방식별");
            md.AppendLine();
            md.AppendLine("| 서비스 | 요청 | Applied | OtherRound | Failed | Empty | Pending |");
            md.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            foreach (IGrouping<string, RequestRow> g in requests.GroupBy(r => r.Service).OrderBy(g => g.Key))
            {
                md.AppendLine($"| {g.Key} | {g.Count()} | {g.Count(r => r.Outcome == "Applied")} | {g.Count(r => r.Outcome == "OtherRound")} | " +
                              $"{g.Count(r => r.Outcome == "Failed")} | {g.Count(r => r.Outcome == "Empty")} | {g.Count(r => r.Outcome == "Pending")} |");
            }

            return md.ToString();
        }

        private static double? Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return null;
            int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
            return sorted[Mathf.Clamp(rank, 1, sorted.Count) - 1];
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static string Num(double value) => value.ToString("0", CultureInfo.InvariantCulture);

        private static string Ms(double? value) => value == null ? "-" : Num(value.Value) + " ms";

        private static string Percent(int part, int whole) =>
            whole == 0 ? "-" : (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
#endif

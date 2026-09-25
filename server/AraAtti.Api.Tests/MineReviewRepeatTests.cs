using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AraAtti.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace AraAtti.Api.Tests;

/// <summary>
/// POST /api/mine/review 를 <b>여러 번</b> 불러 응답시간과 실패율을 잰다. (MINE.md 7장 "반복 시험")
///
/// ⚠ <b>평소 dotnet test 에서는 건너뛴다.</b> 몇 분 걸리고, 실제 Gemini 시험은 무료 한도를 쓴다.
///    환경 변수를 줘야만 돈다.
///
/// <code>
///   MINE_REVIEW_REPEAT=50   가짜 LLM 으로 Success 반복 · Mixed 반복 (횟수)
///   MINE_REVIEW_LIVE=15     실제 Gemini 반복 (1~20 로 조인다, 4초 간격)
///                           appsettings.Development.json 의 MineReview:ApiKey 를 쓴다
/// </code>
///
/// 결과는 <c>server/AraAtti.Api.Tests/TestResults/mine-review/</c> 에 CSV(원시 데이터)와
/// .md(통계)로 남는다. .gitignore 대상이다. 지우려면 폴더째 지운다.
///
/// ⚠ 이 시험은 <b>계정 서버까지</b>다. ResultTick · 성적표 · 공용 결과 전환은 Unity 쪽
///    <c>-minereviewrecord</c> 로 잰다.
/// </summary>
public sealed class MineReviewRepeatTests
{
    /// <summary>
    /// Mixed 반복의 고정 순서. 무작위로 섞지 않는다 — 같은 순서로 다시 돌릴 수 있어야 한다.
    /// 10개가 한 바퀴다: Success 7 · Timeout 1 · Empty 1 · HttpError 1.
    /// </summary>
    public static readonly string[] MixedPattern =
    {
        "Success", "Success", "Success", "Timeout", "Success",
        "Empty", "Success", "HttpError", "Success", "Success",
    };

    /// <summary>가짜 LLM 이 답하는 데 걸리는 시간. 실제 flash-lite 왕복과 비슷하게 잡았다.</summary>
    private static readonly TimeSpan FakeLatency = TimeSpan.FromSeconds(1.5);

    private readonly ITestOutputHelper output;

    public MineReviewRepeatTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [RepeatFact("MINE_REVIEW_REPEAT")]
    public async Task Repeat_FakeSuccess()
    {
        int count = RepeatFactAttribute.Count("MINE_REVIEW_REPEAT", 1, 1000);

        using var factory = new MineReviewApiFactory();
        factory.Llm.Respond = async (_, cancel) =>
        {
            await Task.Delay(FakeLatency, cancel);
            return FakeLlm.Completion("가짜 LLM 한 줄 평");
        };

        List<ReviewRun> runs = await RunAsync(factory.CreateClient(), count, _ => "Success", TimeSpan.Zero);
        ReviewReport.Write("fake-success", runs, output);

        Assert.All(runs, r => Assert.True(r.Valid, $"#{r.Run} {r.Status} {r.Code}"));
    }

    [RepeatFact("MINE_REVIEW_REPEAT")]
    public async Task Repeat_FakeMixed()
    {
        int count = RepeatFactAttribute.Count("MINE_REVIEW_REPEAT", 1, 1000);

        using var factory = new MineReviewApiFactory();
        int call = 0;
        factory.Llm.Respond = async (_, cancel) =>
        {
            string mode = MixedPattern[call++ % MixedPattern.Length];

            if (mode == "Timeout")
            {
                await Task.Delay(TimeSpan.FromSeconds(6), cancel);
            }

            await Task.Delay(FakeLatency, cancel);

            return mode switch
            {
                "Empty" => FakeLlm.Completion("   "),
                "HttpError" => new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"boom\"}", Encoding.UTF8, "application/json")
                },
                _ => FakeLlm.Completion("가짜 LLM 한 줄 평"),
            };
        };

        List<ReviewRun> runs = await RunAsync(
            factory.CreateClient(), count, i => MixedPattern[i % MixedPattern.Length], TimeSpan.Zero);
        ReviewReport.Write("fake-mixed", runs, output);

        // 각 실패가 정해진 코드로 돌아왔는가. 한 번이라도 어긋나면 실패다.
        foreach (ReviewRun r in runs)
        {
            string expected = r.Planned switch
            {
                "Timeout" => "MINE_REVIEW_TIMEOUT",
                "Empty" => "MINE_REVIEW_EMPTY",
                "HttpError" => "MINE_REVIEW_LLM_FAILED",
                _ => "OK",
            };
            Assert.True(expected == r.Code, $"#{r.Run} {r.Planned} → {r.Status} {r.Code}");
        }
    }

    /// <summary>
    /// 실제 Gemini. <b>성공률은 단언하지 않는다</b> — 바깥 서비스라 그날그날 다르다. 대신
    /// 200 이 온 답은 성적표에 그대로 넣을 수 있는 모양(한 줄 · 60자 이내)이어야 한다.
    /// </summary>
    [RepeatFact("MINE_REVIEW_LIVE")]
    public async Task Repeat_LiveGemini()
    {
        int count = RepeatFactAttribute.Count("MINE_REVIEW_LIVE", 1, 20);

        using var factory = new LiveMineReviewApiFactory();

        // 무료 한도의 분당 요청 수를 넘지 않게 띄운다.
        List<ReviewRun> runs = await RunAsync(factory.CreateClient(), count, _ => "Live", TimeSpan.FromSeconds(4));
        ReviewReport.Write("live-gemini", runs, output);

        Assert.All(runs.Where(r => r.Status == 200), r => Assert.True(r.Valid, $"#{r.Run} 형식 오류: {r.Comment}"));
    }

    // ------------------------------------------------------------

    private static readonly (string Target, BoardShape Shape)[] Targets =
    {
        ("하트", BoardShape.Heart),
        ("물고기", BoardShape.Fish),
        ("별", BoardShape.Star),
        ("집", BoardShape.House),
    };

    private static readonly int[] Scores = { 82, 45, 71, 63, 90, 30, 70, 69 };

    private static async Task<List<ReviewRun>> RunAsync(
        HttpClient client, int count, Func<int, string> planned, TimeSpan gap)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        var runs = new List<ReviewRun>(count);

        for (int i = 0; i < count; i++)
        {
            if (i > 0 && gap > TimeSpan.Zero) await Task.Delay(gap);

            (string target, BoardShape shape) = Targets[i % Targets.Length];
            int score = Scores[i % Scores.Length];
            bool success = score >= 70;

            DateTime requestAt = DateTime.Now;
            var clock = Stopwatch.StartNew();

            int status;
            string text;
            try
            {
                HttpResponseMessage response = await client.PostAsJsonAsync("/api/mine/review", new
                {
                    targetName = target,
                    score,
                    success,
                    imagePngBase64 = Convert.ToBase64String(BoardPng.Encode(shape)),
                });
                status = (int)response.StatusCode;
                text = await response.Content.ReadAsStringAsync();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                status = 0;
                text = "{\"code\":\"CLIENT_" + exception.GetType().Name + "\"}";
            }

            clock.Stop();
            runs.Add(ReviewRun.From(i + 1, planned(i), target, score, success, requestAt, DateTime.Now,
                clock.Elapsed.TotalMilliseconds, status, text));
        }

        return runs;
    }
}

/// <summary>반복 한 번의 결과. CSV 한 줄이다.</summary>
public sealed record ReviewRun(
    int Run, string Planned, string Target, int Score, bool Success,
    DateTime RequestAt, DateTime ResponseAt, double LatencyMs,
    int Status, string Code, string Comment, bool Valid)
{
    /// <summary>성적표에 쓸 수 있는가 — 200 · 비지 않음 · 한 줄 · 60자 이내 (서버 Clean 의 약속).</summary>
    public static ReviewRun From(int run, string planned, string target, int score, bool success,
        DateTime requestAt, DateTime responseAt, double latencyMs, int status, string body)
    {
        string code = "UNKNOWN";
        string comment = string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            if (status == 200 && root.TryGetProperty("comment", out JsonElement c))
            {
                code = "OK";
                comment = c.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("code", out JsonElement e))
            {
                code = e.GetString() ?? "UNKNOWN";
            }
        }
        catch (JsonException)
        {
            code = "NOT_JSON";
        }

        bool valid = status == 200 && code == "OK"
                     && !string.IsNullOrWhiteSpace(comment)
                     && comment.Length <= 60
                     && !comment.Contains('\n');

        return new ReviewRun(run, planned, target, score, success, requestAt, responseAt, latencyMs,
            status, code, comment, valid);
    }
}

/// <summary>원시 데이터(CSV)와 통계(.md)를 남긴다.</summary>
public static class ReviewReport
{
    public static void Write(string name, IReadOnlyList<ReviewRun> runs, ITestOutputHelper output)
    {
        string dir = OutputDirectory();
        Directory.CreateDirectory(dir);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string csvPath = Path.Combine(dir, $"{stamp}-{name}.csv");
        string mdPath = Path.Combine(dir, $"{stamp}-{name}.md");

        var csv = new StringBuilder();
        csv.AppendLine("Run,Planned,Target,Score,Success,RequestAt,ResponseAt,LatencyMs,Status,Code,Valid,Fallback,Comment");
        foreach (ReviewRun r in runs)
        {
            csv.AppendLine(string.Join(",",
                r.Run, r.Planned, r.Target, r.Score, r.Success ? "true" : "false",
                r.RequestAt.ToString("HH:mm:ss.fff"), r.ResponseAt.ToString("HH:mm:ss.fff"),
                r.LatencyMs.ToString("0", CultureInfo.InvariantCulture),
                r.Status, r.Code, r.Valid ? "true" : "false", r.Valid ? "false" : "true",
                Quote(r.Comment)));
        }

        // Excel 이 한글을 깨뜨리지 않게 BOM 을 붙인다.
        File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));

        string summary = Summarize(name, runs);
        File.WriteAllText(mdPath, summary, new UTF8Encoding(false));

        output.WriteLine(summary);
        output.WriteLine($"원시 데이터: {csvPath}");
    }

    public static string Summarize(string name, IReadOnlyList<ReviewRun> runs)
    {
        List<ReviewRun> ok = runs.Where(r => r.Valid).ToList();
        List<double> latency = ok.Select(r => r.LatencyMs).OrderBy(v => v).ToList();

        var md = new StringBuilder();
        md.AppendLine($"# 한 줄 평 API 반복 시험 — {name}");
        md.AppendLine();
        md.AppendLine($"- 실행 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        md.AppendLine("- 응답시간: 테스트가 요청을 보낸 순간부터 응답 본문을 다 받을 때까지 (계정 서버 + LLM 왕복)");
        md.AppendLine("- 백분위: nearest-rank — 성공 응답 시간을 오름차순으로 놓고 ceil(p/100 × n) 번째 값");
        md.AppendLine();
        md.AppendLine("| 지표 | 결과 |");
        md.AppendLine("|---|---:|");
        md.AppendLine($"| 실행 횟수 | {runs.Count} |");
        md.AppendLine($"| 성공 (200 · 쓸 수 있는 문장) | {ok.Count} |");
        md.AppendLine($"| 실패 | {runs.Count - ok.Count} |");
        md.AppendLine($"| 성공률 | {Percent(ok.Count, runs.Count)} |");
        md.AppendLine($"| fallback (고정 문구로 갈 요청) | {runs.Count - ok.Count} |");
        md.AppendLine($"| 평균 응답시간 | {Ms(latency.Count > 0 ? latency.Average() : null)} |");
        md.AppendLine($"| 최소 | {Ms(latency.Count > 0 ? latency[0] : null)} |");
        md.AppendLine($"| P50 | {Ms(Percentile(latency, 50))} |");
        md.AppendLine($"| P95 | {Ms(Percentile(latency, 95))} |");
        md.AppendLine($"| P99 | {Ms(Percentile(latency, 99))} |");
        md.AppendLine($"| 최대 | {Ms(latency.Count > 0 ? latency[^1] : null)} |");
        md.AppendLine($"| 200 이지만 형식 오류 | {runs.Count(r => r.Status == 200 && !r.Valid)} |");
        md.AppendLine();
        md.AppendLine("| 응답 코드 | 횟수 |");
        md.AppendLine("|---|---:|");
        foreach (IGrouping<string, ReviewRun> g in runs.GroupBy(r => $"{r.Status} {r.Code}").OrderBy(g => g.Key))
        {
            md.AppendLine($"| {g.Key} | {g.Count()} |");
        }

        if (runs.Any(r => r.Planned is not ("Success" or "Live")))
        {
            md.AppendLine();
            md.AppendLine("| 계획 | 횟수 | 응답 코드 | 실패 응답시간 평균 |");
            md.AppendLine("|---|---:|---|---:|");
            foreach (IGrouping<string, ReviewRun> g in runs.GroupBy(r => r.Planned).OrderBy(g => g.Key))
            {
                string codes = string.Join(" · ", g.Select(r => r.Code).Distinct());
                List<ReviewRun> failed = g.Where(r => !r.Valid).ToList();
                md.AppendLine($"| {g.Key} | {g.Count()} | {codes} | {Ms(failed.Count > 0 ? failed.Average(r => r.LatencyMs) : null)} |");
            }
        }

        return md.ToString();
    }

    /// <summary>nearest-rank. n 이 0 이면 null.</summary>
    public static double? Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return null;
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }

    private static string Percent(int part, int whole) =>
        whole == 0 ? "-" : (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string Ms(double? value) =>
        value is null ? "-" : value.Value.ToString("0", CultureInfo.InvariantCulture) + " ms";

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    /// <summary>테스트 프로젝트 폴더 아래 TestResults/mine-review. bin 에서 위로 올라가며 csproj 를 찾는다.</summary>
    private static string OutputDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AraAtti.Api.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "TestResults", "mine-review");
    }
}

/// <summary>환경 변수가 없으면 건너뛰는 Fact. 반복 시험이 평소 dotnet test 를 느리게 하지 않게.</summary>
public sealed class RepeatFactAttribute : FactAttribute
{
    public RepeatFactAttribute(string variable)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
        {
            Skip = $"반복 시험 — {variable}=횟수 를 주면 돈다";
        }
    }

    public static int Count(string variable, int min, int max)
    {
        int.TryParse(Environment.GetEnvironmentVariable(variable), out int value);
        return Math.Clamp(value, min, max);
    }
}

/// <summary>
/// 실제 Gemini 용. <see cref="MineReviewApiFactory"/> 와 달리 LLM 을 가짜로 바꾸지 않고
/// Development 설정(각자 PC 의 appsettings.Development.json)의 키를 그대로 쓴다.
/// 요청 주소만 같은 머신으로 채운다 — 테스트 서버는 그 칸을 비워 둔다.
/// </summary>
public sealed class LiveMineReviewApiFactory : WebApplicationFactory<MineReviewOptions>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new LoopbackFilter()));
    }

    private sealed class LoopbackFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public enum BoardShape
{
    Heart,
    Fish,
    Star,
    House,
}

/// <summary>
/// Unity MineBoardImage 와 같은 모양의 판 그림 — 20×20 칸, 칸당 16px, 판 칸 검정 · 안 판 칸 흰색.
/// 실제 LLM 이 받는 것과 같은 크기의 그림을 보내야 응답시간이 실제와 가깝다.
/// </summary>
public static class BoardPng
{
    private const int Cells = 20;
    private const int CellPx = 16;

    public static byte[] Encode(BoardShape shape)
    {
        int size = Cells * CellPx;
        var raw = new byte[size * (size + 1)];

        for (int y = 0; y < size; y++)
        {
            int row = y * (size + 1);
            raw[row] = 0; // 필터 없음
            for (int x = 0; x < size; x++)
            {
                raw[row + 1 + x] = Dug(shape, x / CellPx, y / CellPx) ? (byte)0 : (byte)255;
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var header = new byte[13];
        WriteBigEndian(header, 0, size);
        WriteBigEndian(header, 4, size);
        header[8] = 8; // 8비트
        header[9] = 0; // 흑백
        Chunk(png, "IHDR", header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw);
            }
            Chunk(png, "IDAT", compressed.ToArray());
        }

        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static bool Dug(BoardShape shape, int cx, int cy)
    {
        double x = cx - 9.5, y = cy - 9.5;
        switch (shape)
        {
            case BoardShape.Heart:
            {
                double hx = x / 8.0, hy = -y / 8.0 + 0.2;
                double a = hx * hx + hy * hy - 1;
                return a * a * a - hx * hx * hy * hy * hy <= 0;
            }
            case BoardShape.Fish:
                return (x + 2) * (x + 2) / 36.0 + y * y / 12.0 <= 1 || (x > 4 && x < 9 && Math.Abs(y) <= (x - 4));
            case BoardShape.Star:
                return Math.Abs(x) + Math.Abs(y) <= 5 || (Math.Abs(x) < 1.5 && Math.Abs(y) < 9) || (Math.Abs(y) < 1.5 && Math.Abs(x) < 9);
            default:
                return (cx >= 4 && cx <= 15 && cy >= 10 && cy <= 17) || (cy >= 3 && cy < 10 && Math.Abs(x) <= cy - 2);
        }
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        png.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);

        uint crc = Crc32(typeBytes, 0xFFFFFFFFu);
        crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        png.Write(crcBytes);
    }

    private static uint Crc32(byte[] data, uint crc)
    {
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }
        return crc;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}

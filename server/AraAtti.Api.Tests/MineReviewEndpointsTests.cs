using System.Diagnostics;
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
/// POST /api/mine/review 가 LLM 이 실패할 때 무엇을 돌려주는가.
///
/// <b>진짜 LLM 을 부르지 않는다.</b> 엔드포인트가 쓰는 HttpClient 의 맨 끝을 <see cref="FakeLlm"/> 로
/// 바꿔 끼워, 정상 · 빈 답 · 500 · 지연을 마음대로 만든다. API 코드는 그대로다.
///
/// Unity(HttpMineReviewService)는 2xx 가 아니면 이유를 가리지 않고 null → 고정 문구다.
/// 그래서 여기서는 "실패가 실패로 돌아오는가" 와 "LLM 까지 가지 말아야 할 때 안 가는가" 를 본다.
/// 늦은 응답 폐기(ResultTick)와 점수 · 승패 불변은 Unity 쪽 일이다 — MINE.md 7장 "장애 재현".
/// </summary>
public sealed class MineReviewEndpointsTests : IDisposable
{
    private readonly MineReviewApiFactory factory = new();
    private readonly ITestOutputHelper output;

    public MineReviewEndpointsTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public void Dispose() => factory.Dispose();

    // ------------------------------------------------------------
    // TC01 정상
    // ------------------------------------------------------------

    [Fact]
    public async Task TC01_Success_ReturnsOnlyCleanedComment()
    {
        factory.Llm.Respond = (_, _) => Task.FromResult(FakeLlm.Completion("\"물고기라기보단 슬리퍼 같네요\"\n두 번째 줄"));

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("물고기라기보단 슬리퍼 같네요", body.GetProperty("comment").GetString());

        // INV-01: 응답에는 문장 하나뿐이다. 점수 · 승패를 되돌려 줄 칸 자체가 없다.
        Assert.Equal(new[] { "comment" }, body.EnumerateObject().Select(p => p.Name));

        // 이미 정해진 점수 · 승패를 그대로 LLM 에 알린다. 서버가 다시 정하지 않는다.
        Assert.Equal(1, factory.Llm.Calls);
        using JsonDocument sent = JsonDocument.Parse(factory.Llm.LastBody!);
        string? userText = sent.RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("점수 71점, 성공", userText);
        Assert.Equal("Bearer " + MineReviewApiFactory.TestKey, factory.Llm.LastAuthorization);
    }

    [Fact]
    public async Task TC01_TooLongComment_IsCutTo60()
    {
        factory.Llm.Respond = (_, _) => Task.FromResult(FakeLlm.Completion(new string('가', 200)));

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(60, body.GetProperty("comment").GetString()!.Length);
    }

    // ------------------------------------------------------------
    // TC02 키 없음
    // ------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TC02_NoApiKey_Returns503Disabled_WithoutCallingLlm(string key)
    {
        factory.ApiKey = key;

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("MINE_REVIEW_DISABLED", body.GetProperty("code").GetString());
        Assert.Equal(0, factory.Llm.Calls);
    }

    // ------------------------------------------------------------
    // TC03 시간 초과
    // ------------------------------------------------------------

    /// <summary>
    /// LLM 이 6초 걸리면 서버는 설정의 5초에서 끊고 504 를 준다.
    ///
    /// ⚠ Unity 쪽 HttpJson.TimeoutSeconds 는 10초다. 서버가 먼저 끊으므로 Unity 는
    ///    연결이 끊기는 게 아니라 504 를 받아 고정 문구로 간다.
    /// </summary>
    [Fact]
    public async Task TC03_LlmSlowerThanTimeout_Returns504()
    {
        factory.TimeoutSeconds = 5;
        factory.Llm.Respond = async (_, cancel) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancel);
            return FakeLlm.Completion("늦게 온 답");
        };

        var clock = Stopwatch.StartNew();
        (HttpStatusCode status, JsonElement body) = await PostAsync();
        clock.Stop();

        output.WriteLine($"TC03 경과 {clock.Elapsed.TotalSeconds:0.00}s");

        Assert.Equal(HttpStatusCode.GatewayTimeout, status);
        Assert.Equal("MINE_REVIEW_TIMEOUT", body.GetProperty("code").GetString());
        Assert.InRange(clock.Elapsed.TotalSeconds, 4.5, 5.9);
    }

    // ------------------------------------------------------------
    // TC04 빈 답
    // ------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    [InlineData("\"\"")]
    [InlineData("“ ”")]
    public async Task TC04_EmptyComment_Returns502Empty(string? content)
    {
        factory.Llm.Respond = (_, _) => Task.FromResult(FakeLlm.Completion(content));

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal("MINE_REVIEW_EMPTY", body.GetProperty("code").GetString());
    }

    // ------------------------------------------------------------
    // TC05 LLM 오류
    // ------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task TC05_LlmHttpError_Returns502_WithoutLeakingKey(HttpStatusCode llmStatus)
    {
        factory.Llm.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(llmStatus)
        {
            Content = new StringContent("{\"error\":{\"message\":\"boom\"}}", Encoding.UTF8, "application/json")
        });

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal("MINE_REVIEW_LLM_FAILED", body.GetProperty("code").GetString());
        Assert.DoesNotContain(MineReviewApiFactory.TestKey, body.GetRawText());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":[1,2]}}]}")]
    public async Task TC05_LlmMalformedBody_Returns502(string llmBody)
    {
        factory.Llm.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(llmBody, Encoding.UTF8, "application/json")
        });

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal("MINE_REVIEW_LLM_FAILED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TC05_LlmUnreachable_Returns502()
    {
        factory.Llm.Respond = (_, _) => throw new HttpRequestException("연결 거부 (테스트)");

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal("MINE_REVIEW_LLM_FAILED", body.GetProperty("code").GetString());
    }

    // ------------------------------------------------------------
    // 보안 경계가 테스트 때문에 약해지지 않았는가
    // ------------------------------------------------------------

    [Fact]
    public async Task Security_NotLoopback_Returns403_WithoutCallingLlm()
    {
        factory.RemoteIp = IPAddress.Parse("10.0.0.5");

        (HttpStatusCode status, JsonElement body) = await PostAsync();

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("MINE_REVIEW_FORBIDDEN", body.GetProperty("code").GetString());
        Assert.Equal(0, factory.Llm.Calls);
    }

    [Fact]
    public async Task Security_NoImage_Returns400_WithoutCallingLlm()
    {
        (HttpStatusCode status, JsonElement body) = await PostAsync(image: "");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("MINE_REVIEW_BAD_IMAGE", body.GetProperty("code").GetString());
        Assert.Equal(0, factory.Llm.Calls);
    }

    // ------------------------------------------------------------

    /// <summary>Unity 의 MineReviewRequest 와 같은 모양으로 보낸다.</summary>
    private async Task<(HttpStatusCode, JsonElement)> PostAsync(string image = "iVBORw0KGgo=")
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mine/review", new
        {
            targetName = "물고기",
            score = 71,
            success = true,
            imagePngBase64 = image,
        });

        string text = await response.Content.ReadAsStringAsync();
        output.WriteLine($"HTTP {(int)response.StatusCode} {text}");

        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }
}

/// <summary>
/// API 를 테스트 서버에 띄운다. 바꾸는 것은 셋뿐이다 — 한 줄 평 설정, LLM 으로 가는 HttpClient 의 끝,
/// 요청이 온 주소. API 코드와 appsettings 는 건드리지 않는다.
/// </summary>
public sealed class MineReviewApiFactory : WebApplicationFactory<MineReviewOptions>
{
    public const string TestKey = "test-only-key-not-a-real-gemini-key";

    public FakeLlm Llm { get; } = new();

    public string ApiKey { get; set; } = TestKey;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>테스트 서버는 요청 주소를 비워 두므로(→ 403) 여기서 채운다. 기본은 같은 머신.</summary>
    public IPAddress RemoteIp { get; set; } = IPAddress.Loopback;

    static MineReviewApiFactory()
    {
        // Program.cs 는 Build() 전에 이 둘을 읽고, 없으면 켜지지 않는다. 팩토리 설정은 그보다 늦게
        // 붙으므로 환경 변수로 준다. DB 에는 붙지 않는다 — 한 줄 평은 DB 를 쓰지 않는다.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            "server=127.0.0.1;port=1;database=unused;user=unused;password=unused");
        Environment.SetEnvironmentVariable("Jwt__Key", "test-only-jwt-signing-key-0123456789abcdef");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development 로 뜨면 각자 PC 의 appsettings.Development.json(진짜 키)을 읽는다.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // 환경 변수 MineReview__ApiKey 가 PC 에 있어도 이 값이 이긴다.
            services.PostConfigure<MineReviewOptions>(options =>
            {
                options.ApiKey = ApiKey;
                options.TimeoutSeconds = TimeoutSeconds;
                options.BaseUrl = "http://fake-llm.invalid/v1";
            });

            services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(() => Llm));

            services.AddSingleton<IStartupFilter>(new RemoteIpFilter(this));
        });
    }

    private sealed class RemoteIpFilter(MineReviewApiFactory owner) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = owner.RemoteIp;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>OpenAI 호환 /chat/completions 를 흉내낸다. 무엇을 돌려줄지는 테스트가 정한다.</summary>
public sealed class FakeLlm : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(Completion("기본 답"));

    public int Calls { get; private set; }

    public string? LastBody { get; private set; }

    public string? LastAuthorization { get; private set; }

    public static HttpResponseMessage Completion(string? content)
    {
        string json = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content } } }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return await Respond(request, cancellationToken);
    }
}

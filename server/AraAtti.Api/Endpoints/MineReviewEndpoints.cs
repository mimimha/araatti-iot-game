using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AraAtti.Api.Contracts;
using Microsoft.Extensions.Options;

namespace AraAtti.Api.Endpoints;

/// <summary>
/// 광산 결과 한 줄 평. 판 그림을 LLM 에 보여 주고 한 문장을 받아 돌려준다.
///
/// 규칙
///   - <b>로그인을 요구하지 않는다.</b> 부르는 쪽이 사람이 아니라 광산 데디케이티드 서버다.
///     대신 <b>같은 머신(루프백)에서 온 요청만</b> 받는다. 배포 서버에서 광산 서버와 API 는
///     같은 머신이고, 광산 서버는 localhost:5080 으로 붙는다. (tools/deploy/deploy-servers.sh)
///   - 판당 한 번만 불린다. 승패와 점수는 이미 정해졌고 AI 는 바꾸지 않는다.
///   - 실패해도 게임은 멀쩡하다. Unity 는 실패 이유를 가리지 않고 점수 구간별 고정 문구를 쓴다.
///
/// ⚠ 광산 서버와 API 를 다른 머신으로 떼면 루프백 검사에 막힌다.
///    그때는 서버끼리 쓰는 비밀 키 방식으로 바꿔야 한다.
///
/// 문서: unity/UnderTheSea/MINE.md 7장
/// </summary>
public static class MineReviewEndpoints
{
    private const string SystemPrompt =
        "너는 광산 협동 게임의 해설자다. 광부들이 20x20 판에서 칸을 파서 목표 그림을 그린다. " +
        "이미지는 판 모습이다(검정 = 판 칸, 흰색 = 안 판 칸). " +
        "이 그림이 사람 눈에 무엇처럼 보이는지 재치 있게 평한다. " +
        "규칙: 한국어 한 문장, 30자 이내, 따옴표·이모지·줄바꿈 금지. " +
        "주어진 승패와 점수를 뒤집는 말을 하지 않는다. 사람을 비하하지 않는다.";

    /// <summary>
    /// 받는 그림의 최대 크기(base64 글자 수).
    ///
    /// 20×20 판을 칸당 16px 흑백으로 그린 PNG 는 base64 로 4KB 안팎이다. 넉넉히 잡되,
    /// 엉뚱한 큰 파일이 LLM 까지 가지 않게 막는다.
    /// </summary>
    private const int MaxImageBase64Length = 1_000_000;

    /// <summary>
    /// 돌려주는 한 줄 평의 최대 글자 수.
    ///
    /// 프롬프트로 30자를 요구하지만 LLM 이 어길 수 있다. 성적표 칸은 줄바꿈 없이
    /// 자동 축소라, 길면 글자가 깨알처럼 작아진다.
    /// </summary>
    private const int MaxCommentLength = 60;

    public static void MapMineReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/mine")
            .WithTags("Mine");

        group.MapPost("/review", ReviewAsync)
            .WithSummary("광산 결과 한 줄 평")
            .WithDescription("판 그림을 LLM 에 보여 주고 한 문장을 받는다. 같은 머신(루프백)에서 온 요청만 받는다.");
    }

    private static async Task<IResult> ReviewAsync(
        MineReviewRequest request,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        IOptions<MineReviewOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(MineReviewEndpoints));
        MineReviewOptions settings = options.Value;

        if (!IsLoopback(httpContext.Connection.RemoteIpAddress))
        {
            return Failure(StatusCodes.Status403Forbidden, "MINE_REVIEW_FORBIDDEN", "같은 머신에서 온 요청만 받습니다.");
        }

        // 키가 없으면 LLM 을 부르지 않는다. 서버를 켜는 것 자체는 막지 않는다 —
        // 한 줄 평 하나 때문에 로그인 · 인벤토리까지 멈추면 안 된다.
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return Failure(StatusCodes.Status503ServiceUnavailable, "MINE_REVIEW_DISABLED",
                "한 줄 평 키(MineReview:ApiKey)가 설정되지 않았습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.ImagePngBase64) || request.ImagePngBase64.Length > MaxImageBase64Length)
        {
            return Failure(StatusCodes.Status400BadRequest, "MINE_REVIEW_BAD_IMAGE", "판 그림이 없거나 너무 큽니다.");
        }

        string target = string.IsNullOrWhiteSpace(request.TargetName) ? "그림" : request.TargetName.Trim();

        // 성공한 판에도 "○○라기보단" 으로 깎아내리지 않게 말투를 가른다.
        string userText = request.Success
            ? $"목표: {target}. 점수 {request.Score}점, 성공. 목표로 인정하면서 눈에 띄는 특징을 한 번 비틀어 칭찬해 줘."
            : $"목표: {target}. 점수 {request.Score}점, 실패. '{target}라기보단 ○○' 처럼 실제로 무엇처럼 보이는지 짚어 줘.";

        var body = new
        {
            model = settings.Model,
            max_tokens = 200,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userText },
                        new { type = "image_url", image_url = new { url = "data:image/png;base64," + request.ImagePngBase64 } }
                    }
                }
            }
        };

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions")
            {
                Content = JsonContent.Create(body)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using HttpResponseMessage response = await httpClientFactory.CreateClient().SendAsync(message, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                // ⚠ 키는 찍지 않는다. 상태 코드와 LLM 이 준 오류 본문만 남긴다.
                string errorBody = await response.Content.ReadAsStringAsync(timeout.Token);
                logger.LogWarning("LLM 이 실패했습니다. HTTP {Status}: {Body}", (int)response.StatusCode, errorBody);
                return Failure(StatusCodes.Status502BadGateway, "MINE_REVIEW_LLM_FAILED", "LLM 이 답하지 못했습니다.");
            }

            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);

            string? raw = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            string comment = Clean(raw);

            if (comment.Length == 0)
            {
                // 모델이 답을 쓰기 전에 생각하는 데 토큰을 다 쓰면 content 가 빈다. 그때는 max_tokens 를 늘린다.
                logger.LogWarning("LLM 이 빈 답을 돌려줬습니다.");
                return Failure(StatusCodes.Status502BadGateway, "MINE_REVIEW_EMPTY", "LLM 이 빈 답을 돌려줬습니다.");
            }

            return Results.Ok(new MineReviewResponse(comment));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LLM 이 {Seconds}초 안에 답하지 않았습니다.", settings.TimeoutSeconds);
            return Failure(StatusCodes.Status504GatewayTimeout, "MINE_REVIEW_TIMEOUT", "LLM 이 제때 답하지 않았습니다.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
                                              or KeyNotFoundException or InvalidOperationException
                                              or IndexOutOfRangeException)
        {
            logger.LogWarning(exception, "LLM 응답을 처리하지 못했습니다.");
            return Failure(StatusCodes.Status502BadGateway, "MINE_REVIEW_LLM_FAILED", "LLM 응답을 처리하지 못했습니다.");
        }
    }

    /// <summary>
    /// 같은 머신에서 온 요청인가.
    ///
    /// ⚠ IPv4 가 IPv6 로 감싸져 오는 경우(::ffff:127.0.0.1)가 있어 풀어서 본다.
    /// </summary>
    private static bool IsLoopback(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address);
    }

    /// <summary>첫 줄만 쓰고, 앞뒤 따옴표를 떼고, 너무 길면 자른다. LLM 이 규칙을 어겨도 성적표가 안 깨지게.</summary>
    private static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string line = raw.Trim().Split('\n')[0].Trim().Trim('"', '\'', '“', '”', '‘', '’').Trim();
        return line.Length > MaxCommentLength ? line[..MaxCommentLength] : line;
    }

    private static IResult Failure(int statusCode, string code, string message)
    {
        return Results.Json(new ErrorResponse(code, message), statusCode: statusCode);
    }
}

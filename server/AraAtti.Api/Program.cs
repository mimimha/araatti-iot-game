using AraAtti.Api.Data;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// DB 접속
//
// 접속 문자열을 소스에 넣지 않는다. 아래 둘 중 하나로 넣는다.
//   1. 개발용 파일:  AraAtti.Api/appsettings.Development.json   (.gitignore 대상)
//   2. 환경 변수:    ConnectionStrings__Default                 (1번을 덮어쓴다)
// 자세한 방법은 server/README.md 참고.
// ------------------------------------------------------------

string? connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DB 접속 문자열이 없습니다. server/README.md 의 \"2. 접속 정보 설정\" 을 따라 " +
        "appsettings.Development.json 을 만들거나 환경 변수 ConnectionStrings__Default 를 설정해 주세요.");
}

builder.Services.AddDbContext<AraAttiDbContext>(options =>
    options.UseMySql(connectionString, AraAttiDbContext.MySqlVersion));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // http://localhost:5080/swagger
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------------------------------------------
// 헬스 체크
//
// 서버가 살아 있는지와 DB 에 붙는지를 함께 알려준다.
// DB 가 꺼져 있어도 서버는 죽지 않고 503 과 이유를 돌려준다.
// ------------------------------------------------------------

app.MapGet("/api/health", CheckHealthAsync);

// 인프라 도구들이 흔히 기대하는 경로. 위와 같은 내용을 돌려준다.
app.MapGet("/health", CheckHealthAsync);

app.Run();

static async Task<IResult> CheckHealthAsync(AraAttiDbContext database, CancellationToken cancellationToken)
{
    bool reachable;
    string? failureReason = null;

    try
    {
        reachable = await database.Database.CanConnectAsync(cancellationToken);
        if (!reachable)
        {
            failureReason = "MySQL 에 연결할 수 없습니다. docker compose ps 로 컨테이너 상태를 확인해 주세요.";
        }
    }
    catch (Exception exception)
    {
        reachable = false;
        failureReason = exception.Message;
    }

    var payload = new
    {
        status = reachable ? "ok" : "degraded",
        database = reachable ? "connected" : "disconnected",
        message = failureReason,
        serverTimeUtc = DateTime.UtcNow
    };

    return reachable
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
}

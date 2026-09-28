using System.Text;
using AraAtti.Api.Auth;
using AraAtti.Api.Contracts;
using AraAtti.Api.Data;
using AraAtti.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

// ------------------------------------------------------------
// JWT
//
// 서명 키도 접속 문자열과 같은 방식으로 넣는다.
//   1. 개발용 파일:  appsettings.Development.json 의 "Jwt:Key"
//   2. 환경 변수:    Jwt__Key
// appsettings.json 에는 Issuer · Audience · 만료 시간만 두고 키는 넣지 않는다.
// ------------------------------------------------------------

IConfigurationSection jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

JwtOptions jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

// HS256 은 키가 최소 256비트(32바이트) 여야 한다.
// 이 검사가 없으면 첫 로그인 요청에서야 알 수 없는 예외가 터진다.
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    throw new InvalidOperationException(
        "JWT 서명 키(Jwt:Key)가 없거나 너무 짧습니다. 32바이트(영문 32자) 이상이어야 합니다. " +
        "server/README.md 의 \"2. 접속 정보 설정\" 을 따라 appsettings.Development.json 에 넣거나 " +
        "환경 변수 Jwt__Key 를 설정해 주세요.");
}

builder.Services.AddSingleton<JwtTokenGenerator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 끄지 않으면 "sub" 이 긴 URI 형태의 클레임 이름으로 바뀌어 읽기 번거로워진다.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),

            ValidateLifetime = true,

            // 기본값은 5분이라 만료된 토큰이 한동안 통과한다. 짧게 줄인다.
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            // 기본 동작은 본문 없는 401 이다. 다른 실패와 같은 모양으로 맞춘다.
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("TOKEN_INVALID", "로그인이 필요합니다. 토큰이 없거나 만료되었습니다."));
            }
        };
    });

builder.Services.AddAuthorization();

// ------------------------------------------------------------
// 광산 한 줄 평 (LLM)
//
// 키도 JWT 와 같은 방식으로 넣는다.
//   1. 개발용 파일:  appsettings.Development.json 의 "MineReview:ApiKey"
//   2. 환경 변수:    MineReview__ApiKey
// 키가 없어도 서버는 켜진다. 그때 한 줄 평 API 만 503 을 돌려준다.
// ------------------------------------------------------------

builder.Services.Configure<MineReviewOptions>(builder.Configuration.GetSection(MineReviewOptions.SectionName));
builder.Services.AddHttpClient();

// ------------------------------------------------------------
// Swagger
// ------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "아라아띠 API", Version = "v1" });

    // Swagger 화면 오른쪽 위 [Authorize] 버튼. 로그인으로 받은 토큰을 붙여 넣으면
    // 이후 요청에 Authorization 헤더가 자동으로 실린다.
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "로그인 응답의 accessToken 값만 붙여 넣으세요. \"Bearer \" 는 빼고 넣습니다."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            Array.Empty<string>()
        }
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // http://localhost:5080/swagger
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ------------------------------------------------------------
// 헬스 체크
//
// 서버가 살아 있는지와 DB 에 붙는지를 함께 알려준다.
// DB 가 꺼져 있어도 서버는 죽지 않고 503 과 이유를 돌려준다.
// ------------------------------------------------------------

app.MapGet("/api/health", CheckHealthAsync);

// 인프라 도구들이 흔히 기대하는 경로. 위와 같은 내용을 돌려준다.
app.MapGet("/health", CheckHealthAsync);

// ------------------------------------------------------------
// 회원가입 · 로그인
// ------------------------------------------------------------

app.MapAuthEndpoints();

// ------------------------------------------------------------
// 캐릭터 조회 · 생성 (로그인 필요)
// ------------------------------------------------------------

app.MapCharacterEndpoints();

// ------------------------------------------------------------
// 인벤토리 · 제단 조회 (로그인 필요)
// ------------------------------------------------------------

app.MapInventoryEndpoints();

app.MapAltarEndpoints();

// ------------------------------------------------------------
// 🛠 개발자 모드 — 로비 개발자 패널이 쓰는 경로
//
// 게임 서버의 -devmode 와 같은 뜻이다(Unity DevMode.cs). 로컬 개발(Development)에서는 늘 켜고,
// 배포에서는 실행 인자 --devmode 가 있을 때만 켠다. 꺼져 있으면 경로를 만들지 않는다.
// 배포 스크립트(tools/deploy/deploy-servers.sh)의 DEVMODE 가 게임 서버와 API 를 같이 켜고 끈다.
// ------------------------------------------------------------

bool devMode = app.Environment.IsDevelopment() || args.Contains("--devmode");
if (devMode)
{
    app.MapAltarDevEndpoints();
}

app.Logger.LogInformation(devMode
    ? "[개발자 모드] 켜짐 — 개발자 경로(/api/altar/dev)를 엽니다."
    : "[개발자 모드] 꺼짐 — 개발자 경로를 열지 않습니다.");

// ------------------------------------------------------------
// 광산 결과 한 줄 평 (로그인 없음 · 같은 머신에서 온 요청만)
// ------------------------------------------------------------

app.MapMineReviewEndpoints();

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

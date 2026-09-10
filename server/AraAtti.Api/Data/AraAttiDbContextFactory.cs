using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AraAtti.Api.Data;

/// <summary>
/// `dotnet ef` 명령이 DbContext 를 만들 때 쓰는 공장.
///
/// 이것이 없으면 EF 는 Program.cs 의 웹 호스트를 통째로 띄워 DbContext 를 얻으려 하고,
/// 접속 문자열이 없을 때 나는 오류가 마이그레이션과 뒤섞여 원인을 찾기 어려워진다.
///
/// 두 가지를 보장한다.
///   1. `migrations add` 는 DB 없이도 된다. (파일만 만드는 일이므로)
///   2. `database update` 는 앱과 똑같은 접속 문자열을 읽는다.
/// </summary>
internal sealed class AraAttiDbContextFactory : IDesignTimeDbContextFactory<AraAttiDbContext>
{
    public AraAttiDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = BuildConfiguration();

        string? connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // 마이그레이션 파일을 만들 때는 DB 에 붙지 않으므로 이대로도 된다.
            // database update 를 하려면 접속 문자열이 있어야 하니 미리 알려준다.
            Console.Error.WriteLine(
                "[AraAtti] 접속 문자열이 없어 마이그레이션 생성 전용 임시 값을 사용합니다. " +
                "database update 를 하려면 server/README.md 의 \"2. 접속 정보 설정\" 을 먼저 하세요.");

            connectionString = "server=localhost;port=3306;database=araatti;user=araatti;password=";
        }

        DbContextOptions<AraAttiDbContext> options =
            new DbContextOptionsBuilder<AraAttiDbContext>()
                .UseMySql(connectionString, AraAttiDbContext.MySqlVersion)
                .Options;

        return new AraAttiDbContext(options);
    }

    /// <summary>
    /// 설정 파일을 찾는다.
    ///
    /// `dotnet ef` 를 어느 폴더에서 실행했느냐에 따라 현재 디렉터리가 달라지므로
    /// 두 곳을 모두 본다. 뒤에 넣은 것이 앞의 것을 덮는다.
    ///   1. 명령을 실행한 폴더
    ///   2. 빌드 산출물 폴더 (appsettings 는 여기로 복사된다)
    ///   3. 환경 변수 — 예: ConnectionStrings__Default
    /// </summary>
    private static IConfiguration BuildConfiguration()
    {
        IConfigurationBuilder builder = new ConfigurationBuilder();

        foreach (string basePath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            builder
                .AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: true)
                .AddJsonFile(Path.Combine(basePath, "appsettings.Development.json"), optional: true);
        }

        return builder.AddEnvironmentVariables().Build();
    }
}

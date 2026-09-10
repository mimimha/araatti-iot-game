namespace AraAtti.Api.Entities;

/// <summary>
/// 계정 하나.
///
/// 테이블 모양은 Data/AraAttiDbContext.cs 에서 정합니다.
/// (컬럼 이름 · 타입 · 인덱스를 한 곳에 모아 두려고 속성(Attribute)을 쓰지 않습니다)
/// </summary>
public class User
{
    /// <summary>BIGINT UNSIGNED. 자동 증가.</summary>
    public ulong Id { get; set; }

    /// <summary>로그인 아이디. 중복될 수 없습니다.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt 해시. 평문을 넣지 않습니다.
    ///
    /// ⚠ 이 값을 채우는 회원가입 · 로그인은 다음 단계(PRD 04)에서 만듭니다.
    ///    지금은 테이블만 있습니다.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>가입 시각. UTC 로 저장합니다.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>마지막 로그인 시각. 한 번도 로그인하지 않았으면 null.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 이 계정의 캐릭터들.
    ///
    /// ⚠ 지금 정책은 "계정당 1개" 지만 이 관계는 1:N 입니다.
    ///    개수 제한은 DB 제약이 아니라 나중에 서비스 계층에서 겁니다.
    ///    그래야 다중 캐릭터를 붙일 때 마이그레이션이 필요 없습니다.
    ///    (docs/prd/auth-character-roadmap.md 2-2)
    /// </summary>
    public List<Character> Characters { get; set; } = new();
}

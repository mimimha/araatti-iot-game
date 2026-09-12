using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Data;

/// <summary>
/// 테이블 세 개의 모양을 정하는 곳.
///
/// 컬럼 이름 · 타입 · 인덱스를 엔티티 속성(Attribute)에 흩어 두지 않고 여기에 모았습니다.
/// 테이블 구조를 확인할 때 파일 하나만 보면 됩니다.
///
/// 설계 근거: docs/prd/auth-character-roadmap.md 2장
/// </summary>
public class AraAttiDbContext : DbContext
{
    /// <summary>
    /// 접속할 MySQL 의 버전.
    ///
    /// ServerVersion.AutoDetect 를 쓰지 않는 이유: 그것은 앱이 켜질 때 DB 에 한 번 붙어본다.
    /// DB 가 꺼져 있으면 서버가 아예 기동하지 못해서 /api/health 로 원인을 확인할 수 없다.
    /// 버전을 못박아 두면 DB 가 없어도 서버는 뜨고, health 가 "disconnected" 를 알려준다.
    /// </summary>
    public static readonly ServerVersion MySqlVersion = new MySqlServerVersion(new Version(8, 4, 0));

    public AraAttiDbContext(DbContextOptions<AraAttiDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<CharacterPart> CharacterParts => Set<CharacterPart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            // 190자인 이유: utf8mb4 에서 인덱스를 걸 수 있는 최대 길이에 맞춘 관례값.
            entity.Property(user => user.Email)
                .HasColumnName("email")
                .HasMaxLength(190)
                .IsRequired();

            entity.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            entity.Property(user => user.LastLoginAt)
                .HasColumnName("last_login_at")
                .HasColumnType("datetime(6)");

            entity.HasIndex(user => user.Email)
                .IsUnique()
                .HasDatabaseName("uk_users_email");
        });

        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(character => character.Id);

            entity.Property(character => character.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            entity.Property(character => character.UserId)
                .HasColumnName("user_id")
                .HasColumnType("bigint unsigned")
                .IsRequired();

            entity.Property(character => character.Name)
                .HasColumnName("name")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(character => character.SkinColor)
                .HasColumnName("skin_color")
                .HasColumnType("char(7)")
                .IsRequired();

            entity.Property(character => character.SlotIndex)
                .HasColumnName("slot_index")
                .HasColumnType("tinyint unsigned")
                .HasDefaultValue((byte)0)
                .IsRequired();

            entity.Property(character => character.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            entity.Property(character => character.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // ⚠ 여기가 이 설계의 핵심이다. user_id 는 UNIQUE 가 아니라 그냥 인덱스다.
            //    "계정당 캐릭터 1개" 는 DB 가 아니라 서비스 계층에서 거는 규칙이다. (PRD 05)
            //    그래야 다중 캐릭터를 켤 때 스키마를 손대지 않는다.
            entity.HasIndex(character => character.UserId)
                .HasDatabaseName("idx_characters_user_id");

            // 캐릭터 이름은 게임 전체에서 중복될 수 없다.
            entity.HasIndex(character => character.Name)
                .IsUnique()
                .HasDatabaseName("uk_characters_name");

            // 한 계정 안에서 같은 자리(slot_index)를 두 캐릭터가 차지하지 못하게 한다.
            entity.HasIndex(character => new { character.UserId, character.SlotIndex })
                .IsUnique()
                .HasDatabaseName("uk_characters_user_slot");

            entity.HasOne(character => character.User)
                .WithMany(user => user.Characters)
                .HasForeignKey(character => character.UserId)
                .HasConstraintName("fk_characters_user")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterPart>(entity =>
        {
            entity.ToTable("character_parts");
            entity.HasKey(part => part.Id);

            entity.Property(part => part.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            entity.Property(part => part.CharacterId)
                .HasColumnName("character_id")
                .HasColumnType("bigint unsigned")
                .IsRequired();

            entity.Property(part => part.Slot)
                .HasColumnName("slot")
                .HasMaxLength(24)
                .IsRequired();

            entity.Property(part => part.PrefabName)
                .HasColumnName("prefab_name")
                .HasMaxLength(64)
                .IsRequired();

            // 한 캐릭터의 한 슬롯에는 파츠가 하나만 들어간다.
            entity.HasIndex(part => new { part.CharacterId, part.Slot })
                .IsUnique()
                .HasDatabaseName("uk_parts_character_slot");

            entity.HasOne(part => part.Character)
                .WithMany(character => character.Parts)
                .HasForeignKey(part => part.CharacterId)
                .HasConstraintName("fk_parts_character")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

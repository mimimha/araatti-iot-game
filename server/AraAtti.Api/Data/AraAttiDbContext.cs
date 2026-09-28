using AraAtti.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AraAtti.Api.Data;

/// <summary>
/// 테이블 일곱 개의 모양을 정하는 곳.
///
/// 컬럼 이름 · 타입 · 인덱스를 엔티티 속성(Attribute)에 흩어 두지 않고 여기에 모았습니다.
/// 테이블 구조를 확인할 때 파일 하나만 보면 됩니다.
///
/// 설계 근거: docs/prd/auth-character-roadmap.md 2장
///           docs/prd/lobby_altar_inventory_system_design.md STEP 1 (인벤토리 · 제단)
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

    public DbSet<PlayerInventoryItem> PlayerInventoryItems => Set<PlayerInventoryItem>();

    public DbSet<AltarState> AltarStates => Set<AltarState>();

    public DbSet<AltarContribution> AltarContributions => Set<AltarContribution>();

    public DbSet<RewardClaim> RewardClaims => Set<RewardClaim>();

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

        modelBuilder.Entity<PlayerInventoryItem>(entity =>
        {
            entity.ToTable("player_inventories");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            entity.Property(item => item.UserId)
                .HasColumnName("user_id")
                .HasColumnType("bigint unsigned")
                .IsRequired();

            entity.Property(item => item.ItemId)
                .HasColumnName("item_id")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(item => item.Quantity)
                .HasColumnName("quantity")
                .HasColumnType("int unsigned")
                .HasDefaultValue(0u)
                .IsRequired();

            entity.Property(item => item.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // ⚠ 이 UNIQUE 가 보상 지급의 INSERT ... ON DUPLICATE KEY UPDATE 를 성립시킨다.
            //    item_id 단독 UNIQUE 가 아니다. 그러면 한 아이템을 한 사람만 가질 수 있게 된다.
            entity.HasIndex(item => new { item.UserId, item.ItemId })
                .IsUnique()
                .HasDatabaseName("uk_inventory_user_item");

            entity.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .HasConstraintName("fk_inventory_user")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AltarState>(entity =>
        {
            entity.ToTable("altar_state", table =>
            {
                // 0 <= total_offered <= target_offering 을 DB 에서도 막는다.
                // 서버 코드의 검증이 뚫려도 이 세 줄이 남는다.
                table.HasCheckConstraint("ck_altar_target_positive", "`target_offering` > 0");
                table.HasCheckConstraint("ck_altar_total_nonneg", "`total_offered` >= 0");
                table.HasCheckConstraint("ck_altar_total_le_target", "`total_offered` <= `target_offering`");
            });

            entity.HasKey(state => state.Id);

            // ⚠ 자동 증가가 아니다. 행이 하나뿐이고 그 id 는 언제나 1 이다.
            entity.Property(state => state.Id)
                .HasColumnName("id")
                .HasColumnType("int")
                .ValueGeneratedNever();

            entity.Property(state => state.TotalOffered)
                .HasColumnName("total_offered")
                .HasColumnType("bigint unsigned")
                .HasDefaultValue(0UL)
                .IsRequired();

            entity.Property(state => state.TargetOffering)
                .HasColumnName("target_offering")
                .HasColumnType("int unsigned")
                .HasDefaultValue(1000u)
                .IsRequired();

            entity.Property(state => state.ActivatedAt)
                .HasColumnName("activated_at")
                .HasColumnType("datetime(6)");

            entity.Property(state => state.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // 그 한 행을 마이그레이션이 넣는다. 런타임의 "없으면 만든다" 로 하지 않는다.
            // UpdatedAt 을 DateTime.UtcNow 로 두면 마이그레이션을 만들 때마다 모델이 달라져
            // 빈 마이그레이션이 계속 생긴다. 그래서 고정값이다.
            entity.HasData(new AltarState
            {
                Id = 1,
                TotalOffered = 0,
                TargetOffering = 1000,
                ActivatedAt = null,
                UpdatedAt = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            });
        });

        modelBuilder.Entity<AltarContribution>(entity =>
        {
            entity.ToTable("altar_contributions");
            entity.HasKey(contribution => contribution.Id);

            entity.Property(contribution => contribution.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            // char(36) 은 Pomelo 가 Guid 를 담는 형식으로 잡아 둔 store type 이다.
            // 그래서 이 속성만 string 이 아니라 Guid 다. (Entities/AltarContribution.cs 주석)
            entity.Property(contribution => contribution.RequestId)
                .HasColumnName("request_id")
                .HasColumnType("char(36)")
                .IsRequired();

            entity.Property(contribution => contribution.UserId)
                .HasColumnName("user_id")
                .HasColumnType("bigint unsigned")
                .IsRequired();

            entity.Property(contribution => contribution.Amount)
                .HasColumnName("amount")
                .HasColumnType("int unsigned")
                .IsRequired();

            entity.Property(contribution => contribution.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // ⚠ request_id 단독 UNIQUE 가 아니다. 봉헌 멱등성 키는 (user_id, request_id) 다.
            entity.HasIndex(contribution => new { contribution.UserId, contribution.RequestId })
                .IsUnique()
                .HasDatabaseName("uk_contributions_user_request");

            entity.HasIndex(contribution => contribution.UserId)
                .HasDatabaseName("idx_contributions_user");

            entity.HasOne(contribution => contribution.User)
                .WithMany()
                .HasForeignKey(contribution => contribution.UserId)
                .HasConstraintName("fk_contributions_user")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RewardClaim>(entity =>
        {
            entity.ToTable("reward_claims");
            entity.HasKey(claim => claim.Id);

            entity.Property(claim => claim.Id)
                .HasColumnName("id")
                .HasColumnType("bigint unsigned")
                .ValueGeneratedOnAdd();

            entity.Property(claim => claim.UserId)
                .HasColumnName("user_id")
                .HasColumnType("bigint unsigned")
                .IsRequired();

            entity.Property(claim => claim.GameId)
                .HasColumnName("game_id")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(claim => claim.MatchKey)
                .HasColumnName("match_key")
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(claim => claim.ClaimedAt)
                .HasColumnName("claimed_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            // ⚠ match_key 단독 UNIQUE 가 아니다. 한 판의 네 명이 같은 match_key 를 쓴다.
            entity.HasIndex(claim => new { claim.UserId, claim.MatchKey })
                .IsUnique()
                .HasDatabaseName("uk_claims_user_match");

            // 60초 쿨다운의 SELECT MAX(claimed_at) WHERE user_id = ? 를 받는 인덱스.
            // 컬럼 순서가 (user_id, claimed_at) 이어야 한다.
            entity.HasIndex(claim => new { claim.UserId, claim.ClaimedAt })
                .HasDatabaseName("idx_claims_user_time");

            entity.HasOne(claim => claim.User)
                .WithMany()
                .HasForeignKey(claim => claim.UserId)
                .HasConstraintName("fk_claims_user")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

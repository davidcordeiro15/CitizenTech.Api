using CitizenTech.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CitizenTech.Api.Data;

public sealed class GuidanceDbContext(DbContextOptions<GuidanceDbContext> options) : DbContext(options)
{
    public DbSet<GuidanceRecord> GuidanceRecords => Set<GuidanceRecord>();

    public DbSet<SessionEvaluation> SessionEvaluations => Set<SessionEvaluation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var evaluation = modelBuilder.Entity<SessionEvaluation>();
        evaluation.ToTable("SESSION_EVALUATIONS");
        evaluation.HasKey(x => x.Id).HasName("PK_SESSION_EVALUATIONS");
        evaluation.HasIndex(x => x.SessionId).IsUnique().HasDatabaseName("UX_SESSION_EVAL_SESSION_ID");
        evaluation.Property(x => x.Id).HasColumnName("ID")
            .HasConversion(id => id.ToString("D"), value => Guid.Parse(value))
            .HasColumnType("VARCHAR2(36)").HasMaxLength(36).IsUnicode(false).ValueGeneratedNever();
        evaluation.Property(x => x.SessionId).HasColumnName("SESSION_ID")
            .HasConversion(id => id.ToString("D"), value => Guid.Parse(value))
            .HasColumnType("VARCHAR2(36)").HasMaxLength(36).IsUnicode(false).IsRequired();
        evaluation.Property(x => x.Outcome).HasColumnName("OUTCOME").HasColumnType("VARCHAR2(19)")
            .HasMaxLength(19).IsUnicode(false).IsRequired();
        evaluation.Property(x => x.Rating).HasColumnName("RATING").HasColumnType("NUMBER(2)");
        evaluation.Property(x => x.NeededHumanHelp).HasColumnName("NEEDED_HUMAN_HELP").HasColumnType("NUMBER(1)");
        evaluation.Property(x => x.Source).HasColumnName("SOURCE").HasColumnType("VARCHAR2(6)")
            .HasMaxLength(6).IsUnicode(false).IsRequired();
        evaluation.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC").HasColumnType("TIMESTAMP(7)")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        var record = modelBuilder.Entity<GuidanceRecord>();
        record.ToTable("GUIDANCE_RECORDS");
        record.HasKey(x => x.RequestId).HasName("PK_GUIDANCE_RECORDS");
        record.Property(x => x.RequestId).HasColumnName("REQUEST_ID")
            .HasConversion(id => id.ToString("D"), value => Guid.Parse(value))
            .HasColumnType("VARCHAR2(36)").HasMaxLength(36).IsUnicode(false).ValueGeneratedNever();
        record.Property(x => x.SessionId).HasColumnName("SESSION_ID")
            .HasConversion(id => id.ToString("D"), value => Guid.Parse(value))
            .HasColumnType("VARCHAR2(36)").HasMaxLength(36).IsUnicode(false).IsRequired();
        record.Property(x => x.CreatedAtUtc).HasColumnName("CREATED_AT_UTC")
            .HasColumnType("TIMESTAMP(7)")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc)).IsRequired();
        record.Property(x => x.Action).HasColumnName("ACTION").HasColumnType("VARCHAR2(16)")
            .HasMaxLength(16).IsUnicode(false);
        record.Property(x => x.Result).HasColumnName("RESULT").HasColumnType("VARCHAR2(7)")
            .HasMaxLength(7).IsUnicode(false).IsRequired();
        record.Property(x => x.ErrorCode).HasColumnName("ERROR_CODE").HasColumnType("VARCHAR2(64)")
            .HasMaxLength(64).IsUnicode(false);
    }
}

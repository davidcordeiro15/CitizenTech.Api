using CitizenTech.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CitizenTech.Api.Data;

public sealed class GuidanceDbContext(DbContextOptions<GuidanceDbContext> options) : DbContext(options)
{
    public DbSet<GuidanceRecord> GuidanceRecords => Set<GuidanceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

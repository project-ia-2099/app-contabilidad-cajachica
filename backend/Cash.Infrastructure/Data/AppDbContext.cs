using Cash.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Cash.Infrastructure.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string Schema = "contabilidad_cajachica";
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Movement> Movements => Set<Movement>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        model.Entity<Fund>(e => { e.Property(x => x.Id).ValueGeneratedNever(); e.Property(x => x.Revision).IsConcurrencyToken(); e.Property(x => x.Currency).HasMaxLength(3); e.Property(x => x.Balance).HasPrecision(18, 2); });
        model.Entity<Voucher>(e => {
            e.HasIndex(x => x.Number).IsUnique(); e.Property(x => x.Responsible).HasMaxLength(160); e.Property(x => x.Concept).HasMaxLength(500);
            e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.Returned).HasPrecision(18, 2);
            e.HasMany(x => x.Receipts).WithOne().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Receipt>(e => { e.HasIndex(x => new { x.VoucherId, x.Reference }).IsUnique(); e.Property(x => x.Reference).HasMaxLength(120); e.Property(x => x.FileName).HasMaxLength(200); e.Property(x => x.ContentType).HasMaxLength(100); e.Property(x => x.Amount).HasPrecision(18, 2); });
        model.Entity<Movement>(e => { e.Property(x => x.Type).HasMaxLength(50); e.Property(x => x.Detail).HasMaxLength(500); e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.Balance).HasPrecision(18, 2); e.HasIndex(x => x.At); });
    }
}
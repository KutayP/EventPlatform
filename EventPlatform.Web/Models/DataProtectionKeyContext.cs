using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventPlatform.Web.Models;

// Cookie ve antiforgery şifreleme anahtarlarını veritabanında tutar (konteyner diski geçici olduğu için).
// Veritabanı-önce modelden (AppDbContext) ayrı tutuldu: scaffold yenilense bile etkilenmez.
public class DataProtectionKeyContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Diğer tablolarla aynı küçük harf adlandırma.
        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("dataprotectionkeys");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FriendlyName).HasColumnName("friendlyname");
            entity.Property(e => e.Xml).HasColumnName("xml");
        });
    }
}
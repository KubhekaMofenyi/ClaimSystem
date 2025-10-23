using ClaimSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaimSystem.Data
{
    public class ClaimDbContext : DbContext
    {
        public ClaimDbContext(DbContextOptions<ClaimDbContext> options) : base(options) { }

        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<ClaimLineItem> ClaimLineItems => Set<ClaimLineItem>();
        public DbSet<SupportingDocument> SupportingDocuments => Set<SupportingDocument>();
        public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<ClaimLineItem>()
                .HasOne(li => li.Claim)
                .WithMany(c => c.LineItems)
                .HasForeignKey(li => li.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SupportingDocument>()
                .HasOne(d => d.Claim)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<ClaimStatusHistory>()
                .HasOne(h => h.Claim)
                .WithMany(c => c.StatusHistory)
                .HasForeignKey(h => h.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

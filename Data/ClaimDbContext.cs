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
    }
}

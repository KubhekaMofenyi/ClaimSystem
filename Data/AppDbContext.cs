using Microsoft.EntityFrameworkCore;
using ClaimSystem.Models;   // <-- match namespace

namespace ClaimSystem.Data     // <-- match namespace
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<ClaimLineItem> ClaimLineItems => Set<ClaimLineItem>();
        public DbSet<SupportingDocument> SupportingDocuments => Set<SupportingDocument>();
    }
}

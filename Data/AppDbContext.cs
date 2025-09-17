using Microsoft.EntityFrameworkCore;
using ClaimSystem.Models;

namespace ClaimSystem.Data   // <-- must match what Program.cs uses
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<ClaimLineItem> ClaimLineItems => Set<ClaimLineItem>();
        public DbSet<SupportingDocument> SupportingDocuments => Set<SupportingDocument>();
    }
}

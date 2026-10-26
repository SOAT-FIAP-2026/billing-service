using Billing.Service.Domain.Entities;
using Billing.Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Service.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Orcamento> Orcamentos => Set<Orcamento>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}

using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Infrastructure.Data;
using Billing.Service.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Billing.Service.Domain.Entities;

namespace Billing.Service.Infrastructure.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
            services.AddScoped<IPagamentoRepository, PagamentoRepository>();
            return services;
        }
    }
}

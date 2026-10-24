using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Security;
using Billing.Service.Infrastructure.Data;
using Billing.Service.Infrastructure.Repositories;
using Billing.Service.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Service.Infrastructure.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IServicoRepository, ServicoRepository>();
            services.AddScoped<IClienteRepository, ClienteRepository>();
            services.AddScoped<IPecaInsumoRepository, PecaInsumoRepository>();
            services.AddScoped<IEstoqueRepository, EstoqueRepository>();
            services.AddScoped<IVeiculoRepository, VeiculoRepository>();
            services.AddScoped<IStatusOrdemServicoRepository, StatusOrdemServicoRepository>();
            services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
            services.AddScoped<ICrypto, Crypto>();

            return services;
        }
    }
}

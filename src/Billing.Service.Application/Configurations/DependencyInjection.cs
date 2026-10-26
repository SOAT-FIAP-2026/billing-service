using Billing.Service.Application.Interfaces;
using Billing.Service.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Service.Application.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAutenticacaoService, AutenticacaoService>();
            services.AddScoped<IOrcamentoService, OrcamentoService>();
            
            return services;
        }
    }
}

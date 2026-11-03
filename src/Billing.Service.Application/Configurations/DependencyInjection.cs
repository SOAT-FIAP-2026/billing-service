using Billing.Service.Application.Interfaces;
using Billing.Service.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Service.Application.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IOrcamentoService, OrcamentoService>();
            services.AddScoped<IPagamentoService, PagamentoService>();
            
            return services;
        }
    }
}

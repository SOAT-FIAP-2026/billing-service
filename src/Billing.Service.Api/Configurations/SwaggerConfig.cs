using Microsoft.OpenApi.Models;

namespace Billing.Service.Api.Configurations
{
    public static class SwaggerConfig
    {
        public static void AddSwaggerConfig(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                // Configuração básica do documento
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Billing Service", Version = "v1" });
                });
        }
    }
}
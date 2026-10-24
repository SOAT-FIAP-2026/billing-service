using Billing.Service.Infrastructure.Configurations;
using Billing.Service.Application.Configurations;
using Billing.Service.External.Configurations;

namespace Billing.Service.Api.Configurations
{
    public static class DependencyInjectionConfig
    {
        public static void AddDependencyInjection(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddInfrastructure(configuration);
            services.AddApplication();
            services.AddExternal();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }
    }
}
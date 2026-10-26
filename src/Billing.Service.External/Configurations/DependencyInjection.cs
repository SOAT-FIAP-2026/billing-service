using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.External.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Billing.Service.External.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddExternal(this IServiceCollection services)
        {
            services.AddHttpClient<IEmailService, ResendEmailService>();

            return services;
        }
    }
}

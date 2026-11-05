using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Infrastructure.Data;
using Billing.Service.Infrastructure.Messaging;
using Billing.Service.Infrastructure.Repositories;
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

            services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
            services.AddScoped<IPagamentoRepository, PagamentoRepository>();

            services.AddBillingMessaging(configuration);

            return services;
        }

        public static IServiceCollection AddBillingMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            var awsServiceUrl = configuration["AWS_SERVICE_URL"]
                ?? configuration["AWS__ServiceURL"]
                ?? configuration["AWS_ENDPOINT_URL"];
            var awsRegion = configuration["AWS_REGION"] ?? configuration["AWS__Region"] ?? "sa-east-1";
            var credentials = new BasicAWSCredentials(
                configuration["AWS_ACCESS_KEY_ID"] ?? "test",
                configuration["AWS_SECRET_ACCESS_KEY"] ?? "test");

            if (!string.IsNullOrWhiteSpace(awsServiceUrl))
            {
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(credentials, new AmazonSQSConfig
                {
                    ServiceURL = awsServiceUrl,
                    AuthenticationRegion = awsRegion
                }));

                services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(credentials, new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = awsServiceUrl,
                    AuthenticationRegion = awsRegion
                }));
            }
            else
            {
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
                services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient());
            }

            services.AddScoped<IBillingEventPublisher, BillingSnsEventPublisher>();
            services.AddHostedService<BillingSqsBackgroundWorker>();

            return services;
        }
    }
}

using Billing.Service.Api.Configurations;
using Billing.Service.Api.Middlewares;
using Billing.Service.Api.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Billing.Service.Api
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddDependencyInjection(Configuration);
            services.AddSwaggerConfig();
            services.AddObservability(Configuration);
        }

        public void Configure(WebApplication app, IWebHostEnvironment env)
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<RequestObservabilityMiddleware>();
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseHttpsRedirection();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Billing Service API v1");
            });

            app.UseRouting();

            app.MapControllers();

            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            });
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = HealthCheckResponseWriter.WriteAsync
            });
            app.MapPrometheusScrapingEndpoint("/metrics");
        }
    }
}

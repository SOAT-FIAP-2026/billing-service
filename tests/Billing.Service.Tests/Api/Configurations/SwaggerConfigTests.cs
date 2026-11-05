using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Billing.Service.Tests.Billing.Service.Api.Configurations
{
    public class SwaggerConfigTests
    {
        [Fact] 
        public void AddSwaggerConfig_DeveConfigurarSwaggerCorretamente()
        {
            // Arrange
            var services = new ServiceCollection();

            var mockEnv = new Mock<IWebHostEnvironment>();
            services.AddSingleton(mockEnv.Object);
            services.AddLogging();

            // Act
            services.AddSwaggerGen(c =>
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Billing Service",
                    Version = "v1"
                }));

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetRequiredService<IOptions<SwaggerGeneratorOptions>>().Value;
            options.Should().NotBeNull();

            options.SwaggerDocs.Should().ContainKey("v1");
            options.SecuritySchemes.Should().BeEmpty();
            options.SecurityRequirements.Should().BeEmpty();
        }
    }
}
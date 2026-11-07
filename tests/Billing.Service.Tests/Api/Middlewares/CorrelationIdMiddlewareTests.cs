using Billing.Service.Api.Middlewares;
using Billing.Service.Infrastructure.Messaging;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Contracts.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Billing.Service.Tests.Api.Middlewares;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_QuandoHeaderValido_DevePropagarCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "correlation-test";
        string? ambientCorrelation = null;

        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                ambientCorrelation = CorrelationContext.Current;
                return Task.CompletedTask;
            },
            Mock.Of<ILogger<CorrelationIdMiddleware>>());

        await middleware.InvokeAsync(context);

        ambientCorrelation.Should().Be("correlation-test");
        context.Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString().Should().Be("correlation-test");
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_QuandoHeaderInvalido_DeveUsarTraceIdentifier()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "generated-trace-id" };
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = new string('x', 129);
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            Mock.Of<ILogger<CorrelationIdMiddleware>>());

        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString().Should().Be("generated-trace-id");
    }

    [Fact]
    public async Task BillingSnsEventPublisher_QuandoCorrelationContextAtivo_DevePropagarXCorrelationIdParaSns()
    {
        var snsMock = new Mock<IAmazonSimpleNotificationService>();
        PublishRequest? capturedRequest = null;

        snsMock
            .Setup(s => s.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PublishResponse { MessageId = "sns-msg-1" });

        var config = new ConfigurationBuilder().Build();
        var publisher = new BillingSnsEventPublisher(
            snsMock.Object,
            config,
            Mock.Of<ILogger<BillingSnsEventPublisher>>());

        var osId = Guid.NewGuid();

        using (CorrelationContext.Push("corr-billing-saga-77"))
        {
            var evt = new OrcamentoGeradoEvent
            {
                OsId = osId,
                OrcamentoId = Guid.NewGuid(),
                ValorTotal = 250m
            };

            await publisher.PublishAsync(evt, CancellationToken.None);
        }

        capturedRequest.Should().NotBeNull();
        capturedRequest!.MessageAttributes.Should().ContainKey(SnsMessageAttributes.CorrelationId);
        capturedRequest.MessageAttributes[SnsMessageAttributes.CorrelationId].StringValue
            .Should().Be("corr-billing-saga-77");
        capturedRequest.MessageAttributes[SnsMessageAttributes.IdOrdemServico].StringValue
            .Should().Be(osId.ToString());
    }
}

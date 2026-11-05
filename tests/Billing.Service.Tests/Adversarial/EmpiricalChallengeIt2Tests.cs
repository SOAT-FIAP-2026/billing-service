using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Service.Api.Controllers.V1;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.External.Services;
using Fiap.TechChallenge.Contracts.BillingService;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Billing.Service.Tests.Adversarial;

public class EmpiricalChallengeIt2Tests
{
    private static readonly BoletoResponse DummyBoletoResponse = new(
        Guid.NewGuid(),
        "dummy-id",
        "approved",
        100.00m,
        "https://mercadopago.fake/boleto",
        "12345",
        DateTimeOffset.UtcNow.AddDays(1));

    private static (PagamentoController controller, Mock<IPagamentoService> mockService) CreateController(
        Dictionary<string, string?>? configSettings = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configSettings ?? new Dictionary<string, string?>())
            .Build();

        var mockService = new Mock<IPagamentoService>();
        var controller = new PagamentoController(mockService.Object, config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, mockService);
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsNumeric_ProcessesSuccessfullyAndReturns200()
    {
        // 1. Webhook with numeric JSON payment ID: {"data": {"id": 12345}}
        var (controller, mockService) = CreateController();
        var json = "{\"data\": {\"id\": 12345}}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("12345");
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync("12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenRootIdIsNumeric_ProcessesSuccessfullyAndReturns200()
    {
        // 1. Webhook with numeric JSON payment ID at root: {"id": 12345}
        var (controller, mockService) = CreateController();
        var json = "{\"id\": 12345}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("12345");
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync("12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenDataIsPrimitiveNumeric_ProcessesSuccessfullyAndReturns200()
    {
        // 2. Webhook with primitive numeric data: {"data": 12345}
        var (controller, mockService) = CreateController();
        var json = "{\"data\": 12345}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("12345");
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync("12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenDataIsPrimitiveString_ProcessesSuccessfullyAndReturns200()
    {
        // 2. Webhook with primitive string data: {"data": "12345"}
        var (controller, mockService) = CreateController();
        var json = "{\"data\": \"12345\"}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("12345");
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync("12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("{\"data\": null}")]
    [InlineData("{\"data\": true}")]
    [InlineData("{\"data\": [1, 2, 3]}")]
    [InlineData("{\"data\": {}}")]
    [InlineData("{\"data\": {\"id\": null}}")]
    [InlineData("{\"data\": {\"id\": \"\"}}")]
    [InlineData("{\"id\": null}")]
    [InlineData("{\"id\": \"\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"random_string\"")]
    [InlineData("12345")]
    public async Task Webhook_WhenPayloadIsInvalidOrMalformed_SafelyReturnsBadRequestWithoutThrowing(string json)
    {
        var (controller, mockService) = CreateController();
        using var doc = JsonDocument.Parse(json);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsLarge64BitNumber_ExtractsExactRawText()
    {
        var (controller, mockService) = CreateController();
        var json = "{\"data\": {\"id\": 9223372036854775807}}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("9223372036854775807");
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsFloatingPointNumber_ExtractsExactRawText()
    {
        var (controller, mockService) = CreateController();
        var json = "{\"data\": {\"id\": 12345.67}}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("12345.67");
    }

    [Fact]
    public async Task Webhook_WhenPayloadEmpty_FallsBackToQueryStringDataId()
    {
        var (controller, mockService) = CreateController();
        controller.HttpContext.Request.QueryString = new QueryString("?data.id=query_payment_123");
        var json = "{}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("query_payment_123");
    }

    [Fact]
    public async Task Webhook_WhenPayloadEmpty_FallsBackToQueryStringId()
    {
        var (controller, mockService) = CreateController();
        controller.HttpContext.Request.QueryString = new QueryString("?id=query_root_456");
        var json = "{}";
        using var doc = JsonDocument.Parse(json);

        string? capturedPaymentId = null;
        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => capturedPaymentId = id)
            .ReturnsAsync(DummyBoletoResponse);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("query_root_456");
    }

    [Theory]
    [InlineData("ts=1700000000,v1={hash}")]
    [InlineData("ts=1700000000;v1={hash}")]
    [InlineData("v1={hash},ts=1700000000")]
    [InlineData("ts = 1700000000 , v1 = {hash}")]
    public async Task Webhook_WithSecretConfigured_ValidatesSignatureDelimiters(string headerTemplate)
    {
        var secret = "secret_key_adversarial_test";
        var (controller, mockService) = CreateController(new Dictionary<string, string?>
        {
            ["MercadoPago:WebhookSecret"] = secret
        });

        var paymentId = "98765";
        var requestId = "req-test-1234";
        var timestamp = "1700000000";

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var validHash = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();

        var headerValue = headerTemplate.Replace("1700000000", timestamp).Replace("{hash}", validHash);

        controller.HttpContext.Request.Headers["x-signature"] = headerValue;
        controller.HttpContext.Request.Headers["x-request-id"] = requestId;

        mockService
            .Setup(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyBoletoResponse);

        var json = $"{{\"data\": {{\"id\": {paymentId}}}}}";
        using var doc = JsonDocument.Parse(json);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithSecretConfigured_InvalidHashReturns401Unauthorized()
    {
        var secret = "secret_key_adversarial_test";
        var (controller, mockService) = CreateController(new Dictionary<string, string?>
        {
            ["MercadoPago:WebhookSecret"] = secret
        });

        controller.HttpContext.Request.Headers["x-signature"] = "ts=1700000000,v1=badhash123456";
        controller.HttpContext.Request.Headers["x-request-id"] = "req-123";

        var json = "{\"data\": {\"id\": 12345}}";
        using var doc = JsonDocument.Parse(json);

        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        mockService.Verify(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MockMercadoPagoClient_Generate1000IdsInTightLoop_HasZeroCollisions()
    {
        // 3. Mock payment ID collision: Generate 1,000 mock payment IDs in a tight loop and verify that 0 collisions occur.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "test"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        var request = new MercadoPagoBoletoRequest(
            100.00m,
            "Adversarial Test",
            Guid.NewGuid().ToString(),
            "test@example.com",
            "Nome",
            "Sobrenome",
            "12345678909",
            null);

        const int iterations = 1000;
        var generatedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < iterations; i++)
        {
            var boleto = await client.CriarBoletoAsync(request, CancellationToken.None);
            boleto.Should().NotBeNull();
            boleto.Id.Should().NotBeNullOrWhiteSpace();

            // Verify payment ID is unique
            var added = generatedIds.Add(boleto.Id);
            added.Should().BeTrue($"Collision detected on iteration {i}: {boleto.Id}");
        }

        generatedIds.Count.Should().Be(iterations);
    }

    [Fact]
    public async Task MockMercadoPagoClient_ParallelGeneration10000Ids_HasZeroCollisions()
    {
        // High concurrency stress test: 10,000 IDs across parallel threads
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "test"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        var request = new MercadoPagoBoletoRequest(
            50.00m,
            "Stress Test",
            Guid.NewGuid().ToString(),
            "stress@example.com",
            "Nome",
            "Sobrenome",
            "12345678909",
            null);

        const int totalCount = 10000;
        var concurrentBag = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(Enumerable.Range(0, totalCount), async (_, ct) =>
        {
            var boleto = await client.CriarBoletoAsync(request, ct);
            concurrentBag.Add(boleto.Id);
        });

        concurrentBag.Count.Should().Be(totalCount);
        var distinctCount = concurrentBag.Distinct(StringComparer.Ordinal).Count();
        distinctCount.Should().Be(totalCount, $"Found {totalCount - distinctCount} collisions in 10,000 parallel generations!");
    }
}

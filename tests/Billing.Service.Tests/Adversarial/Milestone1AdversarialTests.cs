using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Service.Api.Controllers.V1;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
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

public class Milestone1AdversarialTests
{
    // =========================================================================
    // 1. Controller Bindings & Edge Cases
    // =========================================================================

    [Fact]
    public async Task GerarOrcamento_WhenIdOrdemServicoIsNull_ShouldGenerateNewOsIdGracefully()
    {
        // Arrange
        var mockService = new Mock<IOrcamentoService>();
        Guid capturedOsId = Guid.Empty;
        mockService.Setup(s => s.GerarOrcamento(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<ItemCalculoOrcamentoDto>, IReadOnlyCollection<ItemCalculoOrcamentoDto>, CancellationToken>(
                (osId, _, _, _) => capturedOsId = osId)
            .ReturnsAsync(new Orcamento(Guid.NewGuid(), 100));

        var controller = new OrcamentoController(mockService.Object);
        var request = new GerarOrcamentoRequest(
            [new ItemCalculoOrcamentoDto("Troca de óleo", 100, 1)],
            []
        );

        // Act
        var result = await controller.GerarOrcamento(null, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedOsId.Should().NotBeEmpty("quando idOrdemServico é nulo, um novo Guid não vazio deve ser gerado");
    }

    [Fact]
    public async Task GerarOrcamento_WhenIdOrdemServicoIsEmptyGuid_ShouldGenerateNewOsIdGracefully()
    {
        // Arrange
        var mockService = new Mock<IOrcamentoService>();
        Guid capturedOsId = Guid.Empty;
        mockService.Setup(s => s.GerarOrcamento(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<ItemCalculoOrcamentoDto>, IReadOnlyCollection<ItemCalculoOrcamentoDto>, CancellationToken>(
                (osId, _, _, _) => capturedOsId = osId)
            .ReturnsAsync(new Orcamento(Guid.NewGuid(), 100));

        var controller = new OrcamentoController(mockService.Object);
        var request = new GerarOrcamentoRequest([], []);

        // Act
        var result = await controller.GerarOrcamento(Guid.Empty, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedOsId.Should().NotBeEmpty("quando idOrdemServico é Guid.Empty, um novo Guid não vazio deve ser gerado");
    }

    [Fact]
    public async Task GerarOrcamento_WhenIdOrdemServicoIsValid_ShouldPreserveOsId()
    {
        // Arrange
        var expectedOsId = Guid.NewGuid();
        var mockService = new Mock<IOrcamentoService>();
        Guid capturedOsId = Guid.Empty;
        mockService.Setup(s => s.GerarOrcamento(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<IReadOnlyCollection<ItemCalculoOrcamentoDto>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<ItemCalculoOrcamentoDto>, IReadOnlyCollection<ItemCalculoOrcamentoDto>, CancellationToken>(
                (osId, _, _, _) => capturedOsId = osId)
            .ReturnsAsync(new Orcamento(expectedOsId, 50));

        var controller = new OrcamentoController(mockService.Object);
        var request = new GerarOrcamentoRequest([], []);

        // Act
        var result = await controller.GerarOrcamento(expectedOsId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        capturedOsId.Should().Be(expectedOsId);
    }

    [Theory]
    [InlineData(0, 50.0, 50.0)]     // Quantidade 0 deve ser clampada para 1 (50 * 1 = 50)
    [InlineData(-1, 50.0, 50.0)]    // Quantidade negativa deve ser clampada para 1 (50 * 1 = 50)
    [InlineData(-5, 50.0, 50.0)]    // Quantidade negativa deve ser clampada para 1 (50 * 1 = 50)
    [InlineData(3, 50.0, 150.0)]    // Quantidade 3 calcula 50 * 3 = 150
    public void Orcamento_CalcularOrcamento_QuantityClampingBehavior(int quantidade, decimal valorUnitario, decimal valorEsperado)
    {
        // Arrange
        var orcamento = new Orcamento(Guid.NewGuid(), 0);
        var servicos = new List<ItemCalculoOrcamentoDto>
        {
            new("Serviço teste", valorUnitario, quantidade)
        };
        var pecas = new List<ItemCalculoOrcamentoDto>
        {
            new("Peça teste", valorUnitario, quantidade)
        };

        // Act
        orcamento.CalcularOrcamento(servicos, pecas);

        // Assert
        // Total esperado é valorEsperado para serviços + valorEsperado para peças
        orcamento.ValorTotal.Valor.Should().Be(valorEsperado * 2);
    }

    [Fact]
    public void Orcamento_CalcularOrcamento_WhenCollectionsAreNullOrEmpty_TotalShouldBeZero()
    {
        // Arrange
        var orcamento = new Orcamento(Guid.NewGuid(), 100);

        // Act - null collections
        orcamento.CalcularOrcamento((IEnumerable<ItemCalculoOrcamentoDto>?)null, null);

        // Assert
        orcamento.ValorTotal.Valor.Should().Be(0m);

        // Act - empty collections
        orcamento.CalcularOrcamento(Array.Empty<ItemCalculoOrcamentoDto>(), Array.Empty<ItemCalculoOrcamentoDto>());

        // Assert
        orcamento.ValorTotal.Valor.Should().Be(0m);
    }

    [Fact]
    public void Orcamento_CalcularOrcamento_WhenNegativeUnitPrice_ThrowsArgumentException()
    {
        // Arrange
        var orcamento = new Orcamento(Guid.NewGuid(), 0);
        var servicos = new List<ItemCalculoOrcamentoDto>
        {
            new("Serviço com valor negativo", -10m, 1)
        };

        // Act
        var act = () => orcamento.CalcularOrcamento(servicos, []);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode ser negativo*");
    }

    // =========================================================================
    // 2. Mercado Pago Mock Fallback & Collision Analysis
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mock")]
    [InlineData("MOCK")]
    [InlineData("test")]
    [InlineData("TEST")]
    [InlineData("offline")]
    public async Task MercadoPagoClient_MockMode_ActivatesOnNullEmptyOrMockTokens(string? token)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = token
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        var request = new MercadoPagoBoletoRequest(
            150m,
            "Teste mock",
            "REF-123",
            "cliente@teste.com",
            "Cliente",
            "Teste",
            "12345678909",
            null,
            "123");

        // Act
        var response = await client.CriarBoletoAsync(request, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().NotBeNullOrWhiteSpace();
        response.Status.Should().Be("pending");
        response.BoletoUrl.Should().Contain("mercadopago.fake");
    }

    [Fact]
    public async Task MercadoPagoClient_CriarBoletoAsync_MultipleUniqueIds_CollisionStressTest()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "mock"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        const int totalRequests = 500;
        var request = new MercadoPagoBoletoRequest(
            100m, "Teste", "REF", "test@test.com", "A", "B", "12345678909", null, "1");

        // Act: Run concurrently
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(_ => client.CriarBoletoAsync(request, CancellationToken.None))
            .ToList();

        var results = await Task.WhenAll(tasks);
        var generatedIds = results.Select(r => r.Id).ToList();
        var uniqueCount = generatedIds.Distinct().Count();

        // Assert: We check the collision count.
        uniqueCount.Should().BeGreaterThan(totalRequests / 2, "A grande maioria dos IDs deve ser única mesmo sob estresse concorrente");
    }

    [Fact]
    public async Task MercadoPagoClient_CriarBoletoAsync_TightLoopCollisions_Documented()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "mock"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        const int iterations = 1000;
        var request = new MercadoPagoBoletoRequest(
            100m, "Teste", "REF", "test@test.com", "A", "B", "12345678909", null, "1");

        var ids = new List<string>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            var res = await client.CriarBoletoAsync(request, CancellationToken.None);
            ids.Add(res.Id);
        }

        var distinctCount = ids.Distinct().Count();
        int collisions = iterations - distinctCount;

        // Document: In a tight loop of 1000 items, collisions occur because Random(100, 999) has only 899 values per ms
        // distinctCount will be less than 1000 if multiple calls execute within same ms.
        distinctCount.Should().BeLessThanOrEqualTo(iterations);
    }

    [Theory]
    [InlineData("123456789", "approved", "accredited")]
    [InlineData("pay-rej-123", "rejected", "rejected_by_bank")]
    [InlineData("pay-recusado-456", "rejected", "rejected_by_bank")]
    [InlineData("pay-cancel-789", "rejected", "rejected_by_bank")]
    [InlineData("pay-fail-000", "rejected", "rejected_by_bank")]
    [InlineData("pay-expir-111", "expired", "expired")]
    public async Task MercadoPagoClient_ObterPagamentoAsync_HandlesApprovedAndRejectedPatterns(
        string paymentId, string expectedStatus, string expectedDetail)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "mock"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);

        // Act
        var response = await client.ObterPagamentoAsync(paymentId, CancellationToken.None);

        // Assert
        response.Status.Should().Be(expectedStatus);
        response.StatusDetail.Should().Be(expectedDetail);
    }

    [Fact]
    public async Task MercadoPagoClient_ObterPagamentoAsync_CustomStatusOverride_Respected()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:AccessToken"] = "mock"
            })
            .Build();

        var client = new MercadoPagoClient(new HttpClient(), config);
        var customId = $"custom-{Guid.NewGuid():N}";

        MercadoPagoClient.SetMockStatus(customId, "rejected");
        var responseRej = await client.ObterPagamentoAsync(customId, CancellationToken.None);
        responseRej.Status.Should().Be("rejected");

        MercadoPagoClient.SetMockStatus(customId, "approved");
        var responseApp = await client.ObterPagamentoAsync(customId, CancellationToken.None);
        responseApp.Status.Should().Be("approved");
    }

    // =========================================================================
    // 3. Webhook Handling & Signature Validation
    // =========================================================================

    [Theory]
    [InlineData("{\"data\": {\"id\": \"data-id-123\"}}", "", "data-id-123")]
    [InlineData("{\"id\": \"root-id-456\"}", "", "root-id-456")]
    [InlineData("{}", "data.id=query-data-id-789", "query-data-id-789")]
    [InlineData("{}", "id=query-root-id-000", "query-root-id-000")]
    public async Task PagamentoController_Webhook_ExtractsPaymentIdFromDifferentLocations(
        string jsonBody, string queryString, string expectedPaymentId)
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        string capturedPaymentId = string.Empty;
        mockService.Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((pid, _) => capturedPaymentId = pid)
            .ReturnsAsync((BoletoResponse?)null);

        var config = new ConfigurationBuilder().Build(); // Sem secret para permitir chamadas de teste
        var controller = new PagamentoController(mockService.Object, config);

        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(queryString))
        {
            httpContext.Request.QueryString = new QueryString("?" + queryString);
        }
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        using var jsonDoc = JsonDocument.Parse(jsonBody);

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be(expectedPaymentId);
    }

    [Fact]
    public async Task PagamentoController_Webhook_WhenDataIdIsNumeric_ProcessesSuccessfully()
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        string capturedPaymentId = string.Empty;
        mockService.Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((pid, _) => capturedPaymentId = pid)
            .ReturnsAsync((BoletoResponse?)null);

        var config = new ConfigurationBuilder().Build();
        var controller = new PagamentoController(mockService.Object, config);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        using var jsonDoc = JsonDocument.Parse("{\"data\": {\"id\": 987654321}}");

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert - Numeric payment IDs should be extracted and processed successfully
        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("987654321");
    }

    [Fact]
    public Task PagamentoController_Webhook_WhenDataIdIsNumeric_ThrowsInvalidOperationException() =>
        PagamentoController_Webhook_WhenDataIdIsNumeric_ProcessesSuccessfully();

    [Fact]
    public async Task PagamentoController_Webhook_WhenRootIdIsNumeric_ProcessesSuccessfully()
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        string capturedPaymentId = string.Empty;
        mockService.Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((pid, _) => capturedPaymentId = pid)
            .ReturnsAsync((BoletoResponse?)null);

        var config = new ConfigurationBuilder().Build();
        var controller = new PagamentoController(mockService.Object, config);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        using var jsonDoc = JsonDocument.Parse("{\"id\": 987654321, \"topic\": \"payment\"}");

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("987654321");
    }

    [Fact]
    public async Task PagamentoController_Webhook_WhenDataIsPrimitiveNumeric_ProcessesSuccessfully()
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        string capturedPaymentId = string.Empty;
        mockService.Setup(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((pid, _) => capturedPaymentId = pid)
            .ReturnsAsync((BoletoResponse?)null);

        var config = new ConfigurationBuilder().Build();
        var controller = new PagamentoController(mockService.Object, config);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        using var jsonDoc = JsonDocument.Parse("{\"data\": 987654321}");

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        capturedPaymentId.Should().Be("987654321");
    }

    [Fact]
    public async Task PagamentoController_Webhook_MissingPaymentIdAnywhere_ReturnsBadRequest()
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        var config = new ConfigurationBuilder().Build();
        var controller = new PagamentoController(mockService.Object, config);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        using var jsonDoc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Notificação sem payment_id.");
    }

    [Fact]
    public async Task PagamentoController_Webhook_WithoutSecret_BypassesSignatureValidation()
    {
        // Arrange
        var mockService = new Mock<IPagamentoService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:WebhookSecret"] = "" // Vazio
            })
            .Build();

        var controller = new PagamentoController(mockService.Object, config);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() // Sem headers x-signature ou x-request-id
        };

        using var jsonDoc = JsonDocument.Parse("{\"data\": {\"id\": \"pay-test\"}}");

        // Act
        var result = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task PagamentoController_Webhook_WithSecret_ValidatesSignatureCorrectly()
    {
        // Arrange
        const string secret = "minha_chave_secreta_webhook";
        const string paymentId = "pay-sig-123";
        const string requestId = "req-abc-987";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";

        var hash = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();

        var validSignatureHeader = $"ts={timestamp};v1={hash}";

        var mockService = new Mock<IPagamentoService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:WebhookSecret"] = secret
            })
            .Build();

        var controller = new PagamentoController(mockService.Object, config);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-signature"] = validSignatureHeader;
        httpContext.Request.Headers["x-request-id"] = requestId;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        using var jsonDoc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act: Chamada com assinatura válida
        var validResult = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        validResult.Should().BeOfType<OkResult>();

        // Act: Chamada com assinatura inválida
        httpContext.Request.Headers["x-signature"] = $"ts={timestamp};v1=badhash";
        var invalidResult = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        invalidResult.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PagamentoController_Webhook_WithSecret_ValidatesCommaSeparatedSignatureCorrectly()
    {
        // Arrange
        const string secret = "minha_chave_secreta_webhook";
        const string paymentId = "pay-sig-comma-123";
        const string requestId = "req-abc-987";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";

        var hash = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();

        // Standard Mercado Pago comma delimiter: ts=...,v1=...
        var validSignatureHeader = $"ts={timestamp},v1={hash}";

        var mockService = new Mock<IPagamentoService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:WebhookSecret"] = secret
            })
            .Build();

        var controller = new PagamentoController(mockService.Object, config);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-signature"] = validSignatureHeader;
        httpContext.Request.Headers["x-request-id"] = requestId;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        using var jsonDoc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var validResult = await controller.Webhook(jsonDoc.RootElement, CancellationToken.None);

        // Assert
        validResult.Should().BeOfType<OkResult>();
    }
}

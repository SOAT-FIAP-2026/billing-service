using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Service.Api.Controllers.V1;
using Billing.Service.Application.Interfaces;
using Billing.Service.External.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Billing.Service.Tests.Adversarial;

public class ChallengerWebhookVerificationTests
{
    private readonly Mock<IPagamentoService> _pagamentoServiceMock;

    public ChallengerWebhookVerificationTests()
    {
        _pagamentoServiceMock = new Mock<IPagamentoService>();
    }

    private PagamentoController CreateController(
        string? webhookSecret = null,
        Dictionary<string, StringValues>? queryParams = null,
        Dictionary<string, StringValues>? headerParams = null)
    {
        var configBuilder = new ConfigurationBuilder();
        if (webhookSecret != null)
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MercadoPago:WebhookSecret"] = webhookSecret
            });
        }
        var config = configBuilder.Build();

        var controller = new PagamentoController(_pagamentoServiceMock.Object, config);
        var httpContext = new DefaultHttpContext();

        if (queryParams != null)
        {
            httpContext.Request.Query = new QueryCollection(queryParams);
        }

        if (headerParams != null)
        {
            foreach (var kvp in headerParams)
            {
                httpContext.Request.Headers[kvp.Key] = kvp.Value;
            }
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static string ComputeHmacHex(string secret, string manifest)
    {
        return Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();
    }

    #region 1. Webhook with Query String Fallback

    [Fact]
    public async Task Webhook_WhenBodyIsEmptyObjectAndQueryHasDataId_ExtractsPaymentIdAndReturns200()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "data.id", "987654" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("987654", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenBodyIsEmptyObjectAndQueryHasId_ExtractsPaymentIdAndReturns200()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "id", "543210" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("543210", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenQueryHasBothDataIdAndId_PrefersDataId()
    {
        // Arrange
        var query = new Dictionary<string, StringValues>
        {
            { "data.id", "primary-data-id" },
            { "id", "secondary-id" }
        };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("primary-data-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenQueryDataIdIsWhitespace_FallsBackToQueryId()
    {
        // Arrange
        var query = new Dictionary<string, StringValues>
        {
            { "data.id", "   " },
            { "id", "fallback-id" }
        };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("fallback-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenJsonBodyHasDataId_IgnoresQueryString()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "data.id", "query-id-should-be-ignored" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{\"data\": {\"id\": 999111}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("999111", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenBodyIsArrayAndQueryHasId_ExtractsPaymentIdSafely()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "id", "array-fallback-id" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("[1, 2, 3]");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("array-fallback-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenBodyIsPrimitiveStringAndQueryHasId_ExtractsPaymentIdSafely()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "data.id", "string-body-fallback-id" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("\"just-a-string\"");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("string-body-fallback-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenQueryDataIdIsZero_ExtractsZeroSuccessfully()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { { "data.id", "0" } };
        var controller = CreateController(queryParams: query);
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("0", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 2. Webhook Signature Verification

    [Fact]
    public async Task Webhook_WithSecret_ValidatesCommaSeparatedSignature_Returns200()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_comma_123";
        const string requestId = "req_uuid_comma";
        const string timestamp = "1710000000";

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var hash = ComputeHmacHex(secret, manifest);

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", requestId },
            { "x-signature", $"ts={timestamp},v1={hash}" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithSecret_ValidatesSemicolonSeparatedSignature_Returns200()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_semicolon_456";
        const string requestId = "req_uuid_semicolon";
        const string timestamp = "1720000000";

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var hash = ComputeHmacHex(secret, manifest);

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", requestId },
            { "x-signature", $"ts={timestamp};v1={hash}" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithSecret_SignatureWithMixedSpacesAndInvertedOrder_Returns200()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_permuted_789";
        const string requestId = "req_permuted";
        const string timestamp = "1730000000";

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var hash = ComputeHmacHex(secret, manifest).ToUpperInvariant(); // Test upper case hex

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", requestId },
            { "x-signature", $"  v1 = {hash}  ,   ts = {timestamp}  " }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"id\": \"{paymentId}\"}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithSecret_MixedDelimitersAndTrailingSemicolon_Returns200()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_mixed_delims";
        const string requestId = "req_mixed";
        const string timestamp = "1740000000";

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var hash = ComputeHmacHex(secret, manifest);

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", requestId },
            { "x-signature", $";ts={timestamp};;v1={hash};extra=something," }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithSecret_WhenHashIsInvalid_Returns401Unauthorized()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_invalid_sig";
        const string requestId = "req_uuid";
        const string timestamp = "1710000000";

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", requestId },
            { "x-signature", $"ts={timestamp},v1=deadbeefdeadbeefdeadbeef" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_WithSecret_WhenSignatureHeaderIsMissing_Returns401Unauthorized()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_missing_sig";

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", "some-req-id" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_WithSecret_WhenRequestIdHeaderIsMissing_Returns401Unauthorized()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_missing_req";

        var headers = new Dictionary<string, StringValues>
        {
            { "x-signature", "ts=1710000000,v1=abcdef123456" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_WithSecret_WhenSignatureHeaderIsMalformedWithoutEquals_Returns401Unauthorized()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_malformed_sig";

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", "req-id" },
            { "x-signature", "ts:1710000000;v1:abcdef" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_WithSecret_WhenTsValueIsEmpty_Returns401Unauthorized()
    {
        // Arrange
        const string secret = "test_webhook_secret_key_12345";
        const string paymentId = "pay_empty_ts";

        var headers = new Dictionary<string, StringValues>
        {
            { "x-request-id", "req-id" },
            { "x-signature", "ts=,v1=abcdef" }
        };

        var controller = CreateController(webhookSecret: secret, headerParams: headers);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Webhook_WithoutSecret_SkipsSignatureVerificationAndReturns200()
    {
        // Arrange
        const string paymentId = "pay_no_secret";
        var controller = CreateController(webhookSecret: null);
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 3. Non-Payment and Malformed Webhooks

    [Fact]
    public async Task Webhook_WhenPayloadIsEmptyObjectAndNoQuery_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Notificação sem payment_id.");
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_WhenPayloadIsNonPaymentMerchantOrder_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"type\": \"merchant_order\", \"action\": \"order.created\"}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Notificação sem payment_id.");
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_WhenDataPropertyIsNull_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": null}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataPropertyIsEmptyObject_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": {}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsNull_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": {\"id\": null}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsEmptyString_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": {\"id\": \"\"}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsWhitespaceOnly_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": {\"id\": \"   \\t\\n  \"}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataIsBoolean_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": true}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenDataIsArray_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": [123, 456]}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenRootIdIsNull_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"id\": null}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenRootIdIsArray_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"id\": [\"abc\"]}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenPayloadIsPrimitiveNumberWithNoQuery_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("12345");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenPayloadIsPrimitiveStringWithNoQuery_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("\"not-a-payment\"");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Webhook_WhenPayloadIsDefaultJsonElementWithNoQuery_Returns400BadRequestSafely()
    {
        // Arrange
        var controller = CreateController();
        var defaultElement = default(JsonElement);

        // Act
        var result = await controller.Webhook(defaultElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region 4. Numeric and Primitive Payment IDs & Status Overrides

    [Fact]
    public async Task Webhook_WhenDataIdIsZeroNumber_ExtractsZeroAndProcesses()
    {
        // Arrange
        var controller = CreateController();
        using var doc = JsonDocument.Parse("{\"data\": {\"id\": 0}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync("0", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenDataIdIsHugeInt64Number_ExtractsRawTextCorrectly()
    {
        // Arrange
        var controller = CreateController();
        const string hugeId = "9223372036854775807999";
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": {hugeId}}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(hugeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenDataHasStatusProperty_SetsMockStatusCorrectly()
    {
        // Arrange
        var controller = CreateController();
        const string paymentId = "pay_mock_status_test_1";
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\", \"status\": \"rejected\"}}}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WhenRootHasStatusProperty_SetsMockStatusCorrectly()
    {
        // Arrange
        var controller = CreateController();
        const string paymentId = "pay_mock_status_test_2";
        using var doc = JsonDocument.Parse($"{{\"data\": {{\"id\": \"{paymentId}\"}}, \"status\": \"cancelled\"}}");

        // Act
        var result = await controller.Webhook(doc.RootElement, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkResult>();
        _pagamentoServiceMock.Verify(s => s.AtualizarPorNotificacaoAsync(paymentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

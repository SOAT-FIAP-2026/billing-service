using System.Net;
using System.Net.Http;
using System.Text;
using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.External.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

namespace Billing.Service.Tests.External.Services;

public class MercadoPagoClientTests
{
    [Fact]
    public async Task CriarBoletoAsync_DeveEnviarTokenEIdempotencia()
    {
        HttpRequestMessage? request = null;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"id":"mp-1","status":"pending","status_detail":"pending","transaction_details":{"external_resource_url":"https://boleto","barcode":{"content":"34191"}},"date_of_expiration":"2026-11-05T23:59:59Z"}""",
                    Encoding.UTF8, "application/json")
            });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MercadoPago:AccessToken"] = "token" })
            .Build();
        var client = new MercadoPagoClient(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.mercadopago.com/")
        }, configuration);

        await client.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(100m, "desc", "ref", "cliente@teste.com", "Cliente", "Teste", "12345678909", null),
            CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal("Bearer", request!.Headers.Authorization?.Scheme);
        Assert.Equal("token", request.Headers.Authorization?.Parameter);
        Assert.True(request.Headers.Contains("X-Idempotency-Key"));
    }
}

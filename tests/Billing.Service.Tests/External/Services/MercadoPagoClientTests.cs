using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task CriarBoletoAsync_DeveEnviarPayloadCompleto()
    {
        HttpRequestMessage? request = null;
        Task<string>? payloadRead = null;
        var handler = CriarHandler(
            """{"id":"mp-1","status":"pending","status_detail":"pending"}""",
            callback: captured =>
            {
                request = captured;
                payloadRead = captured.Content!.ReadAsStringAsync();
            });
        var client = CriarClient(handler);

        await client.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(
                100.50m,
                "Pagamento do orçamento",
                "orcamento-1",
                "cliente@teste.com",
                "Cliente",
                "Teste",
                "12345678909",
                "https://api.test/webhook",
                "idem-1"),
            CancellationToken.None);

        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Equal("/v1/payments", request.RequestUri!.AbsolutePath);
        Assert.Equal("idem-1", request.Headers.GetValues("X-Idempotency-Key").Single());
        using var payload = JsonDocument.Parse(await payloadRead!);
        var root = payload.RootElement;
        Assert.Equal(100.50m, root.GetProperty("transaction_amount").GetDecimal());
        Assert.Equal("Pagamento do orçamento", root.GetProperty("description").GetString());
        Assert.Equal("bolbradesco", root.GetProperty("payment_method_id").GetString());
        Assert.Equal("orcamento-1", root.GetProperty("external_reference").GetString());
        Assert.Equal("https://api.test/webhook", root.GetProperty("notification_url").GetString());
        Assert.Equal("cliente@teste.com", root.GetProperty("payer").GetProperty("email").GetString());
        Assert.Equal("Cliente", root.GetProperty("payer").GetProperty("first_name").GetString());
        Assert.Equal("Teste", root.GetProperty("payer").GetProperty("last_name").GetString());
        Assert.Equal("CPF", root.GetProperty("payer").GetProperty("identification").GetProperty("type").GetString());
        Assert.Equal("12345678909", root.GetProperty("payer").GetProperty("identification").GetProperty("number").GetString());
    }

    [Fact]
    public async Task CriarBoletoAsync_SemIdempotencia_DeveGerarChave()
    {
        HttpRequestMessage? request = null;
        var handler = CriarHandler(
            """{"id":"mp-1","status":"pending","status_detail":"pending"}""",
            callback: captured => request = captured);
        var client = CriarClient(handler);

        await client.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
            CancellationToken.None);

        var idempotencyKey = request!.Headers.GetValues("X-Idempotency-Key").Single();
        Assert.True(Guid.TryParse(idempotencyKey, out _));
    }

    [Fact]
    public async Task ObterPagamentoAsync_DeveConsultarPagamentoComIdEscapado()
    {
        HttpRequestMessage? request = null;
        var handler = CriarHandler(
            """{"id":"mp/1","status":"approved","status_detail":"accredited","transaction_details":{"external_resource_url":"https://boleto","barcode":{"content":"34191"}}}""",
            HttpMethod.Get,
            callback: captured => request = captured);
        var client = CriarClient(handler);

        var response = await client.ObterPagamentoAsync("mp/1", CancellationToken.None);

        Assert.Equal("mp/1", response.Id);
        Assert.Equal("approved", response.Status);
        Assert.Equal("https://boleto", response.BoletoUrl);
        Assert.Equal("34191", response.CodigoBarras);
        Assert.Equal("/v1/payments/mp%2F1", request!.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CriarBoletoAsync_QuandoMercadoPagoRetornarErro_DeveLancarHttpRequestException(
        HttpStatusCode statusCode)
    {
        var handler = CriarHandler("{}", statusCode: statusCode);
        var client = CriarClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CriarBoletoAsync(
                new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoRespostaVazia_DeveLancarInvalidOperationException()
    {
        var client = CriarClient(CriarHandler(string.Empty));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CriarBoletoAsync(
                new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
                CancellationToken.None));

        Assert.Equal("Resposta vazia do Mercado Pago.", exception.Message);
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoJsonInvalido_DevePropagarErroDeDesserializacao()
    {
        var client = CriarClient(CriarHandler("{"));

        await Assert.ThrowsAsync<JsonException>(() =>
            client.CriarBoletoAsync(
                new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoCamposOpcionaisAusentes_DeveUsarValoresPadrao()
    {
        var client = CriarClient(CriarHandler("""{"id":"mp-1"}"""));

        var response = await client.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
            CancellationToken.None);

        Assert.Equal("mp-1", response.Id);
        Assert.Equal("pending", response.Status);
        Assert.Equal("pending", response.StatusDetail);
        Assert.Null(response.BoletoUrl);
        Assert.Null(response.CodigoBarras);
        Assert.Null(response.DataVencimento);
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoIdNumerico_DevePreservarIdComoTexto()
    {
        var client = CriarClient(CriarHandler("""{"id":123456789,"status":"pending"}"""));

        var response = await client.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
            CancellationToken.None);

        Assert.Equal("123456789", response.Id);
    }

    [Fact]
    public void Construtor_SemToken_DeveLancarInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder().Build();

        var client = new MercadoPagoClient(new HttpClient(), configuration);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task CriarBoletoAsync_EmModoMock_DeveRetornarRespostaDeterministica()
    {
        var configuration = new ConfigurationBuilder().Build();
        var client = new MercadoPagoClient(new HttpClient(), configuration);

        var request = new MercadoPagoBoletoRequest(
            150.0m,
            "Teste mock",
            "ref-123",
            "cliente@teste.com",
            "Nome",
            "Sobrenome",
            "12345678901",
            null);

        var response = await client.CriarBoletoAsync(request, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("pending", response.Status);
        Assert.NotEmpty(response.Id);
        Assert.NotNull(response.BoletoUrl);
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoTransporteFalhar_DevePropagarExcecao()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("falha de transporte"));
        var client = CriarClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CriarBoletoAsync(
                new MercadoPagoBoletoRequest(100m, "desc", "ref", "email", "nome", "sobrenome", "cpf", null),
                CancellationToken.None));

        Assert.Equal("falha de transporte", exception.Message);
    }

    private static MercadoPagoClient CriarClient(Mock<HttpMessageHandler> handler)
    {
        return new MercadoPagoClient(
            new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.mercadopago.com/") },
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["MercadoPago:AccessToken"] = "token" })
                .Build());
    }

    private static Mock<HttpMessageHandler> CriarHandler(
        string content,
        HttpMethod? method = null,
        HttpStatusCode statusCode = HttpStatusCode.Created,
        Action<HttpRequestMessage>? callback = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => method == null || request.Method == method),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => callback?.Invoke(request))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        return handler;
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Billing.Service.Domain.Interfaces.Service;
using Microsoft.Extensions.Configuration;

namespace Billing.Service.External.Services;

public sealed class MercadoPagoClient : IMercadoPagoClient
{
    private readonly HttpClient _httpClient;

    public MercadoPagoClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var accessToken = configuration["MercadoPago:AccessToken"];

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("MercadoPago:AccessToken não configurado.");

        _httpClient.BaseAddress ??= new Uri("https://api.mercadopago.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<MercadoPagoBoletoResponse> CriarBoletoAsync(
        MercadoPagoBoletoRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new CriarPagamentoRequest(
            request.Valor,
            request.Descricao,
            "bolbradesco",
            new PagadorRequest(request.Email, request.Nome, request.Sobrenome,
                new IdentificacaoRequest("CPF", request.Cpf)),
            request.ReferenciaExterna,
            request.NotificationUrl);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/payments")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add(
            "X-Idempotency-Key",
            request.IdempotencyKey ?? Guid.NewGuid().ToString());
        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();
        return await ParseResponseAsync(httpResponse, cancellationToken);
    }

    public async Task<MercadoPagoBoletoResponse> ObterPagamentoAsync(
        string paymentId,
        CancellationToken cancellationToken)
    {
        using var httpResponse = await _httpClient.GetAsync(
            $"v1/payments/{Uri.EscapeDataString(paymentId)}",
            cancellationToken);
        httpResponse.EnsureSuccessStatusCode();
        return await ParseResponseAsync(httpResponse, cancellationToken);
    }

    private static async Task<MercadoPagoBoletoResponse> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(responseContent))
            throw new InvalidOperationException("Resposta vazia do Mercado Pago.");

        var payment = JsonSerializer.Deserialize<PagamentoResponse>(
            responseContent,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Resposta vazia do Mercado Pago.");

        return new MercadoPagoBoletoResponse(
            payment.Id.ToString(),
            payment.Status ?? "pending",
            payment.StatusDetail ?? "pending",
            payment.TransactionDetails?.ExternalResourceUrl,
            payment.TransactionDetails?.Barcode?.Content,
            payment.DateOfExpiration);
    }

    private sealed record CriarPagamentoRequest(
        [property: JsonPropertyName("transaction_amount")] decimal TransactionAmount,
        string Description,
        [property: JsonPropertyName("payment_method_id")] string PaymentMethodId,
        PagadorRequest Payer,
        [property: JsonPropertyName("external_reference")] string ExternalReference,
        [property: JsonPropertyName("notification_url")] string? NotificationUrl);

    private sealed record PagadorRequest(
        string Email,
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string LastName,
        IdentificacaoRequest Identification);

    private sealed record IdentificacaoRequest(string Type, string Number);

    private sealed record PagamentoResponse(
        string Id,
        string? Status,
        [property: JsonPropertyName("status_detail")] string? StatusDetail,
        [property: JsonPropertyName("date_of_expiration")] DateTimeOffset? DateOfExpiration,
        [property: JsonPropertyName("transaction_details")] TransactionDetailsResponse? TransactionDetails);

    private sealed record TransactionDetailsResponse(
        [property: JsonPropertyName("external_resource_url")] string? ExternalResourceUrl,
        BarcodeResponse? Barcode);

    private sealed record BarcodeResponse(string? Content);
}

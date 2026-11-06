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
    private readonly bool _isMock;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MercadoPagoBoletoResponse> MockPayments = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> MockStatusOverrides = new();

    public static void SetMockStatus(string paymentId, string status)
    {
        MockStatusOverrides[paymentId] = status;
    }

    public MercadoPagoClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var accessToken = configuration["MercadoPago:AccessToken"];

        _isMock = string.IsNullOrWhiteSpace(accessToken) ||
                  accessToken.Equals("mock", StringComparison.OrdinalIgnoreCase) ||
                  accessToken.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                  accessToken.Equals("offline", StringComparison.OrdinalIgnoreCase);

        if (!_isMock)
        {
            _httpClient.BaseAddress ??= new Uri("https://api.mercadopago.com/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    public async Task<MercadoPagoBoletoResponse> CriarBoletoAsync(
        MercadoPagoBoletoRequest request,
        CancellationToken cancellationToken)
    {
        if (_isMock)
        {
            var paymentId = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100, 999)}-{Guid.NewGuid().ToString("N")[..6]}";
            var boletoResponse = new MercadoPagoBoletoResponse(
                paymentId,
                "pending",
                "pending_waiting_payment",
                $"https://mercadopago.fake/boleto/{paymentId}",
                "341917900101043510047910201500098910000012000",
                DateTimeOffset.UtcNow.AddDays(3));

            MockPayments[paymentId] = boletoResponse;
            return boletoResponse;
        }

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
        if (_isMock)
        {
            string status;
            string statusDetail;

            if (MockStatusOverrides.TryGetValue(paymentId, out var customStatus))
            {
                status = customStatus.ToLowerInvariant();
                statusDetail = status switch
                {
                    "approved" or "aprovado" => "accredited",
                    "rejected" or "rejeitado" => "rejected_by_bank",
                    "cancelled" or "cancelado" => "cancelled",
                    "expired" or "expirado" => "expired",
                    _ => status
                };
            }
            else if (paymentId.Contains("rej", StringComparison.OrdinalIgnoreCase) ||
                     paymentId.Contains("recus", StringComparison.OrdinalIgnoreCase) ||
                     paymentId.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
                     paymentId.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                status = "rejected";
                statusDetail = "rejected_by_bank";
            }
            else if (paymentId.Contains("expir", StringComparison.OrdinalIgnoreCase))
            {
                status = "expired";
                statusDetail = "expired";
            }
            else
            {
                status = "approved";
                statusDetail = "accredited";
            }

            MockPayments.TryGetValue(paymentId, out var existing);

            var response = new MercadoPagoBoletoResponse(
                paymentId,
                status,
                statusDetail,
                existing?.BoletoUrl ?? $"https://mercadopago.fake/boleto/{paymentId}",
                existing?.CodigoBarras ?? "341917900101043510047910201500098910000012000",
                existing?.DataVencimento ?? DateTimeOffset.UtcNow.AddDays(3));

            return response;
        }

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
            GetPaymentId(payment.Id),
            payment.Status ?? "pending",
            payment.StatusDetail ?? "pending",
            payment.TransactionDetails?.ExternalResourceUrl,
            payment.TransactionDetails?.Barcode?.Content,
            payment.DateOfExpiration);
    }

    private static string GetPaymentId(JsonElement id)
    {
        var paymentId = id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.GetRawText(),
            _ => null
        };

        return string.IsNullOrWhiteSpace(paymentId)
            ? throw new InvalidOperationException("Resposta do Mercado Pago sem id de pagamento.")
            : paymentId;
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
        JsonElement Id,
        string? Status,
        [property: JsonPropertyName("status_detail")] string? StatusDetail,
        [property: JsonPropertyName("date_of_expiration")] DateTimeOffset? DateOfExpiration,
        [property: JsonPropertyName("transaction_details")] TransactionDetailsResponse? TransactionDetails);

    private sealed record TransactionDetailsResponse(
        [property: JsonPropertyName("external_resource_url")] string? ExternalResourceUrl,
        BarcodeResponse? Barcode);

    private sealed record BarcodeResponse(string? Content);
}

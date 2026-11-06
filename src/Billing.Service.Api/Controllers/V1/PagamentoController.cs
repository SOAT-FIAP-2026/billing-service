using Fiap.TechChallenge.Contracts.BillingService;
using Billing.Service.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Billing.Service.Api.Controllers.V1;

[ApiController]
[Route("api/v1/pagamento")]
public class PagamentoController(
    IPagamentoService pagamentoService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("boleto")]
    [ProducesResponseType(typeof(BoletoResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BoletoResponse>> CriarBoleto(
        [FromBody] CriarBoletoRequest request,
        CancellationToken cancellationToken)
    {
        var response = await pagamentoService.CriarBoletoAsync(request, cancellationToken);
        return CreatedAtAction(nameof(VerificarPagamento), new { id = response.PagamentoId }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoletoResponse>> VerificarPagamento(Guid id)
    {
        var response = await pagamentoService.BuscarAsync(id);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook(
        [FromBody] JsonElement notification,
        CancellationToken cancellationToken)
    {
        if (!TryGetPaymentId(notification, out var paymentId) &&
            !TryGetPaymentIdFromQuery(out paymentId))
            return BadRequest("Notificação sem payment_id.");
        if (!ValidarAssinatura(notification, paymentId))
            return Unauthorized();

        if (notification.ValueKind == JsonValueKind.Object)
        {
            string? status = null;
            if (notification.TryGetProperty("status", out var statusProp))
                status = ExtractJsonId(statusProp);
            else if (notification.TryGetProperty("data", out var d) &&
                     d.ValueKind == JsonValueKind.Object &&
                     d.TryGetProperty("status", out var dataStatusProp))
                status = ExtractJsonId(dataStatusProp);

            if (!string.IsNullOrWhiteSpace(status))
            {
                Billing.Service.External.Services.MercadoPagoClient.SetMockStatus(paymentId, status);
            }
        }

        await pagamentoService.AtualizarPorNotificacaoAsync(paymentId, cancellationToken);
        return Ok();
    }

    private bool TryGetPaymentId(JsonElement notification, out string paymentId)
    {
        paymentId = string.Empty;
        if (notification.ValueKind == JsonValueKind.Object)
        {
            if (notification.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out var id))
                {
                    paymentId = ExtractJsonId(id);
                }
                else if (data.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    paymentId = ExtractJsonId(data);
                }
            }

            if (string.IsNullOrWhiteSpace(paymentId) &&
                notification.TryGetProperty("id", out var rootId))
            {
                paymentId = ExtractJsonId(rootId);
            }
        }

        if (string.IsNullOrWhiteSpace(paymentId) && Request?.Query != null)
        {
            if (Request.Query.TryGetValue("data.id", out var queryDataId) && !string.IsNullOrWhiteSpace(queryDataId))
            {
                paymentId = queryDataId.ToString();
            }
            else if (Request.Query.TryGetValue("id", out var queryId) && !string.IsNullOrWhiteSpace(queryId))
            {
                paymentId = queryId.ToString();
            }
        }

        return !string.IsNullOrWhiteSpace(paymentId);
    }

    private bool TryGetPaymentIdFromQuery(out string paymentId)
    {
        paymentId = Request.Query["data.id"].ToString();
        return !string.IsNullOrWhiteSpace(paymentId);
    }

    private static string ExtractJsonId(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        _ => string.Empty
    };

    private bool ValidarAssinatura(JsonElement notification, string paymentId)
    {
        var secret = configuration["MercadoPago:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return true;

        var signature = Request.Headers["x-signature"].ToString();
        var requestId = Request.Headers["x-request-id"].ToString();
        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(requestId))
            return false;

        var parts = signature.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var timestamp = parts
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part.Length == 2 && part[0].Trim().Equals("ts", StringComparison.OrdinalIgnoreCase))?[1].Trim();
        var receivedHash = parts
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part.Length == 2 && part[0].Trim().Equals("v1", StringComparison.OrdinalIgnoreCase))?[1].Trim();
        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(receivedHash))
            return false;

        var manifest = $"id:{paymentId};request-id:{requestId};ts:{timestamp};";
        var expectedHash = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest)))
            .ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(receivedHash.ToLowerInvariant()));
    }
}

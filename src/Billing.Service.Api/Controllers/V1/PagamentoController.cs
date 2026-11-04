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
        if (!TryGetPaymentId(notification, out var paymentId))
            return BadRequest("Notificação sem payment_id.");
        if (!ValidarAssinatura(notification, paymentId))
            return Unauthorized();

        await pagamentoService.AtualizarPorNotificacaoAsync(paymentId, cancellationToken);
        return Ok();
    }

    private static bool TryGetPaymentId(JsonElement notification, out string paymentId)
    {
        paymentId = string.Empty;
        if (notification.TryGetProperty("data", out var data) &&
            data.TryGetProperty("id", out var id))
        {
            paymentId = id.GetString() ?? id.GetRawText();
        }
        else if (notification.TryGetProperty("id", out var rootId))
        {
            paymentId = rootId.GetString() ?? rootId.GetRawText();
        }

        return !string.IsNullOrWhiteSpace(paymentId);
    }

    private bool ValidarAssinatura(JsonElement notification, string paymentId)
    {
        var secret = configuration["MercadoPago:WebhookSecret"];
        var signature = Request.Headers["x-signature"].ToString();
        var requestId = Request.Headers["x-request-id"].ToString();
        if (string.IsNullOrWhiteSpace(secret) ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(requestId))
            return false;

        var timestamp = signature.Split(';')
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part.Length == 2 && part[0] == "ts")?[1];
        var receivedHash = signature.Split(';')
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(part => part.Length == 2 && part[0] == "v1")?[1];
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

namespace Billing.Service.Application.DTOs.Responses;

public sealed record BoletoResponse(
    Guid PagamentoId,
    string PaymentId,
    string Status,
    decimal Valor,
    string? BoletoUrl,
    string? CodigoBarras,
    DateTimeOffset? DataVencimento);

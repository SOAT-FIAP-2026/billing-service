namespace Billing.Service.Domain.Interfaces.Service;

public record MercadoPagoBoletoRequest(
    decimal Valor,
    string Descricao,
    string ReferenciaExterna,
    string Email,
    string Nome,
    string Sobrenome,
    string Cpf,
    string? NotificationUrl,
    string? IdempotencyKey = null,
    string? Cep = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Estado = null);

public record MercadoPagoBoletoResponse(
    string Id,
    string Status,
    string StatusDetail,
    string? BoletoUrl,
    string? CodigoBarras,
    DateTimeOffset? DataVencimento);

public interface IMercadoPagoClient
{
    Task<MercadoPagoBoletoResponse> CriarBoletoAsync(
        MercadoPagoBoletoRequest request,
        CancellationToken cancellationToken);

    Task<MercadoPagoBoletoResponse> ObterPagamentoAsync(
        string paymentId,
        CancellationToken cancellationToken);
}

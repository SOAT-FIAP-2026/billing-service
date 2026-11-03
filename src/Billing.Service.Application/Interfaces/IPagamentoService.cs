using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.DTOs.Responses;

namespace Billing.Service.Application.Interfaces;

public interface IPagamentoService
{
    Task<BoletoResponse> CriarBoletoAsync(
        CriarBoletoRequest request,
        CancellationToken cancellationToken);

    Task<BoletoResponse?> AtualizarPorNotificacaoAsync(
        string paymentId,
        CancellationToken cancellationToken);

    Task<BoletoResponse?> BuscarAsync(Guid pagamentoId);
}

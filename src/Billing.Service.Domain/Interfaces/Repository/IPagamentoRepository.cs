using Billing.Service.Domain.Entities;

namespace Billing.Service.Domain.Interfaces.Repository;

public interface IPagamentoRepository
{
    Task AdicionarAsync(Pagamento pagamento);
    Task<Pagamento?> ObterPorIdAsync(Guid id);
    Task<Pagamento?> ObterPorIdMercadoPagoAsync(string idMercadoPago);
    Task AtualizarAsync(Pagamento pagamento);
}

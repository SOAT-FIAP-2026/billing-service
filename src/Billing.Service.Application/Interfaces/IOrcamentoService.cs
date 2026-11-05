using Billing.Service.Domain.Entities;
using Fiap.TechChallenge.Contracts.BillingService;

namespace Billing.Service.Application.Interfaces
{
    public interface IOrcamentoService
    {
        Task<Orcamento> GerarOrcamento(
            Guid idOrdemServico,
            IReadOnlyCollection<ItemCalculoOrcamentoDto> servicos,
            IReadOnlyCollection<ItemCalculoOrcamentoDto> pecasInsumo,
            CancellationToken cancellationToken = default);

        Task<Orcamento> GerarOrcamento(Guid idOrdemServico, IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo);
        Task<string> EnviarOrcamento(Guid idOrcamento, string emailCliente);
        Task<Orcamento> BuscarOrcamento(Guid idOrcamento);
        Task<IReadOnlyCollection<Orcamento>> BuscarTodosOrcamentos(int page, int pageSize);
    }
}

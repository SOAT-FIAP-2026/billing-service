using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Domain.Entities;

namespace Billing.Service.Application.Interfaces
{
    public interface IOrcamentoService
    {
        Task<Orcamento> GerarOrcamento(Guid idOrdemServico, IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo);
        Task<string> EnviarOrcamento(Guid idOrcamento, string emailCliente);
        Task<Orcamento> BuscarOrcamento(Guid idOrcamento);
        Task<IReadOnlyCollection<Orcamento>> BuscarTodosOrcamentos(int page, int pageSize);
    }
}

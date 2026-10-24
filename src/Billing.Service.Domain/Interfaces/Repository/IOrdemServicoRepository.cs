using Billing.Service.Domain.Entities;

namespace Billing.Service.Domain.Interfaces.Repository
{
    public interface IOrdemServicoRepository
    {
        Task Adicionar(OrdemServico ordemServico);
        Task Atualizar(OrdemServico ordemServico);
        Task<OrdemServico?> ObterPorId(Guid id);
        Task<IReadOnlyCollection<OrdemServico>> ObterTodos();
        Task<IReadOnlyCollection<ItemServico>> ObterItensServicoFinalizados();
    }
}

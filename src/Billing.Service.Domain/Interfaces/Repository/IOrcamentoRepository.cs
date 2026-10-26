using Billing.Service.Domain.Entities;

namespace Billing.Service.Domain.Interfaces.Repository
{
    public interface IOrcamentoRepository
    {
        Task Adicionar(Orcamento orcamento);
        Task Atualizar(Orcamento orcamento);
        Task<Orcamento?> ObterPorId(Guid id);
        Task<IReadOnlyCollection<Orcamento>> ObterTodos(int page, int pageSize);
    }
}

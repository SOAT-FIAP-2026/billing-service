using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Service.Infrastructure.Repositories
{
    public class OrcamentoRepository(ApplicationDbContext context) : IOrcamentoRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task Adicionar(Orcamento orcamento)
        {
            await _context.Orcamentos.AddAsync(orcamento);
            await _context.SaveChangesAsync();
        }

        public async Task Atualizar(Orcamento orcamento)
        {
            _context.Orcamentos.Update(orcamento);
            await _context.SaveChangesAsync();
        }

        public async Task<Orcamento?> ObterPorId(Guid id)
        {
            return await _context.Orcamentos
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Orcamento?> ObterPorOrdemServicoId(Guid idOrdemServico)
        {
            return await _context.Orcamentos
                .FirstOrDefaultAsync(o => o.IdOrdemServico == idOrdemServico);
        }

        public async Task<IReadOnlyCollection<Orcamento>> ObterTodos(int page, int pageSize)
        {
            return await _context.Orcamentos
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}

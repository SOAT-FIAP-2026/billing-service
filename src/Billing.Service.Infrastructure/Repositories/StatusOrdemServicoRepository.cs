using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.ValueObjects;
using Billing.Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Service.Infrastructure.Repositories
{
    public class StatusOrdemServicoRepository(ApplicationDbContext context) : IStatusOrdemServicoRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<StatusOrdemServico?> ObterPorCodigo(CodigoVO codigo)
        {
            return await _context.StatusOrdensServico.FirstOrDefaultAsync(s => s.Codigo == codigo);
        }
    }
}

using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Service.Infrastructure.Repositories;

public sealed class PagamentoRepository(ApplicationDbContext context) : IPagamentoRepository
{
    public async Task AdicionarAsync(Pagamento pagamento)
    {
        await context.Pagamentos.AddAsync(pagamento);
        await context.SaveChangesAsync();
    }

    public Task<Pagamento?> ObterPorIdAsync(Guid id) =>
        context.Pagamentos.FirstOrDefaultAsync(x => x.Id == id);

    public Task<Pagamento?> ObterPorIdMercadoPagoAsync(string idMercadoPago) =>
        context.Pagamentos.FirstOrDefaultAsync(x => x.IdMercadoPago == idMercadoPago);

    public async Task AtualizarAsync(Pagamento pagamento)
    {
        context.Pagamentos.Update(pagamento);
        await context.SaveChangesAsync();
    }
}

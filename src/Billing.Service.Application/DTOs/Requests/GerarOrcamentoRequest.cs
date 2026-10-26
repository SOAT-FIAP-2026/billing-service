using Billing.Service.Domain.Entities;

namespace Billing.Service.Application.DTOs.Requests
{
    public class GerarOrcamentoRequest
    {
        public IReadOnlyCollection<Servico> Servicos { get; set; }
        public IReadOnlyCollection<PecaInsumo> PecasInsumo { get; set; }
    }
}

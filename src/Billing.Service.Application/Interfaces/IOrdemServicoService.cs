using Billing.Service.Application.DTOs.Responses;
using Billing.Service.Application.DTOs.Requests;

namespace Billing.Service.Application.Interfaces
{
    public interface IOrdemServicoService
    {
        Task<OrdemServicoResponse> AprovarOrcamento(Guid id, bool aprovado);
    }
}

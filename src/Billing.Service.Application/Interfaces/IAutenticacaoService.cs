using Billing.Service.Application.DTOs.Requests;

namespace Billing.Service.Application.Interfaces
{
    public interface IAutenticacaoService
    {
        Task<LoginResponse> Login(LoginRequest request);
    }
}

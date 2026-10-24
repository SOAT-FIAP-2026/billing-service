using Billing.Service.Domain.Entities;
    
namespace Billing.Service.Domain.Interfaces.Service
{
    public interface ITokenService
    {
        string GerarToken(Usuario usuario);
    }
}

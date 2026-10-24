using Billing.Service.Domain.Entities;
using Billing.Service.Domain.ValueObjects;

namespace Billing.Service.Domain.Interfaces.Repository
{
    public interface IUsuarioRepository
    {
        Task<Usuario?> ObterPorLogin(EmailVO email);
        Task<bool> ExisteEmail(EmailVO email);
        Task Adicionar(Usuario usuario);
    }
}

using Billing.Service.Domain.Interfaces.Security;

namespace Billing.Service.Infrastructure.Security
{
    public class Crypto : ICrypto
    {
        public string CriptografarSenha(string senha) => BCrypt.Net.BCrypt.HashPassword(senha);
        public bool VerificarSenha(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
    }
}
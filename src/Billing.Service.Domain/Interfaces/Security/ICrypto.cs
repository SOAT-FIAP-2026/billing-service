namespace Billing.Service.Domain.Interfaces.Security
{
    public interface ICrypto
    {
        string CriptografarSenha(string senha);
        bool VerificarSenha(string senha, string hash);
    }
}
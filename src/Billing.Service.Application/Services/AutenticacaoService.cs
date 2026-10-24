using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Security;
using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.Domain.ValueObjects;

namespace Billing.Service.Application.Services
{
    public class AutenticacaoService: IAutenticacaoService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ITokenService _tokenService;
        private readonly ICrypto _cryptoService;

        public AutenticacaoService(
            IUsuarioRepository usuarioRepository,
            ITokenService tokenService,
            ICrypto cryptoService
        ) 
        { 
            _usuarioRepository = usuarioRepository;
            _tokenService = tokenService;
            _cryptoService = cryptoService;
        }

        public async Task<LoginResponse> Login(LoginRequest request)
        {

            EmailVO email = new(request.Login);
            var usuario = await _usuarioRepository.ObterPorLogin(email);

            if (usuario == null || 
                !_cryptoService.VerificarSenha(request.Senha, usuario.Senha.Hash))
                throw new UnauthorizedAccessException("Usuário ou senha inválidos.");

            var token = _tokenService.GerarToken(usuario);
            return new LoginResponse(token, usuario.Nome.Valor);
        }
    }
}

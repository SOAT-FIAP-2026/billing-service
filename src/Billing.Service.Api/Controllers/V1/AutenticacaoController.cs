using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Service.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AutenticacaoController : ControllerBase
    {
        private readonly IAutenticacaoService _autenticacaoService;

        public AutenticacaoController(
            IAutenticacaoService autenticacaoService
        )
        {
            _autenticacaoService = autenticacaoService;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            LoginResponse login = await _autenticacaoService.Login(request);

            return Ok(login);
        }
    }
}

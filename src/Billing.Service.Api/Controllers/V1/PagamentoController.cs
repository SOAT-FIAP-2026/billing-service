using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Service.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/pagamento")]
    //public class PagamentoController(IPagamentoService pagamentoService) : ControllerBase
    public class PagamentoController : ControllerBase
    {
        //private readonly IPagamentoService _pagamentoService = pagamentoService;

        [HttpPost]
        public async Task<IActionResult> RealizarPagamento(Guid id)
        {
            //var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> VerificarPagamento(Guid id)
        {
            //var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok();
        }
    }
}

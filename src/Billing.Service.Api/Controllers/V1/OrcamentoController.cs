using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Billing.Service.Application.DTOs.Common;

namespace Billing.Service.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/orcamento")]
    public class OrcamentoController(IOrcamentoService orcamentoService) : ControllerBase
    {
        private readonly IOrcamentoService _orcamentoService = orcamentoService;

        [HttpPost]
        public async Task<IActionResult> GerarOrcamento(Guid idOrdemServico, [FromBody] GerarOrcamentoRequest request)
        {
            var orcamento = await _orcamentoService.GerarOrcamento(idOrdemServico, request.Servicos, request.PecasInsumo);

            return Ok(orcamento);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarOrcamento(Guid idOrcamento)
        {
            var orcamento = await _orcamentoService.BuscarOrcamento(idOrcamento);

            return Ok(orcamento);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarTodosOrcamentos(PagedRequest pagedRequest)
        {
            var orcamentos = await _orcamentoService.BuscarTodosOrcamentos(pagedRequest.Page, pagedRequest.PageSize);

            return Ok(orcamentos);
        }

        [HttpPost("{id:guid}/enviar-orcamento")]
        public async Task<IActionResult> EnviarOrcamento(Guid idOrcamento, [FromBody] EnviarOrcamentoRequest request)
        {
            string resultado = await _orcamentoService.EnviarOrcamento(idOrcamento, request.EmailCliente);

            return Ok(resultado);
        }
    }
}
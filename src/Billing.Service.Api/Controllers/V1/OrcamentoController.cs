using Billing.Service.Application.DTOs.Common;
using Billing.Service.Application.Interfaces;
using Fiap.TechChallenge.Contracts.BillingService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Billing.Service.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/orcamento")]
    public class OrcamentoController(IOrcamentoService orcamentoService) : ControllerBase
    {
        private readonly IOrcamentoService _orcamentoService = orcamentoService;

        [HttpPost]
        public async Task<IActionResult> GerarOrcamento(
            [FromQuery] Guid? idOrdemServico,
            [FromBody] GerarOrcamentoRequest request,
            CancellationToken cancellationToken)
        {
            var osId = (idOrdemServico.HasValue && idOrdemServico.Value != Guid.Empty)
                ? idOrdemServico.Value
                : Guid.NewGuid();

            var orcamento = await _orcamentoService.GerarOrcamento(osId, request.Servicos, request.PecasInsumo, cancellationToken);

            return Ok(orcamento);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> BuscarOrcamento(Guid id)
        {
            var orcamento = await _orcamentoService.BuscarOrcamento(id);

            return Ok(orcamento);
        }

        [HttpGet("listar")]
        public async Task<IActionResult> BuscarTodosOrcamentos([FromQuery] PagedRequest pagedRequest)
        {
            var orcamentos = await _orcamentoService.BuscarTodosOrcamentos(pagedRequest.Page, pagedRequest.PageSize);

            return Ok(orcamentos);
        }

        [HttpPost("{id:guid}/enviar-orcamento")]
        public async Task<IActionResult> EnviarOrcamento(Guid id, [FromBody] EnviarOrcamentoRequest request)
        {
            string resultado = await _orcamentoService.EnviarOrcamento(id, request.EmailCliente);

            return Ok(resultado);
        }
    }
}
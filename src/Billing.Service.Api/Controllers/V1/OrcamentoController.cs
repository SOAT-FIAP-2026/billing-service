using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.Service.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/orcamento")]
    public class OrcamentoController(IOrcamentoService orcamentoService) : ControllerBase
    {
        private readonly IOrcamentoService _orcamentoService = ordemServicoService;

        [HttpPost]
        public async Task<IActionResult> GerarOrcamento(Guid id)
        {
            //var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> BuscarOrcamento(Guid id)
        {
            //var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok();
        }

        [HttpPost("{id:guid}/enviar-orcamento")]
        public async Task<IActionResult> EnviarOrcamento(Guid id)
        {
            //var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok();
        }

        [HttpPatch("{id:guid}/aprovar-orcamento")]
        public async Task<IActionResult> AprovarOrcamento(Guid id, bool aprovado)
        {
            var ordemServico = await _ordemServicoService.AprovarOrcamento(id, aprovado);

            return Ok(ordemServico);
        }
    }
}

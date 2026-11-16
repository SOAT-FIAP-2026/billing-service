using Billing.Service.Application.DTOs.Common;
using Billing.Service.Application.Interfaces;
using Fiap.TechChallenge.Contracts.BillingService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BuscarOrcamento(Guid id)
        {
            var orcamento = await _orcamentoService.BuscarOrcamento(id);

            return Ok(orcamento);
        }

        [HttpGet("listar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BuscarTodosOrcamentos([FromQuery] PagedRequest pagedRequest)
        {
            var orcamentos = await _orcamentoService.BuscarTodosOrcamentos(pagedRequest.Page, pagedRequest.PageSize);

            return Ok(orcamentos);
        }

        [HttpPost("{id:guid}/enviar-orcamento")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EnviarOrcamento(Guid id, [FromBody] EnviarOrcamentoRequest request)
        {
            string resultado = await _orcamentoService.EnviarOrcamento(id, request.EmailCliente);

            return Ok(resultado);
        }

        [HttpGet("{id:guid}/decisao")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> DecidirOrcamento(
            Guid id,
            [FromQuery] bool aprovado,
            [FromServices] IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var orcamento = await _orcamentoService.BuscarOrcamento(id);
            var osApiBaseUrl = (configuration["OrdemServico:BaseUrl"]
                ?? configuration["ORDEM_SERVICO_API_BASE_URL"]
                ?? "http://localhost:8081").TrimEnd('/');
            var targetUrl = $"{osApiBaseUrl}/api/v1/ordens-servico/{orcamento.IdOrdemServico}/aprovar-orcamento?aprovado={aprovado.ToString().ToLowerInvariant()}";

            using var request = new HttpRequestMessage(HttpMethod.Patch, targetUrl);
            var osApiKey = configuration["OrdemServico:ApiKey"]
                ?? configuration["ORDEM_SERVICO_API_KEY"];
            if (!string.IsNullOrWhiteSpace(osApiKey))
                request.Headers.TryAddWithoutValidation("X-API-Key", osApiKey);

            using var response = await httpClientFactory.CreateClient("ordem-servico").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status502BadGateway,
                    "Não foi possível registrar sua decisão. Tente novamente mais tarde.");

            var title = aprovado ? "Orçamento aprovado" : "Orçamento recusado";
            var message = aprovado
                ? "Obrigado por confirmar. Daremos continuidade ao atendimento."
                : "Sua recusa foi registrada. Se precisar de ajuda, entre em contato conosco.";
            var html = $"""
                <!DOCTYPE html>
                <html lang="pt-BR"><body style="font-family:Arial,sans-serif;text-align:center;padding:48px;color:#243447;">
                  <h1>{title}</h1><p>{message}</p>
                </body></html>
                """;
            return Content(html, "text/html", Encoding.UTF8);
        }
    }
}
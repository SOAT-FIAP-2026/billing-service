using System.ComponentModel;
using System.IO;
using System.Net;
using System.Globalization;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Exceptions;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.Domain.Interfaces.Observability;
using Billing.Service.Domain.ValueObjects;
using Fiap.TechChallenge.Contracts.BillingService;
using Fiap.TechChallenge.Contracts.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Billing.Service.Application.Services
{
    public class OrcamentoService : IOrcamentoService
    {
        private readonly IOrcamentoRepository _orcamentoRepository;
        private readonly IEmailService _emailService;
        private readonly IObservabilityMetrics? _observabilityMetrics;
        private readonly IBillingEventPublisher? _eventPublisher;
        private readonly ILogger<OrcamentoService>? _logger;
        private readonly IConfiguration? _configuration;

        public OrcamentoService(
            IOrcamentoRepository orcamentoRepository,
            IEmailService emailService,
            IObservabilityMetrics? observabilityMetrics = null,
            IBillingEventPublisher? eventPublisher = null,
            ILogger<OrcamentoService>? logger = null,
            IConfiguration? configuration = null)
        {
            _orcamentoRepository = orcamentoRepository;
            _emailService = emailService;
            _observabilityMetrics = observabilityMetrics;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<Orcamento> GerarOrcamento(
            Guid idOrdemServico,
            IReadOnlyCollection<ItemCalculoOrcamentoDto> servicos,
            IReadOnlyCollection<ItemCalculoOrcamentoDto> pecasInsumo,
            CancellationToken cancellationToken = default)
        {
            var orcamento = await _orcamentoRepository.ObterPorOrdemServicoId(idOrdemServico);
            if (orcamento == null)
            {
                orcamento = new Orcamento(idOrdemServico, 0);
                orcamento.CalcularOrcamento(servicos, pecasInsumo);
                await _orcamentoRepository.Adicionar(orcamento);
            }
            else
            {
                orcamento.CalcularOrcamento(servicos, pecasInsumo);
                await _orcamentoRepository.Atualizar(orcamento);
            }

            if (_eventPublisher != null)
            {
                var evento = new OrcamentoGeradoEvent
                {
                    OsId = orcamento.IdOrdemServico,
                    OrcamentoId = orcamento.Id,
                    ValorTotal = orcamento.ValorTotal.Valor,
                    LinkPagamento = $"http://localhost:8081/api/v1/pagamento/checkout/{orcamento.Id}",
                    QrCodePix = null
                };

                try
                {
                    await _eventPublisher.PublishAsync(evento, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Falha ao publicar OrcamentoGeradoEvent para OS {OsId}", orcamento.IdOrdemServico);
                }
            }

            return orcamento;
        }

        public async Task<Orcamento> GerarOrcamento(Guid idOrdemServico, IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo)
        {
            var orcamento = await _orcamentoRepository.ObterPorOrdemServicoId(idOrdemServico);
            if (orcamento == null)
            {
                orcamento = new Orcamento(idOrdemServico, 0);
                orcamento.CalcularOrcamento(servicos, pecasInsumo);
                await _orcamentoRepository.Adicionar(orcamento);
            }
            else
            {
                orcamento.CalcularOrcamento(servicos, pecasInsumo);
                await _orcamentoRepository.Atualizar(orcamento);
            }

            if (_eventPublisher != null)
            {
                var evento = new OrcamentoGeradoEvent
                {
                    OsId = orcamento.IdOrdemServico,
                    OrcamentoId = orcamento.Id,
                    ValorTotal = orcamento.ValorTotal.Valor,
                    LinkPagamento = $"http://localhost:8081/api/v1/pagamento/checkout/{orcamento.Id}",
                    QrCodePix = null
                };

                try
                {
                    await _eventPublisher.PublishAsync(evento);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Falha ao publicar OrcamentoGeradoEvent para OS {OsId}", orcamento.IdOrdemServico);
                }
            }

            return orcamento;
        }

        public async Task<Orcamento> BuscarOrcamento(Guid idOrcamento)
        {
            var orcamento = await _orcamentoRepository.ObterPorId(idOrcamento);

            if (orcamento == null)
                throw new OrcamentoNaoEncontradoException(idOrcamento);

            return orcamento;
        }

        public async Task<IReadOnlyCollection<Orcamento>> BuscarTodosOrcamentos(int page, int pageSize)
        {
            var orcamentos = await _orcamentoRepository.ObterTodos(page, pageSize);

            if (orcamentos == null || !orcamentos.Any())
                throw new OrcamentoNaoEncontradoException(Guid.Empty);

            return orcamentos;
        }

        public async Task<string> EnviarOrcamento(Guid idOrcamento, string emailCliente)
        {
            var orcamento = await _orcamentoRepository.ObterPorId(idOrcamento);

            if (orcamento == null)
                throw new OrcamentoNaoEncontradoException(idOrcamento);

            string assunto = "Seu orçamento está pronto para análise";
            string corpoHtml = CriarEmailOrcamento(orcamento);

            bool emailEnviado = await _emailService.EnviarEmailAsync(emailCliente, assunto, corpoHtml);

            if (!emailEnviado)
                throw new InvalidOperationException("Falha ao enviar o e-mail de notificação.");

            return "E-mail de orçamento enviado com sucesso.";
        }

        private string CriarEmailOrcamento(Orcamento orcamento)
        {
            var publicBaseUrl = (_configuration?["Billing:PublicBaseUrl"]
                ?? _configuration?["BILLING_PUBLIC_BASE_URL"]
                ?? "http://localhost:8080").TrimEnd('/');
            var approveUrl = $"{publicBaseUrl}/api/v1/orcamento/{orcamento.Id}/decisao?aprovado=true";
            var rejectUrl = $"{publicBaseUrl}/api/v1/orcamento/{orcamento.Id}/decisao?aprovado=false";
            var amount = orcamento.ValorTotal.Valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

            return $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <body style="margin:0;background:#f4f6f8;font-family:Arial,sans-serif;color:#243447;">
                  <div style="max-width:620px;margin:32px auto;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);">
                    <div style="background:#17324d;padding:28px 32px;color:#ffffff;">
                      <h1 style="margin:0;font-size:24px;font-weight:600;">Seu orçamento está pronto</h1>
                    </div>
                    <div style="padding:32px;">
                      <p style="font-size:16px;line-height:1.6;margin-top:0;">Olá!</p>
                      <p style="font-size:16px;line-height:1.6;">Preparamos o orçamento para o serviço solicitado. Confira o valor abaixo e escolha uma opção:</p>
                      <div style="margin:28px 0;padding:20px;background:#f4f6f8;border-radius:8px;text-align:center;">
                        <div style="font-size:13px;color:#607080;">Valor total</div>
                        <div style="margin-top:8px;font-size:28px;font-weight:700;color:#17324d;">{WebUtility.HtmlEncode(amount)}</div>
                      </div>
                      <div style="text-align:center;">
                        <a href="{WebUtility.HtmlEncode(approveUrl)}" style="display:inline-block;margin:4px;padding:13px 22px;background:#16835b;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;">Aprovar orçamento</a>
                        <a href="{WebUtility.HtmlEncode(rejectUrl)}" style="display:inline-block;margin:4px;padding:13px 22px;background:#ffffff;color:#b33a3a;text-decoration:none;border:1px solid #b33a3a;border-radius:6px;font-weight:600;">Recusar orçamento</a>
                      </div>
                      <p style="margin:28px 0 0;font-size:13px;line-height:1.5;color:#607080;">Se você não solicitou este orçamento, ignore esta mensagem.</p>
                    </div>
                  </div>
                </body>
                </html>
                """;
        }
    }
}

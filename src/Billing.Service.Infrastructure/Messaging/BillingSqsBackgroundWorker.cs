using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Infrastructure.Data;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Billing.Service.Infrastructure.Messaging
{
    public sealed class BillingSqsBackgroundWorker : BackgroundService
    {
        private readonly IAmazonSQS _sqs;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BillingSqsBackgroundWorker> _logger;
        private readonly string _queueUrl;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BillingSqsBackgroundWorker(
            IAmazonSQS sqs,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<BillingSqsBackgroundWorker> logger)
        {
            _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _queueUrl = _configuration["BILLING_QUEUE_URL"]
                ?? _configuration["Aws:BillingQueueUrl"]
                ?? "http://floci:4566/000000000000/techchallenge-billing-eventos-local";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando BillingSqsBackgroundWorker na fila {QueueUrl}...", _queueUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveRequest = new ReceiveMessageRequest
                    {
                        QueueUrl = _queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 3,
                        MessageAttributeNames = new List<string> { "All" }
                    };

                    var response = await _sqs.ReceiveMessageAsync(receiveRequest, stoppingToken);

                    if (response.Messages == null || response.Messages.Count == 0)
                    {
                        continue;
                    }

                    foreach (var message in response.Messages)
                    {
                        await ProcessarMensagemAsync(message, stoppingToken);
                        await _sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro no polling da fila SQS {QueueUrl}. Nova tentativa em 3 segundos...", _queueUrl);
                    await Task.Delay(3000, stoppingToken);
                }
            }

            _logger.LogInformation("BillingSqsBackgroundWorker finalizado.");
        }

        private async Task ProcessarMensagemAsync(Message message, CancellationToken stoppingToken)
        {
            var (conteudoJson, tipoEvento) = ExtrairConteudoETipoEvento(message);
            var sqsAttrs = message.MessageAttributes?.ToDictionary(k => k.Key, v => (string?)v.Value?.StringValue);
            var osId = SnsCorrelationHelper.ExtractOsId(message.Body, sqsAttrs);
            var correlationId = SnsCorrelationHelper.ExtractCorrelationId(message.Body, sqsAttrs, osId);

            using (CorrelationContext.Push(correlationId))
            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                [CorrelationContext.LogScopeKey] = correlationId,
                [CorrelationContext.OsIdScopeKey] = osId,
                ["event_type"] = tipoEvento
            }))
            {
                _logger.LogInformation(
                    "Mensagem SQS recebida no billing-service. Tipo: {TipoEvento}, CorrelationId: {CorrelationId}",
                    tipoEvento, correlationId);

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IBillingEventPublisher>();

                switch (tipoEvento)
                {
                    case nameof(DiagnosticoFinalizadoEvent):
                    case "DiagnosticoFinalizado":
                        await ProcessarDiagnosticoFinalizadoAsync(conteudoJson, dbContext, publisher, stoppingToken);
                        break;

                    case nameof(DiagnosticoTecnicoConcluidoEvent):
                    case "DiagnosticoTecnicoConcluido":
                        await ProcessarDiagnosticoTecnicoConcluidoAsync(conteudoJson, dbContext, publisher, stoppingToken);
                        break;

                    case nameof(ProducaoCanceladaEvent):
                    case "ProducaoCancelada":
                        await ProcessarProducaoCanceladaAsync(conteudoJson, dbContext, publisher, stoppingToken);
                        break;

                    case nameof(OrdemServicoCanceladaEvent):
                    case "OrdemServicoCancelada":
                        await ProcessarOrdemServicoCanceladaAsync(conteudoJson, dbContext, stoppingToken);
                        break;

                    default:
                        _logger.LogDebug("Evento {TipoEvento} ignorado pelo billing-service.", tipoEvento);
                        break;
                }
            }
        }

        private (string ConteudoJson, string TipoEvento) ExtrairConteudoETipoEvento(Message message)
        {
            string payload = message.Body;
            string tipoEvento = string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(message.Body);
                var root = doc.RootElement;

                // Envelope SNS Notification
                if (root.TryGetProperty("Type", out var typeProp) && typeProp.GetString() == "Notification")
                {
                    if (root.TryGetProperty("Message", out var msgProp))
                        payload = msgProp.GetString() ?? message.Body;

                    if (root.TryGetProperty("MessageAttributes", out var msgAttrs))
                    {
                        if (msgAttrs.TryGetProperty(SnsMessageAttributes.TipoEvento, out var teProp) &&
                            teProp.TryGetProperty("Value", out var valProp))
                        {
                            tipoEvento = valProp.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
                // Fallback para raw payload em caso de erro no parse do envelope
            }

            if (string.IsNullOrEmpty(tipoEvento) && message.MessageAttributes.TryGetValue(SnsMessageAttributes.TipoEvento, out var attrVal))
            {
                tipoEvento = attrVal.StringValue;
            }

            if (string.IsNullOrEmpty(tipoEvento))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    if (doc.RootElement.TryGetProperty("tipoEvento", out var tProp) ||
                        doc.RootElement.TryGetProperty("TipoEvento", out tProp))
                    {
                        tipoEvento = tProp.GetString() ?? string.Empty;
                    }
                }
                catch
                {
                }
            }

            return (payload, tipoEvento);
        }

        private async Task ProcessarDiagnosticoFinalizadoAsync(
            string json,
            ApplicationDbContext dbContext,
            IBillingEventPublisher publisher,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<DiagnosticoFinalizadoEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty)
            {
                _logger.LogWarning("Falha ao desserializar DiagnosticoFinalizadoEvent: {Json}", json);
                return;
            }

            var existe = await dbContext.Orcamentos.AnyAsync(o => o.IdOrdemServico == evento.OsId, cancellationToken);
            if (existe)
            {
                _logger.LogWarning("Orçamento para OS {OsId} já existe. Ignorando evento duplicado.", evento.OsId);
                return;
            }

            decimal total = evento.Total > 0
                ? evento.Total
                : (evento.Itens ?? []).Sum(i => i.ValorTotal);

            var orcamento = new Orcamento(evento.OsId, Guid.NewGuid(), total);
            await dbContext.Orcamentos.AddAsync(orcamento, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Orçamento {OrcamentoId} criado com sucesso para OS {OsId}. Valor total: {Total}",
                orcamento.Id, evento.OsId, total);

            var orcamentoGerado = new OrcamentoGeradoEvent
            {
                OsId = evento.OsId,
                OrcamentoId = orcamento.Id,
                ValorTotal = total,
                LinkPagamento = $"http://localhost:8081/api/v1/pagamento/checkout/{orcamento.Id}",
                QrCodePix = null
            };

            await publisher.PublishAsync(orcamentoGerado, cancellationToken);
        }

        private async Task ProcessarDiagnosticoTecnicoConcluidoAsync(
            string json,
            ApplicationDbContext dbContext,
            IBillingEventPublisher publisher,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<DiagnosticoTecnicoConcluidoEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty)
            {
                _logger.LogWarning("Falha ao desserializar DiagnosticoTecnicoConcluidoEvent: {Json}", json);
                return;
            }

            var existe = await dbContext.Orcamentos.AnyAsync(o => o.IdOrdemServico == evento.OsId, cancellationToken);
            if (existe)
            {
                _logger.LogWarning("Orçamento para OS {OsId} já existe. Ignorando evento duplicado.", evento.OsId);
                return;
            }

            decimal totalServicos = (evento.Servicos?.Count ?? 0) * 150m;
            decimal totalPecas = (evento.Pecas ?? []).Sum(p => 50m * (p.Quantidade > 0 ? p.Quantidade : 1));
            decimal total = totalServicos + totalPecas;

            var orcamento = new Orcamento(evento.OsId, Guid.NewGuid(), total);
            await dbContext.Orcamentos.AddAsync(orcamento, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Orçamento {OrcamentoId} gerado via DiagnosticoTecnicoConcluido para OS {OsId}. Valor: {Total}",
                orcamento.Id, evento.OsId, total);

            var orcamentoGerado = new OrcamentoGeradoEvent
            {
                OsId = evento.OsId,
                OrcamentoId = orcamento.Id,
                ValorTotal = total,
                LinkPagamento = $"http://localhost:8081/api/v1/pagamento/checkout/{orcamento.Id}",
                QrCodePix = null
            };

            await publisher.PublishAsync(orcamentoGerado, cancellationToken);
        }

        private async Task ProcessarProducaoCanceladaAsync(
            string json,
            ApplicationDbContext dbContext,
            IBillingEventPublisher publisher,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<ProducaoCanceladaEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty)
            {
                _logger.LogWarning("Falha ao desserializar ProducaoCanceladaEvent: {Json}", json);
                return;
            }

            var orcamento = await dbContext.Orcamentos
                .FirstOrDefaultAsync(o => o.IdOrdemServico == evento.OsId, cancellationToken);

            Pagamento? pagamento = null;
            if (orcamento != null)
            {
                pagamento = await dbContext.Pagamentos
                    .FirstOrDefaultAsync(p => p.IdOrcamento == orcamento.Id, cancellationToken);
            }

            if (pagamento != null)
            {
                pagamento.AtualizarStatus(PagamentoStatus.Cancelado, $"Estorno por cancelamento de produção: {evento.Motivo}");
                await dbContext.SaveChangesAsync(cancellationToken);

                var estornoEvent = new PagamentoEstornadoEvent
                {
                    OsId = evento.OsId,
                    PagamentoId = pagamento.Id,
                    ValorEstornado = pagamento.Valor.Valor
                };

                await publisher.PublishAsync(estornoEvent, cancellationToken);
                _logger.LogInformation("Pagamento {PagamentoId} estornado para OS {OsId}. Evento PagamentoEstornadoEvent publicado.",
                    pagamento.Id, evento.OsId);
            }
            else
            {
                _logger.LogWarning("Nenhum pagamento encontrado para OS {OsId} ao estornar produção.", evento.OsId);
                var estornoEvent = new PagamentoEstornadoEvent
                {
                    OsId = evento.OsId,
                    PagamentoId = Guid.Empty,
                    ValorEstornado = orcamento?.ValorTotal.Valor ?? 0m
                };
                await publisher.PublishAsync(estornoEvent, cancellationToken);
            }
        }

        private async Task ProcessarOrdemServicoCanceladaAsync(
            string json,
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<OrdemServicoCanceladaEvent>(json, JsonOptions);
            if (evento == null || evento.OsId == Guid.Empty) return;

            var orcamento = await dbContext.Orcamentos
                .FirstOrDefaultAsync(o => o.IdOrdemServico == evento.OsId, cancellationToken);

            if (orcamento != null)
            {
                var pagamento = await dbContext.Pagamentos
                    .FirstOrDefaultAsync(p => p.IdOrcamento == orcamento.Id, cancellationToken);

                if (pagamento != null && pagamento.Status == PagamentoStatus.Pendente)
                {
                    pagamento.AtualizarStatus(PagamentoStatus.Cancelado, $"Cancelado por cancelamento da OS: {evento.Motivo}");
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Pagamento pendente {PagamentoId} cancelado por cancelamento da OS {OsId}.",
                        pagamento.Id, evento.OsId);
                }
            }
        }
    }
}

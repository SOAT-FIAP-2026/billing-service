using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Billing.Service.Application.Interfaces;
using Fiap.TechChallenge.Contracts;
using Fiap.TechChallenge.Contracts.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Billing.Service.Infrastructure.Messaging
{
    public sealed class BillingSnsEventPublisher : IBillingEventPublisher
    {
        private readonly IAmazonSimpleNotificationService _sns;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BillingSnsEventPublisher> _logger;
        private readonly string _topicArn;

        public BillingSnsEventPublisher(
            IAmazonSimpleNotificationService sns,
            IConfiguration configuration,
            ILogger<BillingSnsEventPublisher> logger)
        {
            _sns = sns ?? throw new ArgumentNullException(nameof(sns));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _topicArn = _configuration["EVENTOS_TOPIC_ARN"]
                ?? _configuration["Aws:EventosTopicArn"]
                ?? "arn:aws:sns:sa-east-1:000000000000:techchallenge-eventos-local";
        }

        public async Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(evento);

            var tipoEvento = (evento as IntegrationEvent)?.TipoEvento ?? typeof(TEvent).Name;
            var jsonPayload = JsonSerializer.Serialize(evento);

            string osIdStr = string.Empty;
            string correlationIdStr = string.Empty;

            if (evento is IntegrationEvent integrationEvent)
            {
                osIdStr = integrationEvent.OsId.ToString();
                correlationIdStr = integrationEvent.CorrelationId.ToString();
            }
            else
            {
                var propOsId = typeof(TEvent).GetProperty("OsId") ?? typeof(TEvent).GetProperty("IdOrdemServico");
                if (propOsId != null)
                {
                    var val = propOsId.GetValue(evento);
                    if (val != null)
                        osIdStr = val.ToString() ?? string.Empty;
                }

                var propCorrelationId = typeof(TEvent).GetProperty("CorrelationId") ?? typeof(TEvent).GetProperty("IdCorrelacao");
                if (propCorrelationId != null)
                {
                    var val = propCorrelationId.GetValue(evento);
                    if (val != null)
                        correlationIdStr = val.ToString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(correlationIdStr))
                correlationIdStr = osIdStr;

            var request = new PublishRequest
            {
                TopicArn = _topicArn,
                Message = jsonPayload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { DataType = "String", StringValue = tipoEvento },
                    [SnsMessageAttributes.Origem] = new() { DataType = "String", StringValue = SnsMessageAttributes.Origens.BillingService }
                }
            };

            if (!string.IsNullOrWhiteSpace(osIdStr))
            {
                request.MessageAttributes[SnsMessageAttributes.IdOrdemServico] = new() { DataType = "String", StringValue = osIdStr };
            }

            if (!string.IsNullOrWhiteSpace(correlationIdStr))
            {
                request.MessageAttributes[SnsMessageAttributes.CorrelationId] = new() { DataType = "String", StringValue = correlationIdStr };
            }

            _logger.LogInformation(
                "Publicando evento {TipoEvento} no SNS ({TopicArn}) para OS {OsId}",
                tipoEvento, _topicArn, osIdStr);

            await _sns.PublishAsync(request, cancellationToken);
        }
    }
}

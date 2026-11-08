using System.Net.Http.Headers;
using System.Net.Http.Json;
using Billing.Service.Domain.Interfaces.Observability;
using Billing.Service.Domain.Interfaces.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Billing.Service.External.Services
{
    public class ResendEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IObservabilityMetrics? _observabilityMetrics;
        private readonly ILogger<ResendEmailService>? _logger;
        private readonly string _from;
        private readonly bool _apiKeyConfigured;

        public ResendEmailService(
            HttpClient httpClient,
            IConfiguration configuration,
            IObservabilityMetrics? observabilityMetrics = null,
            ILogger<ResendEmailService>? logger = null)
        {
            _httpClient = httpClient;
            _observabilityMetrics = observabilityMetrics;
            _logger = logger;
            var apiKey = configuration["RESEND_API_KEY"];
            _from = configuration["RESEND_FROM_EMAIL"] ?? "onboarding@resend.dev";
            _apiKeyConfigured = !string.IsNullOrWhiteSpace(apiKey);
            if (_apiKeyConfigured)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<bool> EnviarEmailAsync(string para, string assunto, string corpoHtml)
        {
            if (!_apiKeyConfigured)
            {
                RecordFailure(new InvalidOperationException("RESEND_API_KEY não configurada."));
                return false;
            }

            var payload = new
            {
                from = _from,
                to = para,
                subject = assunto,
                html = corpoHtml
            };

            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync("https://api.resend.com/emails", payload);
                    response.EnsureSuccessStatusCode();
                    _observabilityMetrics?.RecordIntegrationRequest("resend", succeeded: true);
                    return true;
                }
                catch (HttpRequestException exception)
                {
                    if (i == maxRetries - 1)
                    {
                        RecordFailure(exception);
                        return false;
                    }

                    await Task.Delay(1000);
                }
                catch (TaskCanceledException exception)
                {
                    if (i == maxRetries - 1)
                    {
                        RecordFailure(exception);
                        return false;
                    }

                    await Task.Delay(1000);
                }
            }

            return false;
        }

        private void RecordFailure(Exception exception)
        {
            _observabilityMetrics?.RecordIntegrationRequest("resend", succeeded: false);
            _observabilityMetrics?.RecordIntegrationError("resend", "send_email");
            _logger?.LogError(exception, "Falha na integração com o Resend ao enviar e-mail");
        }
    }
}

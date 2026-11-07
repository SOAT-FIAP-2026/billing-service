using System.Diagnostics.Metrics;
using Billing.Service.Api.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Billing.Service.Tests.Api.Observability;

public class ObservabilityMetricsTests
{
    [Fact]
    public void Metricas_DeveEmitirTodosOsInstrumentosDeObservabilidade()
    {
        var instrumentNames = new HashSet<string>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ObservabilityMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => instrumentNames.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => instrumentNames.Add(instrument.Name));
        listener.Start();

        var metrics = new ObservabilityMetrics();
        metrics.RecordOrderCreated("recebida");
        metrics.RecordOrderStatusDuration("diagnostico", TimeSpan.FromMilliseconds(-1));
        metrics.RecordOrderProcessingFailure("http_request");
        metrics.RecordIntegrationRequest("resend", succeeded: true);
        metrics.RecordIntegrationRequest("resend", succeeded: false);
        metrics.RecordIntegrationError("resend", "send_email");
        metrics.RecordHttpRequest(TimeSpan.FromMilliseconds(-1), "GET", StatusCodes.Status200OK);

        instrumentNames.Should().BeEquivalentTo(
            "billing.budgets.created",
            "billing.budgets.status.duration",
            "billing.processing.failures",
            "billing.integrations.requests",
            "billing.integrations.errors",
            "billing.http.request.duration");
    }
}

# ADR-003: Stack de observabilidade

- **Status:** Aceito
- **Data:** 2026-11-07

## Decisão

Adotar OpenTelemetry, Prometheus, Grafana, Loki, Tempo, Alertmanager e Blackbox
Exporter. A stack pode executar localmente via Docker Compose e no Kubernetes via
ServiceMonitor, Probe e PrometheusRule.

## Justificativa

Essa combinação atende métricas HTTP e de negócio do Billing, logs JSON com
correlação e traces distribuídos sem exigir uma conta SaaS. OTLP mantém a
possibilidade de exportar para outro backend no futuro.

## Privacidade

Tokens, segredos, CPF e dados completos de pagamento não devem aparecer em logs ou
labels de métricas.

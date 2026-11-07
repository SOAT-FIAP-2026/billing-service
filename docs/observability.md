# Observabilidade do Billing Service

## Endpoints

| Endpoint | Uso |
|---|---|
| `/health/live` | Liveness sem dependências externas |
| `/health/ready` | Readiness com verificação do PostgreSQL |
| `/health` | Diagnóstico completo |
| `/metrics` | Scraping Prometheus |
| `/swagger` | Contratos HTTP |

## Stack

- OpenTelemetry para traces, métricas e exportação opcional por OTLP.
- Prometheus para métricas e alertas.
- Grafana para dashboards.
- Loki para logs estruturados.
- Tempo para traces.
- Alertmanager e Blackbox Exporter para alertas e uptime.

No Compose, a stack é definida em `docker-compose.yml` e `observability/`. No
Kubernetes, os recursos da aplicação ficam em `k8s/observability/`.

## Métricas

| Métrica | Tags | Significado |
|---|---|---|
| `billing_budgets_created_total` | `status` | Orçamentos gerados |
| `billing_budgets_status_duration_milliseconds` | `status` | Duração do processamento de orçamento |
| `billing_processing_failures_total` | `operation` | Falhas HTTP/processamento |
| `billing_integrations_requests_total` | `integration`, `result` | Chamadas a Mercado Pago, Resend e AWS |
| `billing_integrations_errors_total` | `integration`, `operation` | Erros de integração |
| `billing_http_request_duration_milliseconds` | `method`, `status_code` | Latência HTTP |

## Logs e correlação

Os logs são JSON no stdout. O middleware propaga ou cria `X-Correlation-ID`, devolve
o valor na resposta e inclui a correlação nos logs. Não registre tokens, segredos,
CPF ou payloads sensíveis.

## Alertas

As regras em `observability/prometheus/alerts.yml` e
`k8s/observability/prometheusrule.yaml` cobrem:

- falhas de processamento;
- erros de integração;
- latência p95 e taxa de erro 5xx;
- indisponibilidade do serviço;
- readiness e uptime;
- CPU, memória, reinícios e HPA no Kubernetes.

## Execução local

```bash
docker compose up -d
```

| Serviço | URL |
|---|---|
| API | http://localhost:8080 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Alertmanager | http://localhost:9093 |
| Loki | http://localhost:3100 |
| Tempo | http://localhost:3200 |

O Blackbox Exporter é publicado em `http://localhost:9116` por padrão. Para usar
outra porta no host, defina `BLACKBOX_EXPORTER_PORT`; a porta interna do serviço
continua sendo `9115`.

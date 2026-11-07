# Diagrama de componentes — Billing Service

```mermaid
graph TB
    CLIENT["Cliente / Serviço consumidor"]
    API["Billing Service API<br/>ASP.NET Core .NET 8"]
    ORC["OrcamentoService"]
    PAG["PagamentoService"]
    DB[("PostgreSQL<br/>orcamentos / pagamentos")]
    MP["Mercado Pago<br/>Payments API + Webhook"]
    EMAIL["Resend<br/>E-mail de orçamento"]
    SNS["AWS SNS<br/>Eventos de Billing"]
    SQS["AWS SQS<br/>Eventos consumidos"]
    OBS["Prometheus / Grafana<br/>Loki / Tempo / OTEL"]

    CLIENT -->|HTTP JSON| API
    API --> ORC
    API --> PAG
    ORC --> DB
    PAG --> DB
    PAG -->|Criar boleto / consultar status| MP
    MP -->|Webhook assinado| API
    ORC -->|Enviar orçamento| EMAIL
    ORC --> SNS
    PAG --> SNS
    SQS --> API
    API -.-> OBS
```

## Componentes implantáveis

| Componente | Responsabilidade |
|---|---|
| `Billing.Service.Api` | Controllers, middleware, Swagger, health checks e métricas |
| `Billing.Service.Application` | Casos de uso de orçamento e pagamento |
| `Billing.Service.Domain` | Entidades, value objects e regras de negócio |
| `Billing.Service.Infrastructure` | EF Core, PostgreSQL, SNS e SQS |
| `Billing.Service.External` | Mercado Pago e Resend |
| `Fiap.TechChallenge.Contracts` | DTOs e eventos compartilhados |

A aplicação pode executar em container Kubernetes ou como pacote Lambda conforme o
pipeline utilizado. O banco permanece externo à aplicação e as credenciais são
injetadas por ambiente.

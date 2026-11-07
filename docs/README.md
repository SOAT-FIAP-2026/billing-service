# Documentação do Billing Service

Documentação arquitetural e operacional do microsserviço responsável por
orçamentos, pagamentos e integração com o Mercado Pago.

## Escopo

- Gerar, consultar e enviar orçamentos.
- Criar boletos e consultar pagamentos.
- Receber e validar webhooks do Mercado Pago.
- Publicar eventos de orçamento e pagamento para os demais microsserviços.
- Persistir dados em PostgreSQL.
- Expor métricas, logs estruturados, traces e health checks.

O Billing Service **não** é responsável por clientes, veículos, estoque,
ordens de serviço, autenticação por CPF ou pelo API Gateway de autenticação.
Esses recursos pertencem a outros serviços.

## Índice

- [Diagrama de componentes](./component-diagram.md)
- [Diagrama de sequência](./sequence-diagram.md)
- [Modelo de dados](./database-model.md)
- [Observabilidade](./observability.md)
- [Relatório de validação](./validation-report.md)
- [ADR-001 — Comunicação](./adrs/ADR-001-communication-pattern.md)
- [ADR-002 — HPA](./adrs/ADR-002-kubernetes-hpa-autoscaling.md)
- [ADR-003 — Observabilidade](./adrs/ADR-003-observability-stack.md)
- [RFC-001 — AWS](./rfcs/RFC-001-cloud-provider-choice.md)
- [RFC-002 — PostgreSQL](./rfcs/RFC-002-managed-database-choice.md)
- [Coleção Postman](./postman/billing-service.postman_collection.json)

## Endpoints principais

| Método | Endpoint | Finalidade |
|---|---|---|
| POST | `/api/v1/orcamento` | Gera ou recalcula orçamento |
| GET | `/api/v1/orcamento/{id}` | Consulta orçamento |
| GET | `/api/v1/orcamento/listar` | Lista orçamentos paginados |
| POST | `/api/v1/orcamento/{id}/enviar-orcamento` | Envia orçamento por e-mail |
| POST | `/api/v1/pagamento/boleto` | Cria boleto no Mercado Pago |
| GET | `/api/v1/pagamento/{id}` | Consulta pagamento |
| POST | `/api/v1/pagamento/webhook` | Recebe atualização do Mercado Pago |
| GET | `/health/live` | Verifica processo |
| GET | `/health/ready` | Verifica PostgreSQL |
| GET | `/metrics` | Métricas Prometheus |
| GET | `/swagger` | Documentação HTTP |

## Execução local

```bash
docker compose up -d
```

O Compose usa PostgreSQL no banco `billingdb`, sobe a API em `http://localhost:8080`
e disponibiliza a stack local de Prometheus, Grafana, Loki, Tempo e Alertmanager.

Nunca versione `MercadoPago__AccessToken`, `MercadoPago__WebhookSecret` ou
`ConnectionStrings__DefaultConnection`.

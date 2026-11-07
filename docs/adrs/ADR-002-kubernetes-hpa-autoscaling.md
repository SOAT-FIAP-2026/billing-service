# ADR-002: HPA do Billing Service

- **Status:** Aceito
- **Data:** 2026-11-07

## Decisão

Usar Kubernetes HPA v2 para escalar o Deployment `billing-service` de 1 a 5
réplicas, usando CPU e memória do Metrics Server. Os limites atuais estão
versionados em `k8s/base/hpa.yaml`.

O Deployment define requests/limits e probes separadas:

- `/health/ready` para readiness, incluindo PostgreSQL;
- `/health/live` para liveness e startup.

## Consequências

O serviço absorve picos de consultas e webhooks sem alterar o contrato HTTP. O
PostgreSQL e as integrações externas continuam sendo recursos compartilhados e
podem exigir limites próprios de conexão e rate limiting.

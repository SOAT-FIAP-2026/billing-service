# ADR-001: Comunicação síncrona e assíncrona do Billing Service

- **Status:** Aceito
- **Data:** 2026-11-07

## Contexto

O Billing Service precisa responder imediatamente a chamadas HTTP de orçamento e
pagamento, mas deve comunicar mudanças de pagamento e orçamento aos demais
microsserviços sem acoplamento temporal.

## Decisão

- REST/HTTP com JSON para os endpoints públicos.
- AWS SNS/SQS para eventos de integração.
- `Fiap.TechChallenge.Contracts` como fonte única de DTOs e eventos.
- `X-Correlation-ID` e o envelope `IntegrationEvent` para rastreabilidade.

Eventos relevantes incluem `OrcamentoGeradoEvent`, `PagamentoConfirmadoEvent` e
`PagamentoCanceladoCompensacaoEvent`.

## Consequências

O consumidor recebe confirmação imediata das operações síncronas e os serviços
mantêm eventual consistency nos fluxos de pagamento. Mensagens exigem idempotência,
correlação e tratamento de falhas/dead letter queue.

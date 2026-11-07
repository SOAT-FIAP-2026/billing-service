# Diagramas de sequência — Billing Service

## Geração de orçamento

```mermaid
sequenceDiagram
    participant OS as OS Service
    participant API as Billing API
    participant SVC as OrcamentoService
    participant DB as PostgreSQL
    participant SNS as AWS SNS

    OS->>API: POST /api/v1/orcamento + itens
    API->>SVC: GerarOrcamento(osId, serviços, peças)
    SVC->>DB: Busca orçamento por OS
    alt orçamento inexistente
        SVC->>DB: INSERT orçamento
    else orçamento existente
        SVC->>DB: UPDATE valor total
    end
    SVC->>SNS: OrcamentoGeradoEvent
    API-->>OS: 200 OK
```

## Criação e confirmação de pagamento

```mermaid
sequenceDiagram
    participant CLIENT as Cliente
    participant API as Billing API
    participant SVC as PagamentoService
    participant DB as PostgreSQL
    participant MP as Mercado Pago
    participant SNS as AWS SNS

    CLIENT->>API: POST /api/v1/pagamento/boleto
    API->>SVC: CriarBoletoAsync
    SVC->>DB: Consulta orçamento
    SVC->>MP: Cria boleto
    MP-->>SVC: ID, status, URL e código de barras
    SVC->>DB: Persiste pagamento
    API-->>CLIENT: 201 Created

    MP->>API: POST /api/v1/pagamento/webhook
    API->>API: Valida x-signature e x-request-id
    API->>SVC: AtualizarPorNotificacaoAsync
    SVC->>MP: Consulta pagamento
    SVC->>DB: Atualiza status
    SVC->>SNS: PagamentoConfirmadoEvent ou compensação
    API-->>MP: 200 OK
```

O webhook aceita o identificador em `data.id` no corpo ou na query string. Com
`MercadoPago__WebhookSecret` configurado, assinatura ausente ou inválida retorna
`401 Unauthorized`.

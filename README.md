# billing-service

## Pagamento via boleto

O backend integra o Mercado Pago pela Payments API (`payment_method_id=bolbradesco`).
Configure estas variáveis antes de iniciar a API:

```text
MercadoPago:AccessToken=<Access Token de teste ou produção>
MercadoPago:NotificationUrl=https://seu-dominio/api/v1/pagamento/webhook
MercadoPago:WebhookSecret=<segredo da assinatura do webhook>
ConnectionStrings__DefaultConnection=<connection string do PostgreSQL>
```

Endpoints:

- `POST /api/v1/pagamento/boleto`: cria o boleto a partir de um orçamento.
- `GET /api/v1/pagamento/{pagamentoId}`: consulta o pagamento persistido.
- `POST /api/v1/pagamento/webhook`: recebe atualizações e consulta o pagamento no Mercado Pago.

Exemplo de criação:

```json
{
  "idOrcamento": "00000000-0000-0000-0000-000000000000",
  "email": "cliente@example.com",
  "nome": "João",
  "sobrenome": "Silva",
  "cpf": "12345678909"
}
```

Em desenvolvimento, use as credenciais de teste do Mercado Pago. A migração
`AddPagamentos` cria a tabela necessária; ela é aplicada automaticamente pela API
na inicialização.
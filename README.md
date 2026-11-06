# billing-service

## Pagamento via boleto

O backend integra o Mercado Pago pela Payments API (`payment_method_id=bolbradesco`).
Configure estas variáveis antes de iniciar a API:

```text
MercadoPago__AccessToken=<Access Token de teste ou produção>
MercadoPago__NotificationUrl=https://seu-dominio/api/v1/pagamento/webhook
MercadoPago__WebhookSecret=<segredo da assinatura do webhook>
ConnectionStrings__DefaultConnection=<connection string do PostgreSQL>
```

No GitHub Actions, cadastre `MERCADO_PAGO_ACCESS_TOKEN` e
`MERCADO_PAGO_WEBHOOK_SECRET` como **Repository secrets**. O workflow de
deploy configura esses valores na Lambda e preserva as demais variáveis já
existentes. A URL de teste atualmente configurada é:

```text
https://figg7rtf4l7sqb2t2fsodkq45m0vmtkt.lambda-url.us-east-2.on.aws/api/v1/pagamento/webhook
```

As chaves `MercadoPago:*` também podem ser definidas no `appsettings.json` ou
em um provedor de configuração .NET. A `NotificationUrl` deve ser HTTPS,
publicamente acessível e apontar para o endpoint de webhook. No painel do
Mercado Pago, configure essa URL para os ambientes de teste e produção,
habilite o evento **Pagamentos** e copie a chave secreta gerada para
`MercadoPago__WebhookSecret`. Nunca versione o access token ou a chave secreta.

Endpoints:

- `POST /api/v1/pagamento/boleto`: cria o boleto a partir de um orçamento.
- `GET /api/v1/pagamento/{pagamentoId}`: consulta o pagamento persistido.
- `POST /api/v1/pagamento/webhook`: recebe atualizações e consulta o pagamento no Mercado Pago.

O webhook valida `x-signature` e `x-request-id` antes de consultar o pagamento.
O identificador pode ser recebido no corpo (`data.id`) ou na query string
(`data.id`), conforme o formato da notificação enviada pelo Mercado Pago.

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
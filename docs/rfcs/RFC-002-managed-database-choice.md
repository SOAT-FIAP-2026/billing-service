# RFC-002: PostgreSQL gerenciado para o Billing Service

- **Status:** Aprovado
- **Data:** 2026-11-07

## Contexto

Orçamentos e pagamentos exigem transações, precisão decimal, índices e relação
referencial entre cobrança e orçamento. O serviço não deve duplicar o cadastro da
ordem de serviço; recebe apenas `IdOrdemServico`.

## Decisão

Usar PostgreSQL 16, localmente em container e em produção como RDS. O acesso é
feito por EF Core/Npgsql e as alterações de schema são controladas por migrations.

## Consequências

O modelo mantém consistência ACID e consultas previsíveis. Backups, failover,
pool de conexões, credenciais e limites de conexão devem ser configurados no
ambiente gerenciado.

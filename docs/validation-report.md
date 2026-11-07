# Relatório de validação — Billing Service

## Escopo validado

- Build dos projetos .NET 8.
- Testes unitários, de contrato e de integração.
- Cobertura via `coverlet.collector`.
- Configuração Docker Compose.
- Dockerfile com contexto dos contratos compartilhados.
- Scripts Bash de deploy e observabilidade.
- Referências Kubernetes para deployment, service, probes, HPA e secrets.

## Resultado

Última validação local:

```text
Billing.Service.Tests: 202 aprovados, 0 falhas
Docker Compose: configuração válida
Dockerfile: buildx --check sem warnings
Scripts Bash: sintaxe válida
```

## Limitações

- A validação não substitui um ambiente real com PostgreSQL, AWS SNS/SQS, Mercado Pago
  e Resend.
- O webhook exige segredo e headers reais para validar a assinatura.
- A validação de Kubernetes depende de um cluster com Metrics Server e os CRDs do
  Prometheus Operator.

## Comandos

```bash
dotnet test tests/Billing.Service.Tests/Billing.Service.Tests.csproj \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage"

docker compose config --quiet
docker buildx build --check --build-context shared=../shared -f Dockerfile .
```

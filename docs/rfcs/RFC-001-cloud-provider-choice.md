# RFC-001: AWS como plataforma de execução

- **Status:** Aprovado
- **Data:** 2026-11-07

## Contexto

O Billing Service precisa executar containers .NET 8, utilizar PostgreSQL gerenciado,
integrar-se ao SNS/SQS e receber webhooks públicos do Mercado Pago.

## Decisão

AWS é a plataforma de referência por oferecer EKS, RDS PostgreSQL, SNS, SQS,
CloudWatch/OTLP e integração com os pipelines existentes. A aplicação permanece
portável por usar container, PostgreSQL e contratos HTTP/eventos padronizados.

## Operação

- Local: Docker Compose ou Kubernetes local.
- Produção: EKS para o container e RDS para PostgreSQL.
- Secrets: GitHub Actions, Kubernetes Secret ou AWS Secrets Manager; nunca código.

## Riscos

O uso de AWS gera dependência operacional de EKS/RDS/SNS/SQS. A separação por
interfaces e a biblioteca de contratos reduzem o acoplamento de aplicação.

#!/usr/bin/env bash
# ==============================================================================
# deploy-local.sh — Deploy no ambiente local (Minikube ou Kind)
# ==============================================================================
# Uso:
#   chmod +x k8s/overlays/local/deploy.sh
#   ./k8s/overlays/local/deploy.sh
#
# Pré-requisitos:
#   - minikube start   OU   kind create cluster
#   - metrics-server instalado (ver k8s/metrics-server/install.sh)
# ==============================================================================
set -euo pipefail

CLUSTER_TYPE="${1:-minikube}"   # minikube | kind
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASE_DIR="$SCRIPT_DIR/../../base"
OVERLAY_DIR="$SCRIPT_DIR"

echo "🚀 Deploy LOCAL — cluster: $CLUSTER_TYPE"

# --- 1. Carrega imagem local no cluster (evita pull do Docker Hub) --------
if [ "$CLUSTER_TYPE" = "minikube" ]; then
  echo "📦 Carregando imagem no Minikube..."
  minikube image load gabrielnetto94/billing-service:latest 2>/dev/null || \
    echo "⚠️  Imagem não encontrada localmente — será feito pull do Docker Hub"
elif [ "$CLUSTER_TYPE" = "kind" ]; then
  echo "📦 Carregando imagem no Kind..."
  kind load docker-image gabrielnetto94/billing-service:latest 2>/dev/null || \
    echo "⚠️  Imagem não encontrada localmente — será feito pull do Docker Hub"
fi

# --- 2. Namespace ----------------------------------------------------------
echo "📁 Aplicando namespace..."
kubectl apply -f "$BASE_DIR/namespace.yaml"

# --- 3. ConfigMap e Secrets -----------------------------------------------
# O secrets.yaml nao e versionado: ele e gerado a partir do template. Os valores
# padrao servem apenas para o cluster local descartavel.
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD must be configured before deployment}"
export MERCADO_PAGO_ACCESS_TOKEN="${MERCADO_PAGO_ACCESS_TOKEN:-}"
export MERCADO_PAGO_WEBHOOK_SECRET="${MERCADO_PAGO_WEBHOOK_SECRET:-}"
export MERCADO_PAGO_NOTIFICATION_URL="${MERCADO_PAGO_NOTIFICATION_URL:-}"
export RESEND_API_KEY="${RESEND_API_KEY:-}"
export RESEND_FROM_EMAIL="${RESEND_FROM_EMAIL:-onboarding@resend.dev}"

echo "🔐 Gerando Secret local a partir do template..."
if command -v envsubst >/dev/null 2>&1; then
  envsubst < "$OVERLAY_DIR/secrets.yaml.template" > "$OVERLAY_DIR/secrets.yaml"
else
  sed -e "s|\${POSTGRES_PASSWORD}|$POSTGRES_PASSWORD|g" \
      -e "s|\${MERCADO_PAGO_ACCESS_TOKEN}|$MERCADO_PAGO_ACCESS_TOKEN|g" \
      -e "s|\${MERCADO_PAGO_WEBHOOK_SECRET}|$MERCADO_PAGO_WEBHOOK_SECRET|g" \
      -e "s|\${MERCADO_PAGO_NOTIFICATION_URL}|$MERCADO_PAGO_NOTIFICATION_URL|g" \
      -e "s|\${RESEND_API_KEY}|$RESEND_API_KEY|g" \
      -e "s|\${RESEND_FROM_EMAIL}|$RESEND_FROM_EMAIL|g" \
      "$OVERLAY_DIR/secrets.yaml.template" > "$OVERLAY_DIR/secrets.yaml"
fi

echo "🔧 Aplicando ConfigMap e Secrets..."
kubectl apply -f "$OVERLAY_DIR/configmap.yaml"
kubectl apply -f "$OVERLAY_DIR/secrets.yaml"

# --- 4. PostgreSQL local ---------------------------------------------------
echo "🐘 Aplicando PostgreSQL local..."
kubectl apply -f "$OVERLAY_DIR/postgres.yaml"

echo "⏳ Aguardando PostgreSQL ficar pronto..."
kubectl rollout status deployment/postgres -n techchallenge --timeout=120s

# --- 5. Deployment, Service e HPA ----------------------------------------
echo "🌐 Aplicando Deployment, Service e HPA..."
kubectl apply -f "$BASE_DIR/deployment.yaml"
kubectl apply -f "$OVERLAY_DIR/service.yaml"
kubectl apply -f "$BASE_DIR/hpa.yaml"

# --- 6. Aguarda API ficar pronta ------------------------------------------
echo "⏳ Aguardando API ficar pronta..."
kubectl rollout status deployment/billing-service -n techchallenge --timeout=180s

# --- 7. Acesso -------------------------------------------------------------
echo ""
echo "✅ Deploy concluído!"
echo ""
if [ "$CLUSTER_TYPE" = "minikube" ]; then
  echo "📡 Acesso à API:"
  echo "   minikube service billing-service -n techchallenge"
  echo "   OU"
  echo "   kubectl port-forward svc/billing-service 8080:80 -n techchallenge"
  echo "   → http://localhost:8080/swagger"
elif [ "$CLUSTER_TYPE" = "kind" ]; then
  echo "📡 Acesso à API (Kind não suporta NodePort nativamente):"
  echo "   kubectl port-forward svc/billing-service 8080:80 -n techchallenge"
  echo "   → http://localhost:8080/swagger"
fi
echo ""
echo "📊 Monitorar HPA:"
echo "   kubectl get hpa -n techchallenge -w"
echo ""
echo "🔥 Gerar carga para testar HPA:"
echo "   kubectl run load-gen --image=busybox --restart=Never -n techchallenge -- \\"
echo "     /bin/sh -c 'while true; do wget -q -O- http://billing-service/health/live; done'"
echo ""
echo "🔥 Deletar carga:"
echo "   kubectl delete pod load-gen -n techchallenge"

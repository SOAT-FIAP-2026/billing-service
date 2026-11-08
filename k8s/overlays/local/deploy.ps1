param(
    [ValidateSet("kind", "minikube")]
    [string]$ClusterType = "kind"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $PSCommandPath
$baseDir = Resolve-Path (Join-Path $scriptDir "../../base")

if ($ClusterType -eq "kind") {
    $kindCommand = Get-Command kind -ErrorAction SilentlyContinue
    if ($null -ne $kindCommand) {
        & $kindCommand.Source load docker-image gabrielnetto94/billing-service:latest --name techchallenge
    }
    else {
        $kindPath = "${env:LOCALAPPDATA}\Microsoft\WinGet\Packages\Kubernetes.kind_Microsoft.Winget.Source_8wekyb3d8bbwe\kind.exe"
        if (-not (Test-Path -LiteralPath $kindPath)) {
            throw "Kind não encontrado. Instale-o e reinicie o terminal, ou adicione-o ao PATH."
        }

        & $kindPath load docker-image gabrielnetto94/billing-service:latest --name techchallenge
    }
}

kubectl apply -f (Join-Path $baseDir "namespace.yaml")
kubectl apply -f (Join-Path $scriptDir "postgres.yaml")
kubectl rollout status deployment/postgres -n techchallenge --timeout=120s
# O secrets.yaml nao e versionado: e gerado do template. Os valores padrao
# servem apenas para o cluster local descartavel.
if (-not $env:POSTGRES_PASSWORD) { throw "POSTGRES_PASSWORD must be configured before deployment." }
if (-not $env:MERCADO_PAGO_ACCESS_TOKEN) { $env:MERCADO_PAGO_ACCESS_TOKEN = "" }
if (-not $env:MERCADO_PAGO_WEBHOOK_SECRET) { $env:MERCADO_PAGO_WEBHOOK_SECRET = "" }
if (-not $env:MERCADO_PAGO_NOTIFICATION_URL) { $env:MERCADO_PAGO_NOTIFICATION_URL = "" }

$secretTemplate = Join-Path $scriptDir "secrets.yaml.template"
$secretFile = Join-Path $scriptDir "secrets.yaml"
(Get-Content $secretTemplate -Raw).
    Replace('${POSTGRES_PASSWORD}', $env:POSTGRES_PASSWORD).
    Replace('${MERCADO_PAGO_ACCESS_TOKEN}', $env:MERCADO_PAGO_ACCESS_TOKEN).
    Replace('${MERCADO_PAGO_WEBHOOK_SECRET}', $env:MERCADO_PAGO_WEBHOOK_SECRET).
    Replace('${MERCADO_PAGO_NOTIFICATION_URL}', $env:MERCADO_PAGO_NOTIFICATION_URL) |
    Set-Content -Path $secretFile -Encoding UTF8

kubectl apply -f (Join-Path $scriptDir "configmap.yaml")
kubectl apply -f $secretFile
kubectl apply -f (Join-Path $baseDir "deployment.yaml")
kubectl apply -f (Join-Path $scriptDir "service.yaml")
kubectl apply -f (Join-Path $baseDir "hpa.yaml")
kubectl rollout status deployment/billing-service -n techchallenge --timeout=180s

Write-Host "Deploy local concluído. Acesse a API com: kubectl port-forward svc/billing-service 8080:80 -n techchallenge"

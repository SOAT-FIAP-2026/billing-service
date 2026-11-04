# syntax=docker/dockerfile:1

# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada (cache) copiando apenas os .csproj
COPY --from=shared src/Fiap.TechChallenge.Contracts/Fiap.TechChallenge.Contracts.csproj /shared/src/Fiap.TechChallenge.Contracts/
COPY src/Billing.Service.Domain/Billing.Service.Domain.csproj src/Billing.Service.Domain/
COPY src/Billing.Service.Application/Billing.Service.Application.csproj src/Billing.Service.Application/
COPY src/Billing.Service.Infrastructure/Billing.Service.Infrastructure.csproj src/Billing.Service.Infrastructure/
COPY src/Billing.Service.External/Billing.Service.External.csproj src/Billing.Service.External/
COPY src/Billing.Service.Api/Billing.Service.Api.csproj src/Billing.Service.Api/

RUN dotnet restore src/Billing.Service.Api/Billing.Service.Api.csproj

COPY --from=shared src/Fiap.TechChallenge.Contracts/ /shared/src/Fiap.TechChallenge.Contracts/
COPY src/ src/
RUN dotnet publish src/Billing.Service.Api/Billing.Service.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Stage 2: Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build --chown=app:app /app/publish .
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Billing.Service.Api.dll"]

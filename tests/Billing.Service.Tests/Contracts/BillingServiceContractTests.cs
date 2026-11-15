using System.Text.Json;
using Billing.Service.Api.Controllers.V1;
using Fiap.TechChallenge.Contracts.BillingService;
using Fiap.TechChallenge.Contracts.Events;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;

namespace Billing.Service.Tests.Contracts;

public class BillingServiceContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Rotas_dos_controllers_devem_corresponder_as_rotas_canonicas_do_contrato()
    {
        typeof(PagamentoController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single()
            .Template
            .Should()
            .Be("api/v1/pagamento");

        typeof(OrcamentoController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single()
            .Template
            .Should()
            .Be("api/v1/orcamento");

        BillingApiRoutes.CriarBoleto.Should().Be("api/v1/pagamento/boleto");
        BillingApiRoutes.ObterPagamentoPorId.Should().Be("api/v1/pagamento/{0}");
        BillingApiRoutes.WebhookMercadoPago.Should().Be("api/v1/pagamento/webhook");
        BillingApiRoutes.GerarOrcamento.Should().Be("api/v1/orcamento");
        BillingApiRoutes.ObterOrcamentoPorId.Should().Be("api/v1/orcamento/{0}");
        BillingApiRoutes.ListarOrcamentos.Should().Be("api/v1/orcamento/listar");
        BillingApiRoutes.EnviarOrcamento.Should().Be("api/v1/orcamento/{0}/enviar-orcamento");
    }

    [Fact]
    public void DTOs_de_pagamento_e_orcamento_devem_preservar_payload_no_roundtrip_json()
    {
        var orcamentoId = Guid.NewGuid();
        var boletoRequest = new CriarBoletoRequest(
            orcamentoId,
            "cliente@teste.com",
            "Ana",
            "Silva",
            "12345678901");
        var boletoResponse = new BoletoResponse(
            Guid.NewGuid(),
            "mp-123",
            "pendente",
            249.90m,
            "https://pagamento.test/boleto",
            "00190500954014481606906809350314337370000000100",
            DateTimeOffset.UtcNow.AddDays(3));
        var orcamentoRequest = new GerarOrcamentoRequest(
            [new ItemCalculoOrcamentoDto(
                "Alinhamento",
                120m,
                Quantidade: 1,
                TempoEstimadoMinutos: 45,
                IdItem: Guid.NewGuid(),
                Nome: "Alinhamento 3D")],
            [new ItemCalculoOrcamentoDto(
                "Filtro de óleo",
                35.50m,
                Quantidade: 2,
                IdItem: Guid.NewGuid())]);

        RoundTrip(boletoRequest).Should().BeEquivalentTo(boletoRequest);
        RoundTrip(boletoResponse).Should().BeEquivalentTo(boletoResponse);
        RoundTrip(orcamentoRequest).Should().BeEquivalentTo(orcamentoRequest);
        RoundTrip(new EnviarOrcamentoRequest("cliente@teste.com"))
            .Should()
            .BeEquivalentTo(new EnviarOrcamentoRequest("cliente@teste.com"));
    }

    [Fact]
    public void Eventos_de_billing_devem_conter_osId_e_payload_canônico()
    {
        var osId = Guid.NewGuid();
        var pagamentoId = Guid.NewGuid();

        var orcamento = new OrcamentoGeradoEvent
        {
            OsId = osId,
            OrcamentoId = Guid.NewGuid(),
            ValorTotal = 249.90m,
            LinkPagamento = "https://pagamento.test/checkout",
            QrCodePix = "pix-code"
        };
        var confirmado = new PagamentoConfirmadoEvent
        {
            OsId = osId,
            PagamentoId = pagamentoId,
            Valor = 249.90m
        };
        var cancelado = new PagamentoCanceladoCompensacaoEvent
        {
            OsId = osId,
            Motivo = "Pagamento expirado"
        };

        orcamento.CorrelationId.Should().Be(osId);
        orcamento.TipoEvento.Should().Be(nameof(OrcamentoGeradoEvent));
        orcamento.ValorTotal.Should().Be(249.90m);
        confirmado.CorrelationId.Should().Be(osId);
        confirmado.PagamentoId.Should().Be(pagamentoId);
        confirmado.Valor.Should().Be(249.90m);
        cancelado.CorrelationId.Should().Be(osId);
        cancelado.Motivo.Should().Be("Pagamento expirado");

        RoundTrip(orcamento).Should().BeEquivalentTo(orcamento);
        RoundTrip(confirmado).Should().BeEquivalentTo(confirmado);
        RoundTrip(cancelado).Should().BeEquivalentTo(cancelado);
    }

    [Fact]
    public void Enums_de_billing_devem_manter_os_valores_do_contrato()
    {
        Enum.GetNames<MetodoPagamentoContrato>()
            .Should()
            .Equal("Pix", "CartaoCredito", "Boleto");

        Enum.GetNames<StatusPagamentoContrato>()
            .Should()
            .Equal("Pendente", "Aprovado", "Rejeitado", "Cancelado", "Expirado");
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }
}

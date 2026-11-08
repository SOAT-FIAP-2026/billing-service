using Fiap.TechChallenge.Contracts.BillingService;
using Billing.Service.Application.Services;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Billing.Service.Tests.Application.Services;

public class PagamentoServiceTests
{
    [Fact]
    public async Task CriarBoletoAsync_DeveCriarEPersistirPagamento()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 150m);
        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        var pagamentoRepository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();

        orcamentoService.Setup(x => x.BuscarOrcamento(orcamento.Id)).ReturnsAsync(orcamento);
        mercadoPago.Setup(x => x.CriarBoletoAsync(
                It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse(
                "mp-1", "pending", "pending", "https://boleto", "34191", DateTimeOffset.UtcNow.AddDays(3)));

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var service = new PagamentoService(
            orcamentoService.Object,
            pagamentoRepository.Object,
            mercadoPago.Object,
            configuration);
        var result = await service.CriarBoletoAsync(
            CriarRequest(orcamento.Id),
            CancellationToken.None);

        Assert.Equal("mp-1", result.PaymentId);
        Assert.Equal("https://boleto", result.BoletoUrl);
        pagamentoRepository.Verify(x => x.AdicionarAsync(It.Is<Pagamento>(p =>
            p.IdOrcamento == orcamento.Id && p.Status == PagamentoStatus.Pendente)), Times.Once);
    }

    [Fact]
    public async Task CriarBoletoAsync_DeveEnviarDadosCompletosAoMercadoPago()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 150m);
        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        var pagamentoRepository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        MercadoPagoBoletoRequest? requestEnviado = null;

        orcamentoService.Setup(x => x.BuscarOrcamento(orcamento.Id)).ReturnsAsync(orcamento);
        mercadoPago
            .Setup(x => x.CriarBoletoAsync(It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoBoletoRequest, CancellationToken>((request, _) => requestEnviado = request)
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-1", "approved", "accredited", null, null, null));
        var service = CriarService(orcamentoService, pagamentoRepository, mercadoPago,
            ("MercadoPago:NotificationUrl", "https://api.test/webhook"));

        await service.CriarBoletoAsync(
            CriarRequest(orcamento.Id),
            CancellationToken.None);

        Assert.NotNull(requestEnviado);
        Assert.Equal(150m, requestEnviado!.Valor);
        Assert.Equal($"Pagamento do orçamento {orcamento.Id}", requestEnviado.Descricao);
        Assert.Equal(orcamento.Id.ToString(), requestEnviado.ReferenciaExterna);
        Assert.Equal("cliente@teste.com", requestEnviado.Email);
        Assert.Equal("Cliente", requestEnviado.Nome);
        Assert.Equal("Teste", requestEnviado.Sobrenome);
        Assert.Equal("12345678909", requestEnviado.Cpf);
        Assert.Equal("https://api.test/webhook", requestEnviado.NotificationUrl);
        Assert.Equal(orcamento.Id.ToString(), requestEnviado.IdempotencyKey);
    }

    [Fact]
    public async Task CriarBoletoAsync_DevePersistirRetornoCompletoDoMercadoPago()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 150m);
        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        var pagamentoRepository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        Pagamento? persistido = null;
        var vencimento = DateTimeOffset.UtcNow.AddDays(3);

        orcamentoService.Setup(x => x.BuscarOrcamento(orcamento.Id)).ReturnsAsync(orcamento);
        mercadoPago.Setup(x => x.CriarBoletoAsync(It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse(
                "mp-1", "rejected", "cc_rejected", "https://boleto", "34191", vencimento));
        pagamentoRepository.Setup(x => x.AdicionarAsync(It.IsAny<Pagamento>()))
            .Callback<Pagamento>(pagamento => persistido = pagamento);
        var service = CriarService(orcamentoService, pagamentoRepository, mercadoPago);

        var result = await service.CriarBoletoAsync(
            CriarRequest(orcamento.Id),
            CancellationToken.None);

        Assert.NotNull(persistido);
        Assert.Equal("mp-1", persistido!.IdMercadoPago);
        Assert.Equal(PagamentoStatus.Rejeitado, persistido.Status);
        Assert.Equal("cc_rejected", persistido.DetalheStatus);
        Assert.Equal("https://boleto", persistido.BoletoUrl);
        Assert.Equal("34191", persistido.CodigoBarras);
        Assert.Equal(vencimento, persistido.DataVencimento);
        Assert.Equal("rejeitado", result.Status);
    }

    [Fact]
    public async Task CriarBoletoAsync_DeveNormalizarVencimentoParaUtc()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 150m);
        var pagamentoRepository = new Mock<IPagamentoRepository>();
        Pagamento? persistido = null;
        pagamentoRepository
            .Setup(x => x.AdicionarAsync(It.IsAny<Pagamento>()))
            .Callback<Pagamento>(pagamento => persistido = pagamento);

        var vencimento = new DateTimeOffset(2026, 11, 8, 14, 0, 0, TimeSpan.FromHours(-4));
        var mercadoPago = new Mock<IMercadoPagoClient>();
        mercadoPago
            .Setup(x => x.CriarBoletoAsync(It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-utc", "pending", "pending", null, null, vencimento));

        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        orcamentoService.Setup(x => x.BuscarOrcamento(orcamento.Id)).ReturnsAsync(orcamento);

        var service = CriarService(orcamentoService, pagamentoRepository, mercadoPago);
        await service.CriarBoletoAsync(CriarRequest(orcamento.Id), CancellationToken.None);

        Assert.NotNull(persistido);
        Assert.Equal(TimeSpan.Zero, persistido!.DataVencimento!.Value.Offset);
        Assert.Equal(vencimento.UtcDateTime, persistido.DataVencimento.Value.UtcDateTime);
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoOrcamentoNaoExiste_DevePropagarExcecao()
    {
        var exception = new InvalidOperationException("Orçamento não encontrado.");
        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        orcamentoService.Setup(x => x.BuscarOrcamento(It.IsAny<Guid>())).ThrowsAsync(exception);
        var service = CriarService(orcamentoService: orcamentoService, mercadoPago: mercadoPago);

        var result = service.CriarBoletoAsync(
            CriarRequest(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => result));
        mercadoPago.Verify(
            x => x.CriarBoletoAsync(It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CriarBoletoAsync_QuandoMercadoPagoFalhar_DevePropagarExcecaoENaoPersistir()
    {
        var orcamento = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 150m);
        var exception = new HttpRequestException("Mercado Pago indisponível.");
        var orcamentoService = new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>();
        var pagamentoRepository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        orcamentoService.Setup(x => x.BuscarOrcamento(orcamento.Id)).ReturnsAsync(orcamento);
        mercadoPago.Setup(x => x.CriarBoletoAsync(It.IsAny<MercadoPagoBoletoRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var service = CriarService(orcamentoService, pagamentoRepository, mercadoPago);

        var result = service.CriarBoletoAsync(
            CriarRequest(orcamento.Id),
            CancellationToken.None);

        Assert.Same(exception, await Assert.ThrowsAsync<HttpRequestException>(() => result));
        pagamentoRepository.Verify(x => x.AdicionarAsync(It.IsAny<Pagamento>()), Times.Never);
    }

    [Fact]
    public async Task BuscarAsync_QuandoNaoEncontrar_DeveRetornarNulo()
    {
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(x => x.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Pagamento?)null);
        var service = CriarService(pagamentoRepository: repository);

        var result = await service.BuscarAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task BuscarAsync_QuandoEncontrar_DeveMapearResposta()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 150m, "ref-1");
        pagamento.AssociarPagamentoMercadoPago(
            "mp-1", "https://boleto", "34191", null, PagamentoStatus.Aprovado, "accredited");
        var repository = new Mock<IPagamentoRepository>();
        repository.Setup(x => x.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        var service = CriarService(pagamentoRepository: repository);

        var result = await service.BuscarAsync(pagamento.Id);

        Assert.NotNull(result);
        Assert.Equal(pagamento.Id, result!.PagamentoId);
        Assert.Equal("mp-1", result.PaymentId);
        Assert.Equal("aprovado", result.Status);
        Assert.Equal(150m, result.Valor);
        Assert.Equal("https://boleto", result.BoletoUrl);
    }

    [Fact]
    public async Task AtualizarPorNotificacaoAsync_QuandoNaoEncontrarPagamento_NaoDeveConsultarNemAtualizar()
    {
        var repository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        repository.Setup(x => x.ObterPorIdMercadoPagoAsync("mp-1")).ReturnsAsync((Pagamento?)null);
        var service = CriarService(pagamentoRepository: repository, mercadoPago: mercadoPago);

        var result = await service.AtualizarPorNotificacaoAsync("mp-1", CancellationToken.None);

        Assert.Null(result);
        mercadoPago.Verify(x => x.ObterPagamentoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.AtualizarAsync(It.IsAny<Pagamento>()), Times.Never);
    }

    [Theory]
    [InlineData("approved", PagamentoStatus.Aprovado)]
    [InlineData("rejected", PagamentoStatus.Rejeitado)]
    [InlineData("cancelled", PagamentoStatus.Cancelado)]
    [InlineData("refunded", PagamentoStatus.Cancelado)]
    [InlineData("expired", PagamentoStatus.Expirado)]
    [InlineData("pending", PagamentoStatus.Pendente)]
    [InlineData("unknown", PagamentoStatus.Pendente)]
    public async Task AtualizarPorNotificacaoAsync_DeveAtualizarStatus(
        string mercadoPagoStatus,
        PagamentoStatus statusEsperado)
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 150m, "ref-1");
        pagamento.AssociarPagamentoMercadoPago("mp-1", null, null, null, PagamentoStatus.Pendente, null);
        var repository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        repository.Setup(x => x.ObterPorIdMercadoPagoAsync("mp-1")).ReturnsAsync(pagamento);
        mercadoPago.Setup(x => x.ObterPagamentoAsync("mp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-1", mercadoPagoStatus, "detail", null, null, null));
        var service = CriarService(pagamentoRepository: repository, mercadoPago: mercadoPago);

        var result = await service.AtualizarPorNotificacaoAsync("mp-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(statusEsperado, pagamento.Status);
        Assert.Equal("detail", pagamento.DetalheStatus);
        repository.Verify(x => x.AtualizarAsync(pagamento), Times.Once);
    }

    [Fact]
    public async Task AtualizarPorNotificacaoAsync_DevePropagarCancellationToken()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 150m, "ref-1");
        pagamento.AssociarPagamentoMercadoPago("mp-1", null, null, null, PagamentoStatus.Pendente, null);
        var repository = new Mock<IPagamentoRepository>();
        var mercadoPago = new Mock<IMercadoPagoClient>();
        var cancellationToken = new CancellationTokenSource().Token;
        repository.Setup(x => x.ObterPorIdMercadoPagoAsync("mp-1")).ReturnsAsync(pagamento);
        mercadoPago.Setup(x => x.ObterPagamentoAsync("mp-1", cancellationToken))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-1", "approved", "accredited", null, null, null));
        var service = CriarService(pagamentoRepository: repository, mercadoPago: mercadoPago);

        await service.AtualizarPorNotificacaoAsync("mp-1", cancellationToken);

        mercadoPago.Verify(x => x.ObterPagamentoAsync("mp-1", cancellationToken), Times.Once);
    }

    private static PagamentoService CriarService(
        Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>? orcamentoService = null,
        Mock<IPagamentoRepository>? pagamentoRepository = null,
        Mock<IMercadoPagoClient>? mercadoPago = null,
        params (string Key, string Value)[] configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();
        return new PagamentoService(
            orcamentoService?.Object ?? new Mock<global::Billing.Service.Application.Interfaces.IOrcamentoService>().Object,
            pagamentoRepository?.Object ?? new Mock<IPagamentoRepository>().Object,
            mercadoPago?.Object ?? new Mock<IMercadoPagoClient>().Object,
            configuration);
    }

    private static CriarBoletoRequest CriarRequest(Guid orcamentoId) =>
        new(
            orcamentoId,
            "cliente@teste.com",
            "Cliente",
            "Teste",
            "12345678909",
            new EnderecoBoletoRequest(
                "01311-000",
                "Avenida Paulista",
                "1000",
                "Bela Vista",
                "São Paulo",
                "SP"));
}

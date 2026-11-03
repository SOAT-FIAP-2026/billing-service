using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Services;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
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
            new CriarBoletoRequest(orcamento.Id, "cliente@teste.com", "Cliente", "Teste", "12345678909"),
            CancellationToken.None);

        Assert.Equal("mp-1", result.PaymentId);
        Assert.Equal("https://boleto", result.BoletoUrl);
        pagamentoRepository.Verify(x => x.AdicionarAsync(It.Is<Pagamento>(p =>
            p.IdOrcamento == orcamento.Id && p.Status == PagamentoStatus.Pendente)), Times.Once);
    }
}

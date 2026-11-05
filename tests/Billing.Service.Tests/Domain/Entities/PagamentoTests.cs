using Billing.Service.Domain.Entities;

namespace Billing.Service.Tests.Domain.Entities;

public class PagamentoTests
{
    [Fact]
    public void CriarBoleto_DeveManterDadosDaTransacao()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 125.50m, "ref-1");

        Assert.Equal(PagamentoStatus.Pendente, pagamento.Status);
        Assert.Equal(125.50m, pagamento.Valor.Valor);
        Assert.Equal("ref-1", pagamento.ReferenciaExterna);
        Assert.Equal("boleto", pagamento.MeioPagamento);
    }

    [Fact]
    public void AtualizarStatus_DeveAlterarSituacao()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 100m, "ref-1");

        pagamento.AtualizarStatus(PagamentoStatus.Aprovado, "accredited");

        Assert.Equal(PagamentoStatus.Aprovado, pagamento.Status);
        Assert.Equal("accredited", pagamento.DetalheStatus);
    }

    [Fact]
    public void CriarBoleto_ComValorNegativo_DeveLancarExcecao()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Pagamento.CriarBoleto(Guid.NewGuid(), -0.01m, "ref-1"));

        Assert.Equal("O valor monetário não pode ser negativo.", exception.Message);
    }

    [Fact]
    public void CriarBoleto_DeveArredondarValorParaDuasCasas()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 125.556m, "ref-1");

        Assert.Equal(125.56m, pagamento.Valor.Valor);
    }

    [Fact]
    public void AssociarPagamentoMercadoPago_DevePreservarDadosDoBoleto()
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 100m, "ref-1");
        var vencimento = DateTimeOffset.UtcNow.AddDays(3);

        pagamento.AssociarPagamentoMercadoPago(
            "mp-123",
            "https://boleto",
            "34191",
            vencimento,
            PagamentoStatus.Pendente,
            "pending_waiting_payment");

        Assert.Equal("mp-123", pagamento.IdMercadoPago);
        Assert.Equal("https://boleto", pagamento.BoletoUrl);
        Assert.Equal("34191", pagamento.CodigoBarras);
        Assert.Equal(vencimento, pagamento.DataVencimento);
        Assert.Equal(PagamentoStatus.Pendente, pagamento.Status);
        Assert.Equal("pending_waiting_payment", pagamento.DetalheStatus);
    }

    [Theory]
    [InlineData(PagamentoStatus.Pendente)]
    [InlineData(PagamentoStatus.Aprovado)]
    [InlineData(PagamentoStatus.Rejeitado)]
    [InlineData(PagamentoStatus.Cancelado)]
    [InlineData(PagamentoStatus.Expirado)]
    public void AtualizarStatus_DeveSuportarTodosOsEstados(
        PagamentoStatus status)
    {
        var pagamento = Pagamento.CriarBoleto(Guid.NewGuid(), 100m, "ref-1");

        pagamento.AtualizarStatus(status, "status_detail");

        Assert.Equal(status, pagamento.Status);
        Assert.Equal("status_detail", pagamento.DetalheStatus);
    }

    [Fact]
    public void AtualizarStatus_DeveAtualizarTimestampSemAlterarDadosOriginais()
    {
        var idOrcamento = Guid.NewGuid();
        var pagamento = Pagamento.CriarBoleto(idOrcamento, 100m, "ref-1");
        var criadoEm = pagamento.CriadoEm;
        var atualizadoEm = pagamento.AtualizadoEm;

        pagamento.AtualizarStatus(PagamentoStatus.Cancelado, null);

        Assert.Equal(idOrcamento, pagamento.IdOrcamento);
        Assert.Equal(100m, pagamento.Valor.Valor);
        Assert.Equal("ref-1", pagamento.ReferenciaExterna);
        Assert.Equal("boleto", pagamento.MeioPagamento);
        Assert.Equal(criadoEm, pagamento.CriadoEm);
        Assert.True(pagamento.AtualizadoEm >= atualizadoEm);
    }
}

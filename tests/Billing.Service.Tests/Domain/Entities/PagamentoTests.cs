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
}

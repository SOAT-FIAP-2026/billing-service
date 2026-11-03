using Billing.Service.Domain.ValueObjects;

namespace Billing.Service.Domain.Entities;

public enum PagamentoStatus
{
    Pendente,
    Aprovado,
    Rejeitado,
    Cancelado,
    Expirado
}

public class Pagamento : EntidadeAuditavel
{
    public Guid IdOrcamento { get; private set; }
    public string? IdMercadoPago { get; private set; }
    public string MeioPagamento { get; private set; } = null!;
    public PagamentoStatus Status { get; private set; }
    public string? DetalheStatus { get; private set; }
    public ValorMonetarioVO Valor { get; private set; } = null!;
    public string ReferenciaExterna { get; private set; } = null!;
    public string? BoletoUrl { get; private set; }
    public string? CodigoBarras { get; private set; }
    public DateTimeOffset? DataVencimento { get; private set; }

    protected Pagamento() { }

    private Pagamento(Guid idOrcamento, decimal valor, string referenciaExterna)
    {
        IdOrcamento = idOrcamento;
        Valor = new ValorMonetarioVO(valor);
        ReferenciaExterna = referenciaExterna;
        MeioPagamento = "boleto";
        Status = PagamentoStatus.Pendente;
    }

    public static Pagamento CriarBoleto(Guid idOrcamento, decimal valor, string referenciaExterna) =>
        new(idOrcamento, valor, referenciaExterna);

    public void AssociarPagamentoMercadoPago(
        string idMercadoPago,
        string? boletoUrl,
        string? codigoBarras,
        DateTimeOffset? dataVencimento,
        PagamentoStatus status,
        string? detalheStatus)
    {
        IdMercadoPago = idMercadoPago;
        BoletoUrl = boletoUrl;
        CodigoBarras = codigoBarras;
        DataVencimento = dataVencimento;
        AtualizarStatus(status, detalheStatus);
    }

    public void AtualizarStatus(PagamentoStatus status, string? detalheStatus)
    {
        Status = status;
        DetalheStatus = detalheStatus;
        AtualizarTimestamp();
    }
}

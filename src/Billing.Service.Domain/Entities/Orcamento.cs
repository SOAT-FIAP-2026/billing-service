using Billing.Service.Domain.ValueObjects;

namespace Billing.Service.Domain.Entities
{
    public class Orcamento : EntidadeAuditavel
    {
        public Guid IdOrdemServico { get; private set; }
        public Guid IdOrcamento { get; private set; }
        public ValorMonetarioVO ValorTotal { get; private set; } = null!;

        protected Orcamento() { }

        public Orcamento(Guid idOrdemServico, Guid idOrcamento, decimal valorTotal) : base()
        {
            IdOrdemServico = idOrdemServico;
            IdOrcamento = idOrcamento;
            ValorTotal = new ValorMonetarioVO(valorTotal);
        }

        public void CalcularOrcamento(Orcamento orcamento)
        {
            IdOrdemServico = orcamento.IdOrdemServico;
            IdOrcamento = orcamento.IdOrcamento;
            ValorTotal = orcamento.ValorTotal;
            AtualizarTimestamp();
        }

        public void AlterarValorTotal(decimal valor)
        {
            ValorTotal = new ValorMonetarioVO(valor);
            AtualizarTimestamp();
        }

        public void CalcularOrcamento(IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo)
        {
            decimal valorTotal = servicos.Sum(servico => servico.ValorUnitario.Valor) + 
                                pecasInsumo.Sum(peca => peca.ValorUnitario.Valor);

            AlterarValorTotal(valorTotal);
        }

    }
}

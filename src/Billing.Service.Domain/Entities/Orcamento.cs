using Billing.Service.Domain.ValueObjects;
using Fiap.TechChallenge.Contracts.BillingService;

namespace Billing.Service.Domain.Entities
{
    public class Orcamento : EntidadeAuditavel
    {
        public Guid IdOrdemServico { get; private set; }
        public Guid IdOrcamento => Id;
        public ValorMonetarioVO ValorTotal { get; private set; } = null!;

        protected Orcamento() { }

        public Orcamento(Guid idOrdemServico, decimal valorTotal = 0) : base()
        {
            IdOrdemServico = idOrdemServico;
            ValorTotal = new ValorMonetarioVO(valorTotal);
        }

        public Orcamento(Guid idOrdemServico, Guid idOrcamento, decimal valorTotal = 0) : base()
        {
            IdOrdemServico = idOrdemServico;
            if (idOrcamento != Guid.Empty)
                Id = idOrcamento;
            ValorTotal = new ValorMonetarioVO(valorTotal);
        }

        public void CalcularOrcamento(Orcamento orcamento)
        {
            IdOrdemServico = orcamento.IdOrdemServico;
            if (orcamento.Id != Guid.Empty)
                Id = orcamento.Id;
            ValorTotal = orcamento.ValorTotal;
            AtualizarTimestamp();
        }

        public void AlterarValorTotal(decimal valor)
        {
            ValorTotal = new ValorMonetarioVO(valor);
            AtualizarTimestamp();
        }

        public void CalcularOrcamento(
            IEnumerable<ItemCalculoOrcamentoDto>? servicos,
            IEnumerable<ItemCalculoOrcamentoDto>? pecasInsumo)
        {
            decimal totalServicos = (servicos ?? Enumerable.Empty<ItemCalculoOrcamentoDto>())
                .Sum(s => s.ValorUnitario * (s.Quantidade > 0 ? s.Quantidade : 1));

            decimal totalPecas = (pecasInsumo ?? Enumerable.Empty<ItemCalculoOrcamentoDto>())
                .Sum(p => p.ValorUnitario * (p.Quantidade > 0 ? p.Quantidade : 1));

            AlterarValorTotal(totalServicos + totalPecas);
        }

        public void CalcularOrcamento(IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo)
        {
            decimal valorTotal = servicos.Sum(servico => servico.ValorUnitario.Valor) + 
                                pecasInsumo.Sum(peca => peca.ValorUnitario.Valor);

            AlterarValorTotal(valorTotal);
        }
    }
}

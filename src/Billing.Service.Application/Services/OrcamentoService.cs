using System.ComponentModel;
using System.IO;
using Billing.Service.Application.DTOs.Requests;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Exceptions;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Billing.Service.Domain.Interfaces.Observability;
using Billing.Service.Domain.ValueObjects;

namespace Billing.Service.Application.Services
{
    public class OrcamentoService : IOrcamentoService
    {
        private readonly IOrcamentoRepository _orcamentoRepository;
        private readonly IEmailService _emailService;
        private readonly IObservabilityMetrics? _observabilityMetrics;

        public OrcamentoService(
            IOrcamentoRepository orcamentoRepository,
            IEmailService emailService,
            IObservabilityMetrics? observabilityMetrics = null)
        {
            _orcamentoRepository = orcamentoRepository;
            _emailService = emailService;
            _observabilityMetrics = observabilityMetrics;
        }

        public async Task<Orcamento> GerarOrcamento(Guid idOrdemServico, IReadOnlyCollection<Servico> servicos, IReadOnlyCollection<PecaInsumo> pecasInsumo)
        {
            var orcamento = new Orcamento(idOrdemServico, Guid.NewGuid(), 0);

            orcamento.CalcularOrcamento(servicos, pecasInsumo);

            await _orcamentoRepository.Adicionar(orcamento);

            return orcamento;
        }

        public async Task<Orcamento> BuscarOrcamento(Guid idOrcamento)
        {
            var orcamento = await _orcamentoRepository.ObterPorId(idOrcamento);

            if (orcamento == null)
                throw new OrcamentoNaoEncontradoException(idOrcamento);

            return orcamento;
        }

        public async Task<IReadOnlyCollection<Orcamento>> BuscarTodosOrcamentos(int page, int pageSize)
        {
            var orcamentos = await _orcamentoRepository.ObterTodos(page, pageSize);

            if (orcamentos == null || !orcamentos.Any())
                throw new OrcamentoNaoEncontradoException(Guid.Empty);

            return orcamentos;
        }

        public async Task<string> EnviarOrcamento(Guid idOrcamento, string emailCliente)
        {
            var orcamento = await _orcamentoRepository.ObterPorId(idOrcamento);

            if (orcamento == null)
                throw new OrcamentoNaoEncontradoException(idOrcamento);

            string assunto = "Seu orçamento está pronto!";
            string corpoHtml = $"<p>Olá,</p><p>Seu orçamento com ID {orcamento.Id} foi gerado com sucesso.</p><p>Valor total: R$ {orcamento.ValorTotal}</p>";

            bool emailEnviado = await _emailService.EnviarEmailAsync(emailCliente, assunto, corpoHtml);

            if (!emailEnviado)
                throw new InvalidOperationException("Falha ao enviar o e-mail de notificação.");

            return "E-mail de orçamento enviado com sucesso.";
        }
    }
}

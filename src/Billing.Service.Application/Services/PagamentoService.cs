using Fiap.TechChallenge.Contracts.BillingService;
using Fiap.TechChallenge.Contracts.Events;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Billing.Service.Application.Services;

public class PagamentoService(
    IOrcamentoService orcamentoService,
    IPagamentoRepository pagamentoRepository,
    IMercadoPagoClient mercadoPagoClient,
    IConfiguration configuration,
    IBillingEventPublisher? eventPublisher = null,
    ILogger<PagamentoService>? logger = null) : IPagamentoService
{
    public async Task<BoletoResponse> CriarBoletoAsync(
        CriarBoletoRequest request,
        CancellationToken cancellationToken)
    {
        var orcamento = await orcamentoService.BuscarOrcamento(request.IdOrcamento);
        var referencia = orcamento.Id.ToString();
        var mpResponse = await mercadoPagoClient.CriarBoletoAsync(
            new MercadoPagoBoletoRequest(
                orcamento.ValorTotal.Valor,
                $"Pagamento do orçamento {orcamento.Id}",
                referencia,
                request.Email,
                request.Nome,
                request.Sobrenome,
                request.Cpf,
                configuration["MercadoPago:NotificationUrl"],
                orcamento.Id.ToString()),
            cancellationToken);

        var pagamento = Pagamento.CriarBoleto(orcamento.Id, orcamento.ValorTotal.Valor, referencia);
        pagamento.AssociarPagamentoMercadoPago(
            mpResponse.Id,
            mpResponse.BoletoUrl,
            mpResponse.CodigoBarras,
            mpResponse.DataVencimento,
            ConverterStatus(mpResponse.Status),
            mpResponse.StatusDetail);
        await pagamentoRepository.AdicionarAsync(pagamento);

        return ToResponse(pagamento);
    }

    public async Task<BoletoResponse?> BuscarAsync(Guid pagamentoId)
    {
        var pagamento = await pagamentoRepository.ObterPorIdAsync(pagamentoId);
        return pagamento is null ? null : ToResponse(pagamento);
    }

    public async Task<BoletoResponse?> AtualizarPorNotificacaoAsync(
        string paymentId,
        CancellationToken cancellationToken)
    {
        var pagamento = await pagamentoRepository.ObterPorIdMercadoPagoAsync(paymentId);
        if (pagamento is null)
            return null;

        var mpResponse = await mercadoPagoClient.ObterPagamentoAsync(paymentId, cancellationToken);
        var novoStatus = ConverterStatus(mpResponse.Status);
        pagamento.AtualizarStatus(novoStatus, mpResponse.StatusDetail);
        await pagamentoRepository.AtualizarAsync(pagamento);

        var orcamento = await orcamentoService.BuscarOrcamento(pagamento.IdOrcamento);

        if (eventPublisher != null)
        {
            try
            {
                if (novoStatus == PagamentoStatus.Aprovado)
                {
                    await eventPublisher.PublishAsync(new PagamentoConfirmadoEvent
                    {
                        OsId = orcamento.IdOrdemServico,
                        PagamentoId = pagamento.Id,
                        Valor = pagamento.Valor.Valor
                    }, cancellationToken);
                }
                else if (novoStatus is PagamentoStatus.Rejeitado or PagamentoStatus.Cancelado or PagamentoStatus.Expirado)
                {
                    await eventPublisher.PublishAsync(new PagamentoCanceladoCompensacaoEvent
                    {
                        OsId = orcamento.IdOrdemServico,
                        Motivo = mpResponse.StatusDetail ?? $"Pagamento {pagamento.Id} não aprovado: {novoStatus}"
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Falha ao publicar evento de pagamento para OS {OsId}", orcamento.IdOrdemServico);
            }
        }

        return ToResponse(pagamento);
    }

    private static BoletoResponse ToResponse(Pagamento pagamento) =>
        new(
            pagamento.Id,
            pagamento.IdMercadoPago!,
            pagamento.Status.ToString().ToLowerInvariant(),
            pagamento.Valor.Valor,
            pagamento.BoletoUrl,
            pagamento.CodigoBarras,
            pagamento.DataVencimento);

    private static PagamentoStatus ConverterStatus(string status) => status.ToLowerInvariant() switch
    {
        "approved" or "aprovado" => PagamentoStatus.Aprovado,
        "rejected" or "rejeitado" => PagamentoStatus.Rejeitado,
        "cancelled" or "canceled" or "cancelado" => PagamentoStatus.Cancelado,
        "refunded" or "estornado" => PagamentoStatus.Cancelado,
        "expired" or "expirado" => PagamentoStatus.Expirado,
        _ => PagamentoStatus.Pendente
    };
}

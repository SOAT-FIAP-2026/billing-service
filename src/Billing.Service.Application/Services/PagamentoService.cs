using Fiap.TechChallenge.Contracts.BillingService;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Microsoft.Extensions.Configuration;

namespace Billing.Service.Application.Services;

public class PagamentoService(
    IOrcamentoService orcamentoService,
    IPagamentoRepository pagamentoRepository,
    IMercadoPagoClient mercadoPagoClient,
    IConfiguration configuration) : IPagamentoService
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
        pagamento.AtualizarStatus(ConverterStatus(mpResponse.Status), mpResponse.StatusDetail);
        await pagamentoRepository.AtualizarAsync(pagamento);
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
        "approved" => PagamentoStatus.Aprovado,
        "rejected" => PagamentoStatus.Rejeitado,
        "cancelled" => PagamentoStatus.Cancelado,
        "refunded" => PagamentoStatus.Cancelado,
        "expired" => PagamentoStatus.Expirado,
        _ => PagamentoStatus.Pendente
    };
}

using Billing.Service.Application.DTOs.Common;
using Billing.Service.Application.Interfaces;
using Billing.Service.Application.Services;
using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Exceptions;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.Interfaces.Service;
using Fiap.TechChallenge.Contracts.BillingService;
using Fiap.TechChallenge.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Billing.Service.Tests.Application.Services;

public class OrcamentoServiceTests
{
    [Fact]
    public async Task GerarOrcamento_ComDtos_DeveCriarNovo_AtualizarExistente_EPublicarEvento()
    {
        var repoMock = new Mock<IOrcamentoRepository>();
        var emailMock = new Mock<IEmailService>();
        var pubMock = new Mock<IBillingEventPublisher>();

        Orcamento? armazenado = null;
        repoMock.Setup(r => r.ObterPorOrdemServicoId(It.IsAny<Guid>()))
            .ReturnsAsync(() => armazenado);
        repoMock.Setup(r => r.Adicionar(It.IsAny<Orcamento>()))
            .Callback<Orcamento>(o => armazenado = o)
            .Returns(Task.CompletedTask);

        var service = new OrcamentoService(
            repoMock.Object,
            emailMock.Object,
            null,
            pubMock.Object,
            NullLogger<OrcamentoService>.Instance);

        var osId = Guid.NewGuid();
        var servicos = new[] { new ItemCalculoOrcamentoDto("Troca de oleo", 150m, 1) };
        var pecas = new[] { new ItemCalculoOrcamentoDto("Filtro", 50m, 2) };

        var criado = await service.GerarOrcamento(osId, servicos, pecas);
        criado.ValorTotal.Valor.Should().Be(250m);
        repoMock.Verify(r => r.Adicionar(It.IsAny<Orcamento>()), Times.Once);
        pubMock.Verify(p => p.PublishAsync(It.IsAny<OrcamentoGeradoEvent>(), It.IsAny<CancellationToken>()), Times.Once);

        // Segunda chamada atualiza existente e tolera falha no publisher
        pubMock.Setup(p => p.PublishAsync(It.IsAny<OrcamentoGeradoEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SNS indisponivel"));

        var atualizado = await service.GerarOrcamento(osId, servicos, []);
        atualizado.ValorTotal.Valor.Should().Be(150m);
        repoMock.Verify(r => r.Atualizar(It.IsAny<Orcamento>()), Times.Once);
    }

    [Fact]
    public async Task GerarOrcamento_ComEntidades_DeveCriarNovo_E_AtualizarExistente()
    {
        var repoMock = new Mock<IOrcamentoRepository>();
        var emailMock = new Mock<IEmailService>();
        var pubMock = new Mock<IBillingEventPublisher>();

        Orcamento? armazenado = null;
        repoMock.Setup(r => r.ObterPorOrdemServicoId(It.IsAny<Guid>()))
            .ReturnsAsync(() => armazenado);
        repoMock.Setup(r => r.Adicionar(It.IsAny<Orcamento>()))
            .Callback<Orcamento>(o => armazenado = o)
            .Returns(Task.CompletedTask);

        var service = new OrcamentoService(
            repoMock.Object,
            emailMock.Object,
            null,
            pubMock.Object,
            NullLogger<OrcamentoService>.Instance);

        var osId = Guid.NewGuid();
        var servico = new Servico("Troca de oleo", "Troca completa", 120m, 45);
        servico.Atualizar("Troca Premium", "Troca sintetica", 140m, 50);

        var peca = new PecaInsumo("Filtro", 40m);
        peca.AlterarDescricao("Filtro Original");
        peca.AlterarValorUnitario(60m);

        var criado = await service.GerarOrcamento(osId, [servico], [peca]);
        criado.ValorTotal.Valor.Should().Be(200m);

        // Atualiza existente e simula falha no publisher
        pubMock.Setup(p => p.PublishAsync(It.IsAny<OrcamentoGeradoEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SNS offline"));

        var atualizado = await service.GerarOrcamento(osId, [servico], []);
        atualizado.ValorTotal.Valor.Should().Be(140m);
    }

    [Fact]
    public async Task BuscarOrcamento_BuscarTodos_E_EnviarOrcamento_DevemValidarFluxosCompletos()
    {
        var repoMock = new Mock<IOrcamentoRepository>();
        var emailMock = new Mock<IEmailService>();
        var service = new OrcamentoService(repoMock.Object, emailMock.Object);

        var orc = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 300m);
        var copia = new Orcamento(Guid.NewGuid(), 0m);
        copia.CalcularOrcamento(orc);
        copia.IdOrcamento.Should().Be(orc.Id);

        repoMock.Setup(r => r.ObterPorId(orc.Id)).ReturnsAsync(orc);
        (await service.BuscarOrcamento(orc.Id)).Should().BeSameAs(orc);

        await Assert.ThrowsAsync<OrcamentoNaoEncontradoException>(() => service.BuscarOrcamento(Guid.NewGuid()));

        repoMock.Setup(r => r.ObterTodos(1, 10)).ReturnsAsync([orc]);
        (await service.BuscarTodosOrcamentos(1, 10)).Should().ContainSingle();

        repoMock.Setup(r => r.ObterTodos(2, 10)).ReturnsAsync([]);
        await Assert.ThrowsAsync<OrcamentoNaoEncontradoException>(() => service.BuscarTodosOrcamentos(2, 10));

        await Assert.ThrowsAsync<OrcamentoNaoEncontradoException>(() => service.EnviarOrcamento(Guid.NewGuid(), "cli@teste.com"));

        emailMock.Setup(e => e.EnviarEmailAsync("cli@teste.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        var msg = await service.EnviarOrcamento(orc.Id, "cli@teste.com");
        msg.Should().Contain("sucesso");

        emailMock.Setup(e => e.EnviarEmailAsync("cli@teste.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnviarOrcamento(orc.Id, "cli@teste.com"));
    }

    [Fact]
    public async Task PagamentoService_AtualizarPorNotificacao_QuandoRejeitadoOuExpirado_DevePublicarCompensacao()
    {
        var orcServiceMock = new Mock<IOrcamentoService>();
        var pagRepoMock = new Mock<IPagamentoRepository>();
        var mpMock = new Mock<IMercadoPagoClient>();
        var pubMock = new Mock<IBillingEventPublisher>();
        var config = new ConfigurationBuilder().Build();

        var orc = new Orcamento(Guid.NewGuid(), Guid.NewGuid(), 200m);
        var pag = Pagamento.CriarBoleto(orc.Id, 200m, orc.Id.ToString());
        pag.AssociarPagamentoMercadoPago("mp-999", "http://boleto", "1234", DateTime.UtcNow.AddDays(3), PagamentoStatus.Pendente, "pending");

        pagRepoMock.Setup(r => r.ObterPorIdMercadoPagoAsync("mp-999")).ReturnsAsync(pag);
        orcServiceMock.Setup(o => o.BuscarOrcamento(orc.Id)).ReturnsAsync(orc);
        mpMock.Setup(m => m.ObterPagamentoAsync("mp-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-999", "rejected", "cc_rejected", "http://boleto", "1234", DateTimeOffset.UtcNow.AddDays(3)));

        var service = new PagamentoService(
            orcServiceMock.Object,
            pagRepoMock.Object,
            mpMock.Object,
            config,
            pubMock.Object,
            NullLogger<PagamentoService>.Instance);

        var resp = await service.AtualizarPorNotificacaoAsync("mp-999", CancellationToken.None);
        resp.Should().NotBeNull();
        resp!.Status.Should().Be("rejeitado");
        pubMock.Verify(p => p.PublishAsync(It.IsAny<PagamentoCanceladoCompensacaoEvent>(), It.IsAny<CancellationToken>()), Times.Once);

        // Expirado e Refunded
        mpMock.Setup(m => m.ObterPagamentoAsync("mp-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-999", "expired", "expired", null, null, null));
        (await service.AtualizarPorNotificacaoAsync("mp-999", CancellationToken.None))!.Status.Should().Be("expirado");

        mpMock.Setup(m => m.ObterPagamentoAsync("mp-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoBoletoResponse("mp-999", "refunded", "refunded", null, null, null));
        (await service.AtualizarPorNotificacaoAsync("mp-999", CancellationToken.None))!.Status.Should().Be("cancelado");
    }

    [Fact]
    public void DomainEntitiesAndExceptions_DevemConstruirCorretamente()
    {
        var status = new StatusOrdemServico("Recebida", "RECEBIDA");
        status.Codigo.Valor.Should().Be("RECEBIDA");
        StatusOS.Recebida.Codigo.Should().Be("RECEBIDA");
        StatusOS.EmDiagnostico.Codigo.Should().Be("EM_DIAGNOSTICO");
        StatusOS.AguardandoAprovacao.Codigo.Should().Be("AGUARDANDO_APROVACAO");
        StatusOS.OrcamentoAprovado.Codigo.Should().Be("ORCAMENTO_APROVADO");
        StatusOS.OrcamentoReprovado.Codigo.Should().Be("ORCAMENTO_REPROVADO");
        StatusOS.EmExecucao.Codigo.Should().Be("EM_EXECUCAO");
        StatusOS.Finalizada.Codigo.Should().Be("FINALIZADA");
        StatusOS.Entregue.Codigo.Should().Be("ENTREGUE");
        StatusOS.Cancelada.Codigo.Should().Be("CANCELADA");

        var id = Guid.NewGuid();
        new ServicoNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new ItemServicoNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new ServicoNomeJaExisteException("Alinhamento").Message.Should().Contain("Alinhamento");
        new ClienteNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new ClienteCpfCnpjJaExisteException("123").Message.Should().Contain("123");
        new VeiculoNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new VeiculoPlacaJaExisteException("ABC1234").Message.Should().Contain("ABC1234");
        new StatusOrdemServicoNaoEncontradoException("X").Message.Should().Contain("X");
        new PecaInsumoNaoEncontradaException(id).Message.Should().Contain(id.ToString());
        new EstoqueNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new ItemPecaInsumoNaoEncontradoException(id).Message.Should().Contain(id.ToString());
        new OrdemServicoNaoEncontradaException(id).Message.Should().Contain(id.ToString());

        var req = new global::Billing.Service.Application.DTOs.Requests.GerarOrcamentoRequest
        {
            Servicos = [new Servico("Troca", "Desc", 100m)],
            PecasInsumo = [new PecaInsumo("Peca", 50m)]
        };
        req.Servicos.Should().ContainSingle();
        req.PecasInsumo.Should().ContainSingle();

        var pagedReq = new PagedRequest { Page = 2, PageSize = 15 };
        var pagedRes = new PagedResult<string>(["a"], pagedReq.Page, pagedReq.PageSize, 45);
        pagedRes.Items.Should().ContainSingle();
        pagedRes.TotalPages.Should().Be(3);
        pagedRes.HasPreviousPage.Should().BeTrue();
        pagedRes.HasNextPage.Should().BeTrue();
    }
}

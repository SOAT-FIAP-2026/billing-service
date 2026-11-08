using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Billing.Service.Application.Interfaces;
using Billing.Service.Domain.Entities;
using Billing.Service.Infrastructure.Data;
using Billing.Service.Infrastructure.Messaging;
using Billing.Service.Infrastructure.Repositories;
using Fiap.TechChallenge.Contracts.Common;
using Fiap.TechChallenge.Contracts.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using SqsMessageAttributeValue = Amazon.SQS.Model.MessageAttributeValue;

namespace Billing.Service.Tests.Infrastructure;

public class BillingMessagingAndRepositoriesTests
{
    private static ApplicationDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task OrcamentoRepository_E_PagamentoRepository_DevemExecutarOperacoesCrudCompletas()
    {
        await using var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var orcRepo = new OrcamentoRepository(db);
        var pagRepo = new PagamentoRepository(db);

        var osId = Guid.NewGuid();
        var orc = new Orcamento(osId, Guid.NewGuid(), 420m);
        await orcRepo.Adicionar(orc);

        (await orcRepo.ObterPorId(orc.Id)).Should().NotBeNull();
        (await orcRepo.ObterPorOrdemServicoId(osId)).Should().NotBeNull();
        (await orcRepo.ObterTodos(1, 10)).Should().ContainSingle();

        orc.AlterarValorTotal(500m);
        await orcRepo.Atualizar(orc);
        (await orcRepo.ObterPorId(orc.Id))!.ValorTotal.Valor.Should().Be(500m);

        var pag = Pagamento.CriarBoleto(orc.Id, 500m, orc.Id.ToString());
        pag.AssociarPagamentoMercadoPago("mp-123", "http://boleto", "9999", DateTime.UtcNow.AddDays(2), PagamentoStatus.Pendente, "pending");
        await pagRepo.AdicionarAsync(pag);

        (await pagRepo.ObterPorIdAsync(pag.Id)).Should().NotBeNull();
        (await pagRepo.ObterPorIdMercadoPagoAsync("mp-123")).Should().NotBeNull();

        pag.AtualizarStatus(PagamentoStatus.Aprovado, "accredited");
        await pagRepo.AtualizarAsync(pag);
        (await pagRepo.ObterPorIdAsync(pag.Id))!.Status.Should().Be(PagamentoStatus.Aprovado);
    }

    [Fact]
    public async Task BillingSnsEventPublisher_PublishAsync_DevePublicarIntegrationEvent_E_ObjetoCustomizado()
    {
        var snsMock = new Mock<IAmazonSimpleNotificationService>();
        var published = new List<PublishRequest>();

        snsMock
            .Setup(s => s.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((r, _) => published.Add(r))
            .ReturnsAsync(new PublishResponse { MessageId = "msg-sns-1" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EVENTOS_TOPIC_ARN"] = "arn:aws:sns:sa-east-1:000000000000:eventos-test"
            })
            .Build();

        var publisher = new BillingSnsEventPublisher(snsMock.Object, config, NullLogger<BillingSnsEventPublisher>.Instance);
        var osId = Guid.NewGuid();

        using (CorrelationContext.Push("corr-billing-pub-1"))
        {
            await publisher.PublishAsync(new OrcamentoGeradoEvent
            {
                OsId = osId,
                OrcamentoId = Guid.NewGuid(),
                ValorTotal = 350m,
                LinkPagamento = "http://checkout"
            });

            await publisher.PublishAsync(new { OsId = osId, CorrelationId = "corr-custom-2" });
        }

        published.Should().HaveCount(2);
        published[0].MessageAttributes[SnsMessageAttributes.CorrelationId].StringValue.Should().Be("corr-billing-pub-1");
        published[1].MessageAttributes[SnsMessageAttributes.CorrelationId].StringValue.Should().Be("corr-custom-2");
    }

    [Fact]
    public async Task BillingSqsBackgroundWorker_DeveProcessarTodosOsTiposDeEventoSqsESnsEnvelope()
    {
        var dbName = Guid.NewGuid().ToString();
        var pubMock = new Mock<IBillingEventPublisher>();

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(pubMock.Object);
        var sp = services.BuildServiceProvider();

        var osDiagFin = Guid.NewGuid();
        var osDiagTec = Guid.NewGuid();
        var osCancel = Guid.NewGuid();
        var osComPag = Guid.NewGuid();

        // Prepara orçamento + pagamento pendente para osCancel e aprovado para osComPag
        using (var seedScope = sp.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orcCancel = new Orcamento(osCancel, Guid.NewGuid(), 190m);
            var pagCancel = Pagamento.CriarBoleto(orcCancel.Id, 190m, orcCancel.Id.ToString());
            await db.Orcamentos.AddAsync(orcCancel);
            await db.Pagamentos.AddAsync(pagCancel);

            var orcComPag = new Orcamento(osComPag, Guid.NewGuid(), 250m);
            var pagTec = Pagamento.CriarBoleto(orcComPag.Id, 250m, orcComPag.Id.ToString());
            pagTec.AtualizarStatus(PagamentoStatus.Aprovado, "approved");
            await db.Orcamentos.AddAsync(orcComPag);
            await db.Pagamentos.AddAsync(pagTec);
            await db.SaveChangesAsync();
        }

        var diagFinEvt = new DiagnosticoFinalizadoEvent(
            osDiagFin,
            [new ItemOrcamentoInfo(Guid.NewGuid(), "SERVICO", "Troca", 1, 280m)],
            280m,
            new ClienteInfo(Guid.NewGuid(), "Cliente", "11999999999", "cli@teste.com"));

        var diagTecEvt = DiagnosticoTecnicoConcluidoEvent.Create(
            osDiagTec,
            "Troca de pastilha",
            [new ServicoSugeridoInfo(Guid.NewGuid(), "Freio")],
            [new PecaSugeridaInfo(Guid.NewGuid(), 2, "Pastilha")],
            30);

        var messages = new List<Message>
        {
            // 1. DiagnosticoFinalizado via SNS Envelope
            new()
            {
                ReceiptHandle = "rh-1",
                Body = JsonSerializer.Serialize(new
                {
                    Type = "Notification",
                    Message = JsonSerializer.Serialize(diagFinEvt),
                    MessageAttributes = new Dictionary<string, object>
                    {
                        [SnsMessageAttributes.TipoEvento] = new { Value = "DiagnosticoFinalizado" },
                        [SnsMessageAttributes.CorrelationId] = new { Value = "corr-1" }
                    }
                })
            },
            // 2. DiagnosticoFinalizado duplicado (idempotência)
            new()
            {
                ReceiptHandle = "rh-1b",
                Body = JsonSerializer.Serialize(diagFinEvt),
                MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = "DiagnosticoFinalizadoEvent" }
                }
            },
            // 3. DiagnosticoTecnicoConcluido
            new()
            {
                ReceiptHandle = "rh-2",
                Body = JsonSerializer.Serialize(diagTecEvt),
                MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = "DiagnosticoTecnicoConcluido" }
                }
            },
            // 4. ProducaoCancelada (sem pagamento prévio -> estorna com valor do orçamento)
            new()
            {
                ReceiptHandle = "rh-3",
                Body = JsonSerializer.Serialize(new ProducaoCanceladaEvent
                {
                    OsId = osDiagFin,
                    Motivo = "Peca avariada"
                }),
                MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = "ProducaoCancelada" }
                }
            },
            // 5. OrdemServicoCancelada + Evento desconhecido
            new()
            {
                ReceiptHandle = "rh-4",
                Body = JsonSerializer.Serialize(new OrdemServicoCanceladaEvent
                {
                    OsId = osCancel,
                    Motivo = "Desistencia"
                }),
                MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = "OrdemServicoCancelada" }
                }
            },
            new()
            {
                ReceiptHandle = "rh-5",
                Body = "{\"tipoEvento\":\"OutroEvento\"}"
            },
            // 6. ProducaoCancelada para osComPag (que possui orçamento + pagamento aprovado)
            new()
            {
                ReceiptHandle = "rh-6",
                Body = JsonSerializer.Serialize(new ProducaoCanceladaEvent
                {
                    OsId = osComPag,
                    Motivo = "Erro bancada"
                }),
                MessageAttributes = new Dictionary<string, SqsMessageAttributeValue>
                {
                    [SnsMessageAttributes.TipoEvento] = new() { StringValue = "ProducaoCanceladaEvent" }
                }
            }
        };

        using var cts = new CancellationTokenSource();
        var sqsMock = new Mock<IAmazonSQS>();
        int callCount = 0;

        sqsMock
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ReceiveMessageResponse { Messages = messages };
                }

                cts.Cancel();
                return new ReceiveMessageResponse { Messages = [] };
            });

        sqsMock
            .Setup(s => s.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        var config = new ConfigurationBuilder().Build();
        var worker = new BillingSqsBackgroundWorker(
            sqsMock.Object,
            config,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BillingSqsBackgroundWorker>.Instance);

        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);

        pubMock.Verify(p => p.PublishAsync(It.IsAny<OrcamentoGeradoEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        pubMock.Verify(p => p.PublishAsync(It.IsAny<PagamentoEstornadoEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}

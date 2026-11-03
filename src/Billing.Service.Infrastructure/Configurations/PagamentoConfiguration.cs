using Billing.Service.Domain.Entities;
using Billing.Service.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.Service.Infrastructure.Configurations;

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamento");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdOrcamento).HasColumnName("id_orcamento").IsRequired();
        builder.Property(x => x.IdMercadoPago).HasColumnName("id_mercado_pago").HasMaxLength(100);
        builder.Property(x => x.MeioPagamento).HasColumnName("meio_pagamento").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DetalheStatus).HasColumnName("detalhe_status").HasMaxLength(100);
        builder.Property(x => x.Valor).HasConversion(x => x.Valor, x => new ValorMonetarioVO(x))
            .HasColumnName("valor").HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.ReferenciaExterna).HasColumnName("referencia_externa").HasMaxLength(100).IsRequired();
        builder.Property(x => x.BoletoUrl).HasColumnName("boleto_url");
        builder.Property(x => x.CodigoBarras).HasColumnName("codigo_barras");
        builder.Property(x => x.DataVencimento).HasColumnName("data_vencimento");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(x => x.ApagadoEm).HasColumnName("apagado_em");
        builder.HasIndex(x => x.IdMercadoPago).IsUnique();
        builder.HasIndex(x => x.IdOrcamento);
        builder.HasQueryFilter(x => x.ApagadoEm == null);
    }
}

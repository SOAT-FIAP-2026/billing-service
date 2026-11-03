using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPagamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pagamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_orcamento = table.Column<Guid>(type: "uuid", nullable: false),
                    id_mercado_pago = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    meio_pagamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detalhe_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    referencia_externa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    boleto_url = table.Column<string>(type: "text", nullable: true),
                    codigo_barras = table.Column<string>(type: "text", nullable: true),
                    data_vencimento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    apagado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamento_id_mercado_pago",
                table: "pagamento",
                column: "id_mercado_pago",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pagamento_id_orcamento",
                table: "pagamento",
                column: "id_orcamento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamento");
        }
    }
}

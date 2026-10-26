using Microsoft.EntityFrameworkCore.Migrations;

namespace Billing.Service.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orcamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_ordem_servico = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    apagado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orcamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_orcamento_ordem_servico_id_ordem_servico",
                        column: x => x.id_ordem_servico,
                        principalTable: "ordem_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orcamento_id_ordem_servico",
                table: "orcamento",
                column: "id_ordem_servico",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orcamento");
        }
    }
}

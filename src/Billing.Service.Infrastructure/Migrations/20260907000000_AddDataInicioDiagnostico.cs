using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Billing.Service.Infrastructure.Data;

#nullable disable

namespace Billing.Service.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260907000000_AddDataInicioDiagnostico")]
    public partial class AddDataInicioDiagnostico : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "data_inicio_diagnostico",
                table: "ordem_servico",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_inicio_diagnostico",
                table: "ordem_servico");
        }
    }
}

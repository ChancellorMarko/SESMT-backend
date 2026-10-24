using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SESMT.Infrastructure.Persistence;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SesmtDbContext))]
[Migration("20261023190000_PermanentWorkPermitAppointmentUniqueness")]
public sealed class PermanentWorkPermitAppointmentUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ux_permissao_trabalho_agendamento", schema: "core", table: "permissao_trabalho");
        migrationBuilder.CreateIndex(name: "ux_permissao_trabalho_agendamento", schema: "core", table: "permissao_trabalho", column: "agendamento_id", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ux_permissao_trabalho_agendamento", schema: "core", table: "permissao_trabalho");
        migrationBuilder.CreateIndex(name: "ux_permissao_trabalho_agendamento", schema: "core", table: "permissao_trabalho", column: "agendamento_id", unique: true, filter: "excluido_em IS NULL");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chave_idempotencia",
                schema: "core",
                table: "agendamento_pt",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "impressao_idempotencia",
                schema: "core",
                table: "agendamento_pt",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_agendamento_pt_solicitante_chave",
                schema: "core",
                table: "agendamento_pt",
                columns: new[] { "chave_idempotencia", "solicitado_por" },
                unique: true,
                filter: "chave_idempotencia IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_agendamento_pt_solicitante_chave",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "chave_idempotencia",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "impressao_idempotencia",
                schema: "core",
                table: "agendamento_pt");
        }
    }
}

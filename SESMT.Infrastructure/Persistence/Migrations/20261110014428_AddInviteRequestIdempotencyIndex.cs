using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteRequestIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_agendamento_pt_convite_chave",
                schema: "core",
                table: "agendamento_pt",
                columns: new[] { "chave_idempotencia", "convite_id" },
                unique: true,
                filter: "chave_idempotencia IS NOT NULL AND convite_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_agendamento_pt_convite_chave",
                schema: "core",
                table: "agendamento_pt");
        }
    }
}

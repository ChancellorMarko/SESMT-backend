using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPermits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissao_trabalho",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    liberada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    liberada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    iniciada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    suspensa_por = table.Column<Guid>(type: "uuid", nullable: true),
                    suspensa_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_suspensao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concluida_por = table.Column<Guid>(type: "uuid", nullable: true),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_permissao_trabalho_empresa_status",
                schema: "core",
                table: "permissao_trabalho",
                columns: new[] { "empresa_id", "status" },
                filter: "excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permissao_trabalho",
                schema: "core");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPermitHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissao_trabalho_historico",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_trabalho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status_novo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho_historico", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_historico_permissao_trabalho_permissao_t~",
                        column: x => x.permissao_trabalho_id,
                        principalSchema: "core",
                        principalTable: "permissao_trabalho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_historico_versao",
                schema: "core",
                table: "permissao_trabalho_historico",
                columns: new[] { "permissao_trabalho_id", "versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permissao_trabalho_historico",
                schema: "core");
        }
    }
}

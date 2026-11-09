using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "epi",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ca = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fabricante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_epi", x => x.id);
                    table.ForeignKey(
                        name: "FK_epi_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "epi_entrega",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabalhador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    epi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comando_id = table.Column<Guid>(type: "uuid", nullable: false),
                    epi_nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    epi_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ca = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    tamanho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    entregue_em = table.Column<DateOnly>(type: "date", nullable: false),
                    troca_prevista_em = table.Column<DateOnly>(type: "date", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    devolvido_em = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_devolucao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_epi_entrega", x => x.id);
                    table.CheckConstraint("ck_epi_entrega_quantidade", "quantidade BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_epi_entrega_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_epi_entrega_epi_epi_id",
                        column: x => x.epi_id,
                        principalSchema: "core",
                        principalTable: "epi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_epi_entrega_trabalhador_trabalhador_id",
                        column: x => x.trabalhador_id,
                        principalSchema: "core",
                        principalTable: "trabalhador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_epi_entrega_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "core",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_epi_empresa_codigo",
                schema: "core",
                table: "epi",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_epi_entrega_empresa_id_comando_id",
                schema: "core",
                table: "epi_entrega",
                columns: new[] { "empresa_id", "comando_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_epi_entrega_epi_id",
                schema: "core",
                table: "epi_entrega",
                column: "epi_id");

            migrationBuilder.CreateIndex(
                name: "IX_epi_entrega_trabalhador_id",
                schema: "core",
                table: "epi_entrega",
                column: "trabalhador_id");

            migrationBuilder.CreateIndex(
                name: "IX_epi_entrega_unidade_id_trabalhador_id_entregue_em",
                schema: "core",
                table: "epi_entrega",
                columns: new[] { "unidade_id", "trabalhador_id", "entregue_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "epi_entrega",
                schema: "core");

            migrationBuilder.DropTable(
                name: "epi",
                schema: "core");
        }
    }
}

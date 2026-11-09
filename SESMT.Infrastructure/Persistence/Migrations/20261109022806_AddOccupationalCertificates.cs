using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOccupationalCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "saude");

            migrationBuilder.CreateTable(
                name: "aso_registro",
                schema: "saude",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabalhador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comando_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_exame = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    exame_em = table.Column<DateOnly>(type: "date", nullable: false),
                    emitido_em = table.Column<DateOnly>(type: "date", nullable: false),
                    proxima_avaliacao_em = table.Column<DateOnly>(type: "date", nullable: true),
                    dados_protegidos = table.Column<string>(type: "text", nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false),
                    invalidado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_invalidacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_aso_registro", x => x.id);
                    table.CheckConstraint("ck_aso_datas", "emitido_em >= exame_em AND (proxima_avaliacao_em IS NULL OR proxima_avaliacao_em >= exame_em)");
                    table.CheckConstraint("ck_aso_tipo", "tipo_exame IN ('ADMISSION','PERIODIC','RETURN_TO_WORK','RISK_CHANGE','DISMISSAL')");
                    table.ForeignKey(
                        name: "FK_aso_registro_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aso_registro_trabalhador_trabalhador_id",
                        column: x => x.trabalhador_id,
                        principalSchema: "core",
                        principalTable: "trabalhador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aso_registro_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "core",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aso_registro_empresa_id_comando_id",
                schema: "saude",
                table: "aso_registro",
                columns: new[] { "empresa_id", "comando_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aso_registro_trabalhador_id",
                schema: "saude",
                table: "aso_registro",
                column: "trabalhador_id");

            migrationBuilder.CreateIndex(
                name: "IX_aso_registro_unidade_id_trabalhador_id_exame_em",
                schema: "saude",
                table: "aso_registro",
                columns: new[] { "unidade_id", "trabalhador_id", "exame_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aso_registro",
                schema: "saude");
        }
    }
}

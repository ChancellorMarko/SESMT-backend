using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPermitTemplateVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "checklist_respostas_json",
                schema: "core",
                table: "permissao_trabalho",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "template_versoes_json",
                schema: "core",
                table: "permissao_trabalho",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "pt_template_versao",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    servicos_json = table.Column<string>(type: "jsonb", nullable: false),
                    perguntas_json = table.Column<string>(type: "jsonb", nullable: false),
                    declaracao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ilustrativo = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_pt_template_versao", x => x.id);
                    table.ForeignKey(
                        name: "FK_pt_template_versao_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pt_template_versao_empresa_id_template_id",
                schema: "core",
                table: "pt_template_versao",
                columns: new[] { "empresa_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pt_template_versao",
                schema: "core",
                table: "pt_template_versao",
                columns: new[] { "template_id", "versao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pt_template_versao",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "checklist_respostas_json",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "template_versoes_json",
                schema: "core",
                table: "permissao_trabalho");

        }
    }
}

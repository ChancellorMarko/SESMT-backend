using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPermitWorkflowIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO core.perfil (id, codigo, nome, descricao, ativo, criado_em)
                VALUES
                    ('eea40c89-79f5-4ee9-9d35-31bcaa7d2f01', 'SESMT_PT_SOLICITANTE', 'Solicitante de PT', 'Pode abrir agendamentos e permissões de trabalho na empresa atribuída.', TRUE, NOW()),
                    ('eea40c89-79f5-4ee9-9d35-31bcaa7d2f02', 'SESMT_PT_OPERADOR', 'Operador de PT', 'Pode iniciar, suspender, retomar e concluir permissões de trabalho na empresa atribuída.', TRUE, NOW()),
                    ('eea40c89-79f5-4ee9-9d35-31bcaa7d2f03', 'SESMT_PT_LIBERADOR', 'Liberador de PT', 'Pode liberar permissões de trabalho na empresa atribuída.', TRUE, NOW()),
                    ('eea40c89-79f5-4ee9-9d35-31bcaa7d2f04', 'SESMT_PT_CONFIG', 'Configurador de PT', 'Pode configurar requisitos documentais na empresa atribuída.', TRUE, NOW())
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "agendamento_id",
                schema: "core",
                table: "permissao_trabalho",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "conteudo_versao",
                schema: "core",
                table: "permissao_trabalho",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "servico_codigo",
                schema: "core",
                table: "permissao_trabalho",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "agendamento_pt",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inicio_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fim_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    solicitado_por = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_agendamento_pt", x => x.id);
                    table.ForeignKey(
                        name: "FK_agendamento_pt_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agendamento_pt_usuario_solicitado_por",
                        column: x => x.solicitado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho_participante",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_trabalho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsabilidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    atribuido_por = table.Column<Guid>(type: "uuid", nullable: false),
                    ciente_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ciente_versao = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_permissao_trabalho_participante", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_permissao_trabalho_permissa~",
                        column: x => x.permissao_trabalho_id,
                        principalSchema: "core",
                        principalTable: "permissao_trabalho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_usuario_atribuido_por",
                        column: x => x.atribuido_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho_requisito_documental",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo_documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    configurado_por = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_permissao_trabalho_requisito_documental", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_documento_tipo_tipo~",
                        column: x => x.tipo_documento_id,
                        principalSchema: "docs",
                        principalTable: "documento_tipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_usuario_configurado~",
                        column: x => x.configurado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_agendamento",
                schema: "core",
                table: "permissao_trabalho",
                column: "agendamento_id",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_pt_empresa_inicio",
                schema: "core",
                table: "agendamento_pt",
                columns: new[] { "empresa_id", "inicio_em" },
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_pt_solicitado_por",
                schema: "core",
                table: "agendamento_pt",
                column: "solicitado_por");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_participante_atribuido_por",
                schema: "core",
                table: "permissao_trabalho_participante",
                column: "atribuido_por");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_participante_usuario_id",
                schema: "core",
                table: "permissao_trabalho_participante",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_participante",
                schema: "core",
                table: "permissao_trabalho_participante",
                columns: new[] { "permissao_trabalho_id", "usuario_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_requisito_documental_configurado_por",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                column: "configurado_por");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_requisito_documental_tipo_documento_id",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                column: "tipo_documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_pt_requisito_servico_tipo_doc",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                columns: new[] { "empresa_id", "servico_codigo", "tipo_documento_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_permissao_trabalho_agendamento_pt_agendamento_id",
                schema: "core",
                table: "permissao_trabalho",
                column: "agendamento_id",
                principalSchema: "core",
                principalTable: "agendamento_pt",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM core.perfil AS p
                WHERE p.id IN (
                    'eea40c89-79f5-4ee9-9d35-31bcaa7d2f01',
                    'eea40c89-79f5-4ee9-9d35-31bcaa7d2f02',
                    'eea40c89-79f5-4ee9-9d35-31bcaa7d2f03',
                    'eea40c89-79f5-4ee9-9d35-31bcaa7d2f04')
                  AND NOT EXISTS (SELECT 1 FROM core.usuario_perfil assignment WHERE assignment.perfil_id = p.id)
                  AND NOT EXISTS (SELECT 1 FROM core.perfil_permissao assigned_permission WHERE assigned_permission.perfil_id = p.id);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_permissao_trabalho_agendamento_pt_agendamento_id",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropTable(
                name: "agendamento_pt",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho_participante",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho_requisito_documental",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "ux_permissao_trabalho_agendamento",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "agendamento_id",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "conteudo_versao",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "servico_codigo",
                schema: "core",
                table: "permissao_trabalho");
        }
    }
}

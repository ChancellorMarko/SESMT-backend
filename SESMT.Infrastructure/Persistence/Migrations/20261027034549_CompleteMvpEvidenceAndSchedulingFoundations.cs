using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMvpEvidenceAndSchedulingFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encerramento_perguntas_json",
                schema: "core",
                table: "pt_template_versao",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "ciencias_json",
                schema: "core",
                table: "permissao_trabalho_participante",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "condicoes_liberacao",
                schema: "core",
                table: "permissao_trabalho",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encerramento_respostas_json",
                schema: "core",
                table: "permissao_trabalho",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fim_autorizado",
                schema: "core",
                table: "permissao_trabalho",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "inicio_autorizado",
                schema: "core",
                table: "permissao_trabalho",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacoes_json",
                schema: "core",
                table: "permissao_trabalho",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "resumo_realizado",
                schema: "core",
                table: "permissao_trabalho",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_revisao",
                schema: "core",
                table: "permissao_trabalho",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NAO_ENVIADA");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "termino_efetivo",
                schema: "core",
                table: "permissao_trabalho",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revisao",
                schema: "docs",
                table: "documento",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "versao_evidencia_atual",
                schema: "docs",
                table: "documento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "solicitado_por",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "convite_id",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "equipe_id",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "historico",
                schema: "core",
                table: "agendamento_pt",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<Guid[]>(
                name: "participantes_ids",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::uuid[]");

            migrationBuilder.AddColumn<Guid>(
                name: "responsavel_id",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "servicos",
                schema: "core",
                table: "agendamento_pt",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "situacao",
                schema: "core",
                table: "agendamento_pt",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "REQUESTED");

            migrationBuilder.AddColumn<int>(
                name: "versao",
                schema: "core",
                table: "agendamento_pt",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "convite_agendamento",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    revogado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_convite_agendamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_convite_agendamento_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_convite_agendamento_equipe_equipe_id",
                        column: x => x.equipe_id,
                        principalSchema: "core",
                        principalTable: "equipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_convite_agendamento_unidade_local_id",
                        column: x => x.local_id,
                        principalSchema: "core",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_convite_agendamento_usuario_criado_por",
                        column: x => x.criado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_convite_agendamento_usuario_revogado_por",
                        column: x => x.revogado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_concessao",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concedido_por = table.Column<Guid>(type: "uuid", nullable: false),
                    pode_conferir = table.Column<bool>(type: "boolean", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_concessao", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_concessao_documento_documento_id",
                        column: x => x.documento_id,
                        principalSchema: "docs",
                        principalTable: "documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_pt_convite_id",
                schema: "core",
                table: "agendamento_pt",
                column: "convite_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_pt_equipe_id",
                schema: "core",
                table: "agendamento_pt",
                column: "equipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_pt_responsavel_id",
                schema: "core",
                table: "agendamento_pt",
                column: "responsavel_id");

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_criado_por",
                schema: "core",
                table: "convite_agendamento",
                column: "criado_por");

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_empresa_id_equipe_id_expira_em",
                schema: "core",
                table: "convite_agendamento",
                columns: new[] { "empresa_id", "equipe_id", "expira_em" });

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_equipe_id",
                schema: "core",
                table: "convite_agendamento",
                column: "equipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_local_id",
                schema: "core",
                table: "convite_agendamento",
                column: "local_id");

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_revogado_por",
                schema: "core",
                table: "convite_agendamento",
                column: "revogado_por");

            migrationBuilder.CreateIndex(
                name: "IX_convite_agendamento_token_hash",
                schema: "core",
                table: "convite_agendamento",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_concessao_documento_id_usuario_id_expira_em",
                schema: "docs",
                table: "documento_concessao",
                columns: new[] { "documento_id", "usuario_id", "expira_em" });

            migrationBuilder.AddForeignKey(
                name: "FK_agendamento_pt_convite_agendamento_convite_id",
                schema: "core",
                table: "agendamento_pt",
                column: "convite_id",
                principalSchema: "core",
                principalTable: "convite_agendamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agendamento_pt_equipe_equipe_id",
                schema: "core",
                table: "agendamento_pt",
                column: "equipe_id",
                principalSchema: "core",
                principalTable: "equipe",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agendamento_pt_usuario_responsavel_id",
                schema: "core",
                table: "agendamento_pt",
                column: "responsavel_id",
                principalSchema: "core",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agendamento_pt_convite_agendamento_convite_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropForeignKey(
                name: "FK_agendamento_pt_equipe_equipe_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropForeignKey(
                name: "FK_agendamento_pt_usuario_responsavel_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropTable(
                name: "convite_agendamento",
                schema: "core");

            migrationBuilder.DropTable(
                name: "documento_concessao",
                schema: "docs");

            migrationBuilder.DropIndex(
                name: "IX_agendamento_pt_convite_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropIndex(
                name: "IX_agendamento_pt_equipe_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropIndex(
                name: "IX_agendamento_pt_responsavel_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "encerramento_perguntas_json",
                schema: "core",
                table: "pt_template_versao");

            migrationBuilder.DropColumn(
                name: "ciencias_json",
                schema: "core",
                table: "permissao_trabalho_participante");

            migrationBuilder.DropColumn(
                name: "condicoes_liberacao",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "encerramento_respostas_json",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "fim_autorizado",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "inicio_autorizado",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "observacoes_json",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "resumo_realizado",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "status_revisao",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "termino_efetivo",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "revisao",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropColumn(
                name: "versao_evidencia_atual",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropColumn(
                name: "convite_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "equipe_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "historico",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "participantes_ids",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "responsavel_id",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "servicos",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "situacao",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.DropColumn(
                name: "versao",
                schema: "core",
                table: "agendamento_pt");

            migrationBuilder.AlterColumn<Guid>(
                name: "solicitado_por",
                schema: "core",
                table: "agendamento_pt",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

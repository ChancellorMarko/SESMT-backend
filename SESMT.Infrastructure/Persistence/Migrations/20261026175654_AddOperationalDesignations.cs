using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations;

public partial class AddOperationalDesignations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "local_id",
            schema: "core",
            table: "agendamento_pt",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_agendamento_pt_local_id",
            schema: "core",
            table: "agendamento_pt",
            column: "local_id");

        migrationBuilder.AddForeignKey(
            name: "FK_agendamento_pt_unidade_local_id",
            schema: "core",
            table: "agendamento_pt",
            column: "local_id",
            principalSchema: "core",
            principalTable: "unidade",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "designacao_operacional",
            schema: "core",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                pessoa_id = table.Column<Guid>(type: "uuid", nullable: false),
                servico_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                local_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                tipo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                inicio_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                fim_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                origem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                atribuido_por = table.Column<Guid>(type: "uuid", nullable: false),
                revogado_por = table.Column<Guid>(type: "uuid", nullable: true),
                revogado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                table.PrimaryKey("PK_designacao_operacional", x => x.id);
                table.ForeignKey(
                    name: "FK_designacao_operacional_empresa_empresa_id",
                    column: x => x.empresa_id,
                    principalSchema: "core",
                    principalTable: "empresa",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_designacao_operacional_trabalhador_pessoa_id",
                    column: x => x.pessoa_id,
                    principalSchema: "core",
                    principalTable: "trabalhador",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_designacao_operacional_usuario_atribuido_por",
                    column: x => x.atribuido_por,
                    principalSchema: "core",
                    principalTable: "usuario",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_designacao_operacional_usuario_revogado_por",
                    column: x => x.revogado_por,
                    principalSchema: "core",
                    principalTable: "usuario",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_designacao_operacional_atribuido_por", "designacao_operacional", "atribuido_por", schema: "core");
        migrationBuilder.CreateIndex("IX_designacao_operacional_pessoa_id", "designacao_operacional", "pessoa_id", schema: "core");
        migrationBuilder.CreateIndex("IX_designacao_operacional_revogado_por", "designacao_operacional", "revogado_por", schema: "core");
        migrationBuilder.CreateIndex("ix_designacao_empresa_pessoa_tipo", "designacao_operacional", new[] { "empresa_id", "pessoa_id", "tipo" }, schema: "core");
        migrationBuilder.CreateIndex("ix_designacao_vigencia", "designacao_operacional", new[] { "ativo", "inicio_em", "fim_em" }, schema: "core");
        migrationBuilder.CreateIndex("ux_designacao_comando_idempotente", "designacao_operacional",
            new[] { "empresa_id", "pessoa_id", "tipo", "servico_ids", "local_ids", "inicio_em", "fim_em", "origem" },
            schema: "core", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "designacao_operacional", schema: "core");

        migrationBuilder.DropForeignKey(
            name: "FK_agendamento_pt_unidade_local_id",
            schema: "core",
            table: "agendamento_pt");
        migrationBuilder.DropIndex(name: "IX_agendamento_pt_local_id", schema: "core", table: "agendamento_pt");
        migrationBuilder.DropColumn(name: "local_id", schema: "core", table: "agendamento_pt");
    }
}

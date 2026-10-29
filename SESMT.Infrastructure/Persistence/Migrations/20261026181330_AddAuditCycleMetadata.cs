using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCycleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "declarante_id",
                schema: "audit",
                table: "log_auditoria",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                schema: "audit",
                table: "log_auditoria",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evento_id",
                schema: "audit",
                table: "log_auditoria",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<int>(
                name: "recurso_versao",
                schema: "audit",
                table: "log_auditoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resultado",
                schema: "audit",
                table: "log_auditoria",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                schema: "audit",
                table: "log_auditoria",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_empresa_ocorrido_em",
                schema: "audit",
                table: "log_auditoria",
                columns: new[] { "empresa_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ux_auditoria_evento_id",
                schema: "audit",
                table: "log_auditoria",
                column: "evento_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_auditoria_empresa_ocorrido_em",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropIndex(
                name: "ux_auditoria_evento_id",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "declarante_id",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "evento_id",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "recurso_versao",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "resultado",
                schema: "audit",
                table: "log_auditoria");

            migrationBuilder.DropColumn(
                name: "trace_id",
                schema: "audit",
                table: "log_auditoria");
        }
    }
}

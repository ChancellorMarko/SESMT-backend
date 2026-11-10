using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPermitFinalReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encerramentos_anteriores_json",
                schema: "core",
                table: "permissao_trabalho",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "parecer_revisao",
                schema: "core",
                table: "permissao_trabalho",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revisado_em",
                schema: "core",
                table: "permissao_trabalho",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revisado_por",
                schema: "core",
                table: "permissao_trabalho",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encerramentos_anteriores_json",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "parecer_revisao",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "revisado_em",
                schema: "core",
                table: "permissao_trabalho");

            migrationBuilder.DropColumn(
                name: "revisado_por",
                schema: "core",
                table: "permissao_trabalho");
        }
    }
}

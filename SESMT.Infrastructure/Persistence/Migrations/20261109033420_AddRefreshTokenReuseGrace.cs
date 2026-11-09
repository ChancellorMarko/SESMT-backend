using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenReuseGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refresh_token_anterior_hash",
                schema: "core",
                table: "usuario_sessao",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rotacionado_em",
                schema: "core",
                table: "usuario_sessao",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_sessao_refresh_anterior_hash",
                schema: "core",
                table: "usuario_sessao",
                column: "refresh_token_anterior_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_usuario_sessao_refresh_anterior_hash",
                schema: "core",
                table: "usuario_sessao");

            migrationBuilder.DropColumn(
                name: "refresh_token_anterior_hash",
                schema: "core",
                table: "usuario_sessao");

            migrationBuilder.DropColumn(
                name: "rotacionado_em",
                schema: "core",
                table: "usuario_sessao");
        }
    }
}

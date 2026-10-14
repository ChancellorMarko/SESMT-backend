using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario_token_redefinicao_senha",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    utilizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_usuario_token_redefinicao_senha", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_token_redefinicao_senha_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_token_redefinicao_usuario",
                schema: "core",
                table: "usuario_token_redefinicao_senha",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_token_redefinicao_hash",
                schema: "core",
                table: "usuario_token_redefinicao_senha",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_token_redefinicao_senha",
                schema: "core");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignIdentityAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "usuario",
                newName: "atualizado_por");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "core",
                table: "usuario",
                newName: "excluido_por");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "core",
                table: "usuario",
                newName: "criado_por");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "perfil_permissao",
                newName: "atualizado_por");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "core",
                table: "perfil_permissao",
                newName: "excluido_por");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "core",
                table: "perfil_permissao",
                newName: "criado_por");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                schema: "core",
                table: "perfil",
                newName: "atualizado_por");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "core",
                table: "perfil",
                newName: "excluido_por");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                schema: "core",
                table: "perfil",
                newName: "criado_por");

            migrationBuilder.RenameColumn(
                name: "Active",
                schema: "core",
                table: "perfil",
                newName: "ativo");

            migrationBuilder.AlterColumn<bool>(
                name: "ativo",
                schema: "core",
                table: "perfil",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "excluido_por",
                schema: "core",
                table: "usuario",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "criado_por",
                schema: "core",
                table: "usuario",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "atualizado_por",
                schema: "core",
                table: "usuario",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "excluido_por",
                schema: "core",
                table: "perfil_permissao",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "criado_por",
                schema: "core",
                table: "perfil_permissao",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "atualizado_por",
                schema: "core",
                table: "perfil_permissao",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "excluido_por",
                schema: "core",
                table: "perfil",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "criado_por",
                schema: "core",
                table: "perfil",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "atualizado_por",
                schema: "core",
                table: "perfil",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "ativo",
                schema: "core",
                table: "perfil",
                newName: "Active");

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                schema: "core",
                table: "perfil",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveAuditHashCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hash_versao",
                schema: "audit",
                table: "log_auditoria",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hash_versao",
                schema: "audit",
                table: "log_auditoria");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEventDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "evento_origem_id",
                schema: "core",
                table: "alerta",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE core.alerta SET evento_origem_id = gen_random_uuid() WHERE evento_origem_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "evento_origem_id",
                schema: "core",
                table: "alerta",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_alerta_evento_destinatario",
                schema: "core",
                table: "alerta",
                columns: new[] { "evento_origem_id", "destinatario_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_alerta_evento_destinatario",
                schema: "core",
                table: "alerta");

            migrationBuilder.DropColumn(
                name: "evento_origem_id",
                schema: "core",
                table: "alerta");
        }
    }
}

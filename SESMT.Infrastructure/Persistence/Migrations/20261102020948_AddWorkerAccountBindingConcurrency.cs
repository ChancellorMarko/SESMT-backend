using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerAccountBindingConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Never choose an identity or delete historical links automatically.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT usuario_id FROM core.trabalhador
                        WHERE usuario_id IS NOT NULL AND excluido_em IS NULL
                        GROUP BY usuario_id HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Existem contas vinculadas a mais de um trabalhador. Revise os vinculos administrativamente antes de aplicar a migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<int>(
                name: "revisao",
                schema: "core",
                table: "trabalhador",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ux_trabalhador_usuario_vinculado",
                schema: "core",
                table: "trabalhador",
                column: "usuario_id",
                unique: true,
                filter: "usuario_id IS NOT NULL AND excluido_em IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_trabalhador_usuario_vinculado",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropColumn(
                name: "revisao",
                schema: "core",
                table: "trabalhador");
        }
    }
}

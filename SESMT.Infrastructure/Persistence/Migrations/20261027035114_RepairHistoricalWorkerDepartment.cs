using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairHistoricalWorkerDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The historical snapshot already mapped this nullable field, but no
            // previous migration created it. Preserve databases repaired manually.
            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD COLUMN IF NOT EXISTS setor_id uuid NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the column: it may predate this migration in existing databases.
            // Removing it could destroy manually maintained department assignments.
        }
    }
}

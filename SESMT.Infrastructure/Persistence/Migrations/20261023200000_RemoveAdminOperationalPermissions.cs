using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SesmtDbContext))]
[Migration("20261023200000_RemoveAdminOperationalPermissions")]
public sealed class RemoveAdminOperationalPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE core.perfil_permissao rp
        SET ativo = FALSE, excluido_em = NOW()
        FROM core.perfil r, core.permissao p
        WHERE rp.perfil_id = r.id
          AND rp.permissao_id = p.id
          AND r.codigo = 'ADMIN'
          AND rp.ativo = TRUE
          AND rp.excluido_em IS NULL
          AND (p.recurso = 'aso' OR (p.recurso = 'pt' AND p.acao IN ('liberar', 'iniciar', 'concluir')));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO core.perfil_permissao
            (id, perfil_id, permissao_id, ativo, criado_em)
        SELECT gen_random_uuid(), r.id, p.id, TRUE, NOW()
        FROM core.perfil r
        CROSS JOIN core.permissao p
        WHERE r.codigo = 'ADMIN'
          AND (p.recurso = 'aso' OR (p.recurso = 'pt' AND p.acao IN ('liberar', 'iniciar', 'concluir')))
          AND NOT EXISTS (
              SELECT 1 FROM core.perfil_permissao rp
              WHERE rp.perfil_id = r.id AND rp.permissao_id = p.id AND rp.excluido_em IS NULL
          );
        """);
}

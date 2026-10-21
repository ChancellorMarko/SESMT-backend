using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.Infrastructure.Persistence.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SesmtDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<SeedSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseSeeder));

        try
        {
            await SeedRolesAndPermissionsAsync(db, logger, cancellationToken);
            await SeedAdminUserAsync(db, passwordHasher, settings, logger, cancellationToken);

            logger.LogInformation("Seed concluído com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao executar o seed.");
            throw;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. Perfis + Permissões + Vínculos
    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedRolesAndPermissionsAsync(
        SesmtDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        // ═══ PERMISSÕES BASE ════════════════════════════════════════════════
        var permissions = new (string Resource, string Action, string Description)[]
        {
            // Documentos
            ("documento", "ler",      "Consultar documentos"),
            ("documento", "criar",    "Criar documentos"),
            ("documento", "editar",   "Editar documentos"),
            ("documento", "excluir",  "Excluir documentos"),
            ("documento", "aprovar",  "Aprovar documentos"),
            ("documento", "assinar",  "Assinar documentos"),
            ("documento", "exportar", "Exportar pacote de auditoria"),

            // Empresa
            ("empresa", "ler",     "Consultar empresas"),
            ("empresa", "criar",   "Criar empresas"),
            ("empresa", "editar",  "Editar empresas"),
            ("empresa", "excluir", "Desativar empresas"),

            // Unidade
            ("unidade", "ler",     "Consultar unidades"),
            ("unidade", "criar",   "Criar unidades"),
            ("unidade", "editar",  "Editar unidades"),
            ("unidade", "excluir", "Desativar unidades"),

            // Setor
            ("setor", "ler",     "Consultar setores"),
            ("setor", "criar",   "Criar setores"),
            ("setor", "editar",  "Editar setores"),
            ("setor", "excluir", "Desativar setores"),

            // Cargo
            ("cargo", "ler",     "Consultar cargos"),
            ("cargo", "criar",   "Criar cargos"),
            ("cargo", "editar",  "Editar cargos"),
            ("cargo", "excluir", "Desativar cargos"),

            // Trabalhador
            ("trabalhador", "ler",     "Consultar trabalhadores"),
            ("trabalhador", "criar",   "Criar trabalhadores"),
            ("trabalhador", "editar",  "Editar trabalhadores"),
            ("trabalhador", "excluir", "Desativar trabalhadores"),
            ("trabalhador", "importar","Importar trabalhadores via CSV"),

            // Equipe
            ("equipe", "ler",     "Consultar equipes"),
            ("equipe", "criar",   "Criar equipes"),
            ("equipe", "editar",  "Editar equipes"),
            ("equipe", "excluir", "Desativar equipes"),

            // Alertas
            ("alerta", "ler",    "Consultar alertas"),
            ("alerta", "tratar", "Marcar alerta como tratado"),

            // Auditoria
            ("auditoria", "ler", "Consultar logs de auditoria"),

            // Parâmetros
            ("parametro", "ler",   "Consultar parâmetros"),
            ("parametro", "editar","Editar parâmetros"),

            // Perfis
            ("perfil", "ler",     "Consultar perfis"),
            ("perfil", "criar",   "Criar perfis"),
            ("perfil", "editar",  "Editar perfis"),
            ("perfil", "excluir", "Desativar perfis"),

            // Usuários
            ("usuario", "ler",     "Consultar usuários"),
            ("usuario", "criar",   "Criar usuários"),
            ("usuario", "editar",  "Editar usuários"),
            ("usuario", "excluir", "Desativar usuários"),

            // ASO / dados clínicos
            ("aso", "ler",   "Consultar ASO (dados sensíveis)"),
            ("aso", "editar","Registrar ASO (dados sensíveis)"),

            // PT / APR
            ("pt", "ler",     "Consultar Permissão de Trabalho"),
            ("pt", "criar",   "Criar PT"),
            ("pt", "editar",  "Editar PT"),
            ("pt", "excluir", "Cancelar PT"),
            ("pt", "liberar", "Liberar PT"),
            ("pt", "iniciar", "Iniciar PT"),
            ("pt", "concluir", "Concluir PT"),
        };

        var existingPermissions = await db.Permissions
            .Where(p => p.DeletedAt == null)
            .ToListAsync(ct);

        var newPermissions = new List<Permission>();

        foreach (var (resource, action, description) in permissions)
        {
            var exists = existingPermissions.Any(p =>
                p.Resource == resource && p.Action == action);

            if (!exists)
            {
                newPermissions.Add(new Permission(resource, action, description));
            }
        }

        if (newPermissions.Count > 0)
        {
            await db.Permissions.AddRangeAsync(newPermissions, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed: {Count} permissões criadas.", newPermissions.Count);
        }

        // ═══ PERFIS PADRÃO ═════════════════════════════════════════════════
        var roles = new (string Code, string Name, string Description)[]
        {
            ("ADMIN",       "Administrador",      "Configura o sistema, perfis e parâmetros."),
            ("GESTOR",      "Gestor SESMT",       "Visão global, aprovações e indicadores."),
            ("TECNICO",     "Técnico/Engenheiro", "Riscos, inspeções, ações, EPI e documentos."),
            ("MEDICO",      "Médico",             "PCMSO, exames, ASO e dados clínicos."),
            ("RH",          "Recursos Humanos",   "Trabalhadores, cargos e status de aptidão."),
            ("LIDER",       "Liderança",          "Pendências e ações da própria equipe."),
            ("TRABALHADOR", "Trabalhador",        "Seus documentos, treinamentos e EPIs."),
            ("AUDITOR",     "Auditor",            "Consulta e exportação com prazo definido."),
        };

        var existingRoles = await db.Roles
            .Where(r => r.DeletedAt == null)
            .ToListAsync(ct);

        var newRoles = new List<Role>();

        foreach (var (code, name, description) in roles)
        {
            var exists = existingRoles.Any(r => r.Code == code);

            if (!exists)
            {
                newRoles.Add(new Role(code, name, description));
            }
        }

        if (newRoles.Count > 0)
        {
            await db.Roles.AddRangeAsync(newRoles, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed: {Count} perfis criados.", newRoles.Count);
        }

        // ═══ VÍNCULOS ROLE × PERMISSION ════════════════════════════════════
        await SeedRolePermissionsAsync(db, logger, ct);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. Vínculos Role × Permission
    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedRolePermissionsAsync(
        SesmtDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var roles = await db.Roles
            .Where(r => r.DeletedAt == null)
            .ToListAsync(ct);

        var permissions = await db.Permissions
            .Where(p => p.DeletedAt == null)
            .ToListAsync(ct);

        var existingLinks = await db.RolePermissions
            .Where(rp => rp.DeletedAt == null)
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);

        var existingSet = existingLinks
            .Select(l => (l.RoleId, l.PermissionId))
            .ToHashSet();

        var newLinks = new List<RolePermission>();

        // ADMIN → todas as permissões
        var adminRole = roles.FirstOrDefault(r => r.Code == "ADMIN");
        if (adminRole is not null)
        {
            foreach (var perm in permissions)
            {
                if (!existingSet.Contains((adminRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(adminRole.Id, perm.Id));
            }
        }

        // GESTOR → tudo exceto ASO (dados clínicos) e administração de sistema
        var gestorRole = roles.FirstOrDefault(r => r.Code == "GESTOR");
        if (gestorRole is not null)
        {
            var gestorPerms = permissions.Where(p =>
                !p.Resource.Equals("aso", StringComparison.OrdinalIgnoreCase) &&
                !p.Resource.Equals("perfil", StringComparison.OrdinalIgnoreCase) &&
                !p.Resource.Equals("usuario", StringComparison.OrdinalIgnoreCase) &&
                !p.Resource.Equals("parametro", StringComparison.OrdinalIgnoreCase));

            foreach (var perm in gestorPerms)
            {
                if (!existingSet.Contains((gestorRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(gestorRole.Id, perm.Id));
            }
        }

        // TECNICO → SST (documentos, riscos, EPI, treinamentos, PT)
        var tecnicoRole = roles.FirstOrDefault(r => r.Code == "TECNICO");
        if (tecnicoRole is not null)
        {
            var tecnicoResources = new[] { "documento", "pt", "trabalhador", "equipe" };
            var tecnicoPerms = permissions.Where(p =>
                tecnicoResources.Contains(p.Resource) &&
                p.Action != "excluir");

            foreach (var perm in tecnicoPerms)
            {
                if (!existingSet.Contains((tecnicoRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(tecnicoRole.Id, perm.Id));
            }
        }

        // MEDICO → ASO + trabalhador:ler + documento:ler
        var medicoRole = roles.FirstOrDefault(r => r.Code == "MEDICO");
        if (medicoRole is not null)
        {
            var medicoPerms = permissions.Where(p =>
                (p.Resource == "aso") ||
                (p.Resource == "trabalhador" && p.Action == "ler") ||
                (p.Resource == "documento" && p.Action == "ler"));

            foreach (var perm in medicoPerms)
            {
                if (!existingSet.Contains((medicoRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(medicoRole.Id, perm.Id));
            }
        }

        // RH → trabalhador (CRUD) + ler documentos + ler unidade/setor/cargo
        var rhRole = roles.FirstOrDefault(r => r.Code == "RH");
        if (rhRole is not null)
        {
            var rhPerms = permissions.Where(p =>
                (p.Resource == "trabalhador") ||
                (p.Resource == "documento" && p.Action == "ler") ||
                (p.Resource == "unidade" && p.Action == "ler") ||
                (p.Resource == "setor" && p.Action == "ler") ||
                (p.Resource == "cargo" && p.Action == "ler"));

            foreach (var perm in rhPerms)
            {
                if (!existingSet.Contains((rhRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(rhRole.Id, perm.Id));
            }
        }

        // LIDER → ler apenas
        var liderRole = roles.FirstOrDefault(r => r.Code == "LIDER");
        if (liderRole is not null)
        {
            var liderPerms = permissions.Where(p => p.Action == "ler");

            foreach (var perm in liderPerms)
            {
                if (!existingSet.Contains((liderRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(liderRole.Id, perm.Id));
            }
        }

        // TRABALHADOR → apenas documentos e treinamentos (próprios)
        var trabalhadorRole = roles.FirstOrDefault(r => r.Code == "TRABALHADOR");
        if (trabalhadorRole is not null)
        {
            var trabalhadorPerms = permissions.Where(p =>
                p.Action == "ler" &&
                (p.Resource == "documento" || p.Resource == "trabalhador"));

            foreach (var perm in trabalhadorPerms)
            {
                if (!existingSet.Contains((trabalhadorRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(trabalhadorRole.Id, perm.Id));
            }
        }

        // AUDITOR → apenas ler + exportar
        var auditorRole = roles.FirstOrDefault(r => r.Code == "AUDITOR");
        if (auditorRole is not null)
        {
            var auditorPerms = permissions.Where(p =>
                p.Action == "ler" || p.Action == "exportar");

            foreach (var perm in auditorPerms)
            {
                if (!existingSet.Contains((auditorRole.Id, perm.Id)))
                    newLinks.Add(new RolePermission(auditorRole.Id, perm.Id));
            }
        }

        if (newLinks.Count > 0)
        {
            await db.RolePermissions.AddRangeAsync(newLinks, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed: {Count} vínculos role-permission criados.", newLinks.Count);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Usuário admin inicial
    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedAdminUserAsync(
        SesmtDbContext db,
        IPasswordHashService passwordHasher,
        SeedSettings settings,
        ILogger logger,
        CancellationToken ct)
    {
        var normalizedEmail = settings.AdminEmail.Trim().ToLowerInvariant();

        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (adminUser is not null)
        {
            logger.LogInformation(
                "Seed: admin '{Email}' já existe, nada a fazer.", normalizedEmail);
            return;
        }

        var passwordHash = passwordHasher.HashPassword(settings.AdminPassword);

        adminUser = new User(
            name: settings.AdminName,
            email: normalizedEmail,
            passwordHash: passwordHash);

        await db.Users.AddAsync(adminUser, ct);
        await db.SaveChangesAsync(ct);

        var adminRole = await db.Roles
            .FirstOrDefaultAsync(r => r.Code == "ADMIN" && r.DeletedAt == null, ct);

        if (adminRole is null)
        {
            throw new InvalidOperationException(
                "Perfil ADMIN não encontrado — verifique o seed de perfis.");
        }

        var userRole = new UserRole(
            userId: adminUser.Id,
            roleId: adminRole.Id,
            companyId: null);   // null = global

        await db.UserRoles.AddAsync(userRole, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seed: admin '{Email}' criado com perfil ADMIN.", normalizedEmail);
    }
}
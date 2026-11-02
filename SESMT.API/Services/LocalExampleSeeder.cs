using Microsoft.EntityFrameworkCore;
using Npgsql;
using SESMT.Application.Common.Interfaces;
using SESMT.Domain.Common.Entity;
using SESMT.Domain.Modules.Audit.Entities;
using SESMT.Domain.Modules.Documents.Constants;
using SESMT.Domain.Modules.Documents.Entities;
using SESMT.Domain.Modules.IdentityAccess.Entities;
using SESMT.Domain.Modules.Notifications.Entities;
using SESMT.Domain.Modules.Organization.Entities;
using SESMT.Domain.Modules.Organization.Enums;
using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.Modules.WorkPermits.Entities;
using SESMT.Infrastructure.Persistence;

namespace SESMT.API.Services;

public sealed record ExampleAccount(string Name, string Email, string[] Roles);
public sealed record ExampleSeedResult(bool AlreadyPresent, Guid CompanyId, int Users, int Workers, int Units, int Teams, int Documents, int Appointments, int Permits, bool CredentialsMatch, ExampleAccount[] Accounts);

/// <summary>Explicit CLI-only fixture. Never called by ordinary application startup.</summary>
public static class LocalExampleSeeder
{
    public static readonly Guid CompanyId = Guid.Parse("e76a12c0-20aa-4d00-8000-000000000001");
    private const string CompanyName = "EXEMPLO - Operação Ampara";
    public static readonly ExampleAccount[] Accounts = [
        new("Exemplo Alice Administradora", "admin@exemplo.ampara.local", ["ADMIN", "SESMT_PT_CONFIG"]),
        new("Exemplo Bruno Gestor", "gestor@exemplo.ampara.local", ["GESTOR", "SESMT_PT_CONFIG", "SESMT_PT_LIBERADOR"]),
        new("Exemplo Carla Tecnica", "tecnico@exemplo.ampara.local", ["TECNICO", "SESMT_PT_SOLICITANTE", "SESMT_PT_OPERADOR"]),
        new("Exemplo Daniel Bombeiro", "bombeiro@exemplo.ampara.local", ["EXEMPLO_BOMBEIRO", "SESMT_PT_SOLICITANTE", "SESMT_PT_OPERADOR"]),
        new("Exemplo Elisa Recursos Humanos", "rh@exemplo.ampara.local", ["RH"]),
        new("Exemplo Fabio Auditor", "auditor@exemplo.ampara.local", ["AUDITOR"]),
        new("Exemplo Gabriela Medica", "medico@exemplo.ampara.local", ["MEDICO"]),
        new("Exemplo Hugo Trabalhador", "trabalhador@exemplo.ampara.local", ["TRABALHADOR"]),
    ];
    public static void RequireLocalTarget(string environment, string connectionString)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (environment != "Development" || target.Host is not ("127.0.0.1" or "localhost" or "::1")
            || !System.Text.RegularExpressions.Regex.IsMatch(target.Database ?? "", "^(ampara_fullstack_preview_[0-9]{8}|ampara_uiqa_[a-f0-9]{12}|sesmt_it_[a-f0-9]{32})$"))
            throw new InvalidOperationException("Exemplos exigem Development, loopback e um banco local de prévia/QA explicitamente identificado.");
    }

    public static async Task<ExampleSeedResult> SeedAsync(SesmtDbContext db, IPasswordHashService passwords, ICpfProtectionService cpf,
        string actorEmail, string examplePassword, DateTimeOffset now, CancellationToken ct = default)
    {
        if (examplePassword.Length < 16) throw new ArgumentException("A senha local de exemplos precisa ter ao menos 16 caracteres.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724891352618::bigint)", ct);
        var previous = await db.Companies.SingleOrDefaultAsync(c => c.Id == CompanyId, ct);
        if (previous is not null)
        {
            if (previous.Cnpj.Value != "00000000000191" || !previous.Active || previous.DeletedAt.HasValue)
                throw new InvalidOperationException("O identificador do conjunto já existe com outro estado. Nenhum cadastro foi alterado.");
            return await Report(true);
        }
        if (await db.Companies.AnyAsync(c => c.Cnpj.Value == "00000000000191", ct)
            || await db.Users.AnyAsync(u => Accounts.Select(a => a.Email).Contains(u.Email.Value), ct))
            throw new InvalidOperationException("Há um cadastro anterior usando um identificador reservado aos exemplos. Nenhum dado foi substituído.");
        var actor = await db.Users.SingleAsync(u => u.Email.Value == actorEmail && u.Active && u.DeletedAt == null, ct);
        if (!await (from a in db.UserRoles join r in db.Roles on a.RoleId equals r.Id
            where a.UserId == actor.Id && a.CompanyId == null && a.Active && a.DeletedAt == null && r.Code == "ADMIN" && r.Active && r.DeletedAt == null select a.Id).AnyAsync(ct))
            throw new InvalidOperationException("O responsável pela carga precisa ser um administrador global ativo.");
        var company = new Company(CompanyName, CompanyName, "00000000000191", null, null, CompanyType.INTERNA);
        db.Entry(company).Property(c => c.Id).CurrentValue = CompanyId; db.Add(company);
        var units = new[] { "Matriz de treinamento", "Unidade de manutenção" }.Select((name, i) => new Unit(CompanyId, "EX-" + (i + 1), "EXEMPLO - " + name,
            i == 0 ? "MATRIZ" : "FILIAL", "00000000", "Endereço fictício para testes", "S/N", null, "Bairro exemplo", "Cidade exemplo", "PR")).ToArray(); db.AddRange(units);
        var departments = units.Select(u => new Department(u.Id, "EXEMPLO - Operações", "Dados sintéticos para testes locais.", null)).ToArray(); db.AddRange(departments);
        var positions = new[] { "Administração", "Gestão SESMT", "Técnico de segurança", "Bombeiro", "Recursos Humanos", "Auditoria", "Medicina", "Operação" }
            .Select(name => new Position(CompanyId, "EXEMPLO - " + name, null, "Cadastro sintético.")).ToArray(); db.AddRange(positions);
        var roles = await db.Roles.Where(r => r.Active && r.DeletedAt == null).ToDictionaryAsync(r => r.Code, ct);
        if (roles.ContainsKey("EXEMPLO_BOMBEIRO")) throw new InvalidOperationException("O perfil reservado aos exemplos já existe fora do conjunto.");
        var firefighter = new Role("EXEMPLO_BOMBEIRO", "Bombeiro (exemplo)", "Perfil exclusivo do conjunto de exemplos locais."); db.Add(firefighter); roles.Add(firefighter.Code, firefighter);
        var technicianRole = roles["TECNICO"].Id;
        foreach (var permission in await db.RolePermissions.Where(r => r.RoleId == technicianRole && r.Active && r.DeletedAt == null).Select(r => r.PermissionId).ToListAsync(ct))
            db.Add(new RolePermission(firefighter.Id, permission));
        var people = new List<User>(); var workers = new List<Worker>();
        for (var i = 0; i < Accounts.Length; i++)
        {
            var account = Accounts[i]; var person = new User(account.Name, account.Email, passwords.HashPassword(examplePassword)); people.Add(person); db.Add(person);
            var scope = new UserScope(person.Id, "EMPRESA"); scope.Update(person.Id, "EMPRESA", CompanyId); db.Add(scope);
            foreach (var role in account.Roles) db.Add(new UserRole(person.Id, roles[role].Id, CompanyId));
            var protectedCpf = cpf.Protect(ExampleCpf(i + 1)); var location = i == 3 ? 1 : 0;
            var worker = new Worker(CompanyId, $"EX-{i + 1:000}", protectedCpf.Encrypted, protectedCpf.Hash, account.Name,
                new DateOnly(1990 + i, 1, 15), DateOnly.FromDateTime(now.UtcDateTime).AddYears(-1), null, positions[i].Id, units[location].Id, departments[location].Id, person.Id);
            workers.Add(worker); db.Add(worker); db.Add(new WorkerHistory(worker.Id, departments[location].Id, positions[i].Id, worker.AdmissionDate, reasonForChange: "EXEMPLO - Admissão para testes locais."));
        }
        var teams = units.Select((unit, i) => new Team(CompanyId, CompanyId, unit.Id, "EXEMPLO - Equipe " + (i + 1), "Equipe sintética para testes.", workers[i == 0 ? 2 : 3].Id,
            i == 0 ? [workers[7].Id] : [])).ToArray(); db.AddRange(teams);
        var type = new DocumentType("EXEMPLO_REGISTRO", "EXEMPLO - Registro administrativo", "Sem valor de comprovação profissional.", "administrativo", SecrecyLevel.Internal, false, false, null, null, "{}", 1); db.Add(type);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        for (var i = 0; i < workers.Count; i++)
        {
            var document = new Document(type.Id, CompanyId, workers[i].UnitId, workers[i].DepartmentId, $"EX-DOC-{i + 1:000}", "EXEMPLO - Registro de " + Accounts[i].Name,
                "Rascunho sem arquivo ou aprovação. Não comprova treinamento, aptidão ou entrega de EPI.", SecrecyLevel.Internal, today.AddDays(-60), today.AddDays(-60), today.AddDays(i < 2 ? -5 : i < 5 ? 10 : 90), people[0].Id);
            db.Add(document); db.Add(new DocumentLink(document.Id, LinkedEntityType.Worker, workers[i].Id));
        }
        var services = WorkPermitServiceCatalogEntry.All.Where(s => s.Code is "ALTURA" or "FRIO").ToArray();
        var templates = services.Select(s => new WorkPermitTemplateVersion(CompanyId, Guid.NewGuid(), "EXEMPLO - Checklist " + s.Name, 1, [s.Code],
            [new(Guid.NewGuid(), "Exemplo: as informações da atividade foram conferidas?", "BOOLEAN", true, true)],
            "Modelo ilustrativo para testar o preenchimento. Não constitui autorização de trabalho.", true, people[0].Id,
            [new(Guid.NewGuid(), "Exemplo: registrar o resultado da atividade.", "TEXT", true, false)])).ToArray(); db.AddRange(templates);
        foreach (var service in services) db.Add(new WorkPermitDocumentRequirement(CompanyId, service.Code, type.Id, "EXEMPLO - Evidência pendente, sem liberação automática.", people[0].Id));
        for (var i = 0; i < 6; i++)
        {
            var location = i % 2; var start = new DateTimeOffset(now.UtcDateTime.Date.AddDays(i + 1).AddHours(12), TimeSpan.Zero);
            var roster = location == 0 ? new[] { 2, 7 } : new[] { 3 };
            var appointment = new WorkPermitAppointment(CompanyId, "EXEMPLO - " + new[] { "Vistoria de cobertura", "Manutenção de bancada", "Inspeção de área" }[i % 3], start, start.AddHours(3), people[i % 2 == 0 ? 0 : 2].Id, units[location].Id);
            appointment.ConfigureContext(teams[location].Id, [services[location].Code], roster.Select(index => workers[index].Id).ToArray(), people[0].Id, appointment.Version);
            if (i < 4) appointment.Confirm(people[0].Id, people[location == 0 ? 2 : 3].Id, appointment.Version);
            db.Add(appointment);
            if (i < 3)
            {
                var permit = new WorkPermit(CompanyId, appointment.Id, services[location].Code, appointment.Title, people[0].Id);
                permit.PinTemplates([templates[location]], people[0].Id);
                foreach (var index in roster) permit.AddParticipant(new WorkPermitParticipant(permit.Id, people[index].Id, "EXEMPLO - Participante de teste", people[0].Id), people[0].Id);
                db.Add(permit);
            }
        }
        foreach (var (index, kind) in new[] { (2, "atender"), (3, "atender"), (1, "revisar"), (1, "liberar") })
            db.Add(new OperationalDesignation(CompanyId, workers[index].Id, services.Select(s => s.Id), units.Select(u => u.Id), kind, now.AddDays(-1), now.AddDays(90), "EXEMPLO - Designação local de teste", people[0].Id));
        foreach (var recipient in new[] { people[0].Id, people[1].Id, people[2].Id, actor.Id })
            db.Add(new Alert("EXEMPLO", "empresa", CompanyId, Guid.NewGuid(), recipient, "SISTEMA", "EXEMPLO - Cadastros para explorar o sistema",
                "Os registros EXEMPLO são sintéticos. As PTs permanecem em verificação e os documentos em rascunho.", now));
        db.Add(new AuditLog("CRIACAO", "conjunto_exemplos", CompanyId, actor.Id, actor.Email.Value, actor.Name, null, null, null, null, "CLI local",
            await db.AuditLogs.OrderByDescending(a => a.Id).Select(a => a.CurrentHash).FirstOrDefaultAsync(ct), companyId: CompanyId));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return await Report(false);

        async Task<ExampleSeedResult> Report(bool existed)
        {
            var emails = Accounts.Select(a => a.Email).ToArray();
            var accounts = await db.Users.Where(u => emails.Contains(u.Email.Value) && u.Active && u.DeletedAt == null).ToListAsync(ct);
            var credentialsMatch = accounts.Count == Accounts.Length && accounts.All(u => passwords.VerifyPassword(u.PasswordHash, examplePassword));
            return new(existed, CompanyId,
            await db.UserRoles.Where(r => r.CompanyId == CompanyId && r.Active && r.DeletedAt == null).Select(r => r.UserId).Distinct().CountAsync(ct),
            await db.Workers.CountAsync(w => w.CompanyId == CompanyId, ct), await db.Units.CountAsync(u => u.CompanyId == CompanyId, ct),
            await db.Teams.CountAsync(t => t.CompanyId == CompanyId, ct), await db.Documents.CountAsync(d => d.CompanyId == CompanyId, ct),
            await db.WorkPermitAppointments.CountAsync(a => a.CompanyId == CompanyId, ct), await db.WorkPermits.CountAsync(p => p.CompanyId == CompanyId, ct), credentialsMatch, Accounts);
        }
    }
    private static string ExampleCpf(int number)
    {
        var value = number.ToString("D9");
        for (var length = 9; length <= 10; length++) { var sum = value.Select((ch, index) => (ch - '0') * (length + 1 - index)).Sum(); var digit = sum * 10 % 11; value += digit > 9 ? 0 : digit; }
        return value;
    }
}

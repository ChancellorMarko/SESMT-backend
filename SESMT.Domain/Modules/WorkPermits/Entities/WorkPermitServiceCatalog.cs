namespace SESMT.Domain.Modules.WorkPermits.Entities;

public sealed record WorkPermitServiceCatalogEntry(Guid Id, string Code, string Name)
{
    public static IReadOnlyList<WorkPermitServiceCatalogEntry> All { get; } =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "ALTURA", "Trabalho em altura"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "QUENTE", "Trabalho a quente"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "FRIO", "Trabalho a frio"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "CONFINADO", "Espaço confinado"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000005"), "ELETRICIDADE", "Eletricidade")
    ];

    public static IReadOnlyList<string> ResolveServiceCodes(IEnumerable<Guid> serviceIds)
    {
        var suppliedIds = serviceIds.ToArray();
        var ids = suppliedIds.Distinct().ToArray();
        var matches = All.Where(service => ids.Contains(service.Id)).ToArray();
        if (ids.Length == 0 || suppliedIds.Length != ids.Length || matches.Length != ids.Length)
            throw new ArgumentException("Selecione serviços válidos do catálogo.");
        return matches.Select(service => service.Code).ToArray();
    }
}

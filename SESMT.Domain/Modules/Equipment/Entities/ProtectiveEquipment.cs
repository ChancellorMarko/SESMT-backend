using SESMT.Domain.Common.AggregateRoot;

namespace SESMT.Domain.Modules.Equipment.Entities;

public sealed class ProtectiveEquipment : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? ApprovalCertificate { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public int Revision { get; private set; } = 1;
    private ProtectiveEquipment() { }
    public ProtectiveEquipment(Guid company, string code, string name, string? certificate, string? manufacturer, string? model, Guid actor)
    {
        if (company == Guid.Empty || actor == Guid.Empty) throw new ArgumentException("Empresa e responsável são obrigatórios.");
        CompanyId = company; CreatedBy = actor; Set(code, name, certificate, manufacturer, model);
    }
    public void Update(string code, string name, string? certificate, string? manufacturer, string? model, Guid actor)
    { Set(code, name, certificate, manufacturer, model); Revision++; MarkAsUpdated(actor); }
    private void Set(string code, string name, string? certificate, string? manufacturer, string? model)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 30 || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160
            || certificate?.Trim().Length > 30 || manufacturer?.Trim().Length > 120 || model?.Trim().Length > 120)
            throw new ArgumentException("Confira código, nome, CA, fabricante e modelo do equipamento.");
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); ApprovalCertificate = Clean(certificate);
        Manufacturer = Clean(manufacturer); Model = Clean(model);
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EquipmentDelivery : AggregateRootBase
{
    public Guid CompanyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid WorkerId { get; private set; }
    public Guid EquipmentId { get; private set; }
    public Guid CommandId { get; private set; }
    public string EquipmentName { get; private set; } = "";
    public string EquipmentCode { get; private set; } = "";
    public string? ApprovalCertificate { get; private set; }
    public int Quantity { get; private set; }
    public string? Size { get; private set; }
    public DateOnly DeliveredOn { get; private set; }
    public DateOnly? ReplacementOn { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly? ReturnedOn { get; private set; }
    public string? ReturnReason { get; private set; }
    public int Revision { get; private set; } = 1;
    private EquipmentDelivery() { }
    public EquipmentDelivery(ProtectiveEquipment equipment, Guid unit, Guid worker, Guid command, int quantity, string? size,
        DateOnly deliveredOn, DateOnly? replacementOn, string? notes, Guid actor)
    {
        if (unit == Guid.Empty || worker == Guid.Empty || command == Guid.Empty || actor == Guid.Empty || quantity is < 1 or > 1000
            || replacementOn < deliveredOn || size?.Length > 30 || notes?.Length > 500) throw new ArgumentException("Dados de entrega inválidos.");
        CompanyId = equipment.CompanyId; UnitId = unit; WorkerId = worker; CommandId = command; EquipmentId = equipment.Id;
        EquipmentName = equipment.Name; EquipmentCode = equipment.Code; ApprovalCertificate = equipment.ApprovalCertificate;
        Quantity = quantity; Size = string.IsNullOrWhiteSpace(size) ? null : size.Trim(); DeliveredOn = deliveredOn;
        ReplacementOn = replacementOn; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); CreatedBy = actor;
    }
    public void Return(DateOnly on, string reason, Guid actor)
    {
        if (ReturnedOn.HasValue) throw new InvalidOperationException("Esta entrega já possui devolução.");
        if (on < DeliveredOn || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 200) throw new ArgumentException("Confira data e motivo da devolução.");
        ReturnedOn = on; ReturnReason = reason.Trim(); Revision++; MarkAsUpdated(actor);
    }
}

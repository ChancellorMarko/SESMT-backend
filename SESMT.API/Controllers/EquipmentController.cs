using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Services;

namespace SESMT.API.Controllers;

/// <summary>HTTP translation only; rules live in <see cref="EquipmentService"/>.</summary>
[ApiController, Authorize, Route("equipment")]
public sealed class EquipmentController(EquipmentService equipment) : ApiControllerBase
{
    [HttpGet("context")]
    public Task<IActionResult> Context(Guid unitId, CancellationToken ct) => Respond(() => equipment.ContextAsync(unitId, ct));
    [HttpGet]
    public Task<IActionResult> List(Guid unitId, CancellationToken ct, string? q = null, int page = 1, int pageSize = 20) => Respond(() => equipment.ListAsync(unitId, ct, q, page, pageSize));
    [HttpPost]
    public Task<IActionResult> Create(EquipmentRequest request, CancellationToken ct) => Respond(() => equipment.SaveCatalogAsync(null, request, ct), 201);
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, Guid unitId, CancellationToken ct) => Respond(() => equipment.GetAsync(id, unitId, ct));
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Edit(Guid id, EquipmentRequest request, CancellationToken ct) => Respond(() => equipment.SaveCatalogAsync(id, request, ct));
    [HttpGet("deliveries")]
    public Task<IActionResult> Deliveries(Guid unitId, CancellationToken ct, string? q = null, Guid? workerId = null, string? status = null, int page = 1, int pageSize = 20)
        => Respond(() => equipment.DeliveriesAsync(unitId, ct, q, workerId, status, page, pageSize));
    [HttpGet("deliveries/{id:guid}")]
    public Task<IActionResult> Delivery(Guid id, Guid unitId, CancellationToken ct) => Respond(() => equipment.DeliveryAsync(id, unitId, ct));
    [HttpPost("deliveries")]
    public Task<IActionResult> Deliver(EquipmentDeliveryRequest request, CancellationToken ct) => Respond(() => equipment.DeliverAsync(request, ct), 201);
    [HttpPost("deliveries/{id:guid}/return")]
    public Task<IActionResult> Return(Guid id, EquipmentReturnRequest request, CancellationToken ct) => Respond(() => equipment.ReturnAsync(id, request, ct));
}

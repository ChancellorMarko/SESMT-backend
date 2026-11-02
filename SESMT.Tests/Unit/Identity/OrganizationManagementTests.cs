using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SESMT.API.Controllers;
using Fixture = SESMT.Tests.Unit.Identity.NativeResourceAuthorizationTests.Fixture;

namespace SESMT.Tests.Unit.Identity;

public sealed class OrganizationManagementTests
{
    private static async Task<JsonElement> Read(Fixture f)
    {
        var controller = new OrganizationManagementController(f.Access(), f.Db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        return JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Read(default)).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    [Fact]
    public async Task UnitScopedActorSeesOnlyItsHierarchyAndCannotCreateUnitOrCompany()
    {
        await using var f = await Fixture.Create(); var body = await Read(f);
        Assert.False(body.GetProperty("canCreateCompany").GetBoolean());
        Assert.Equal(f.CompanyA.Id, Assert.Single(body.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.False(body.GetProperty("companies")[0].GetProperty("canCreateUnit").GetBoolean());
        Assert.False(body.GetProperty("companies")[0].GetProperty("canCreatePosition").GetBoolean());
        Assert.Equal(f.Units[0].Id, Assert.Single(body.GetProperty("units").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("units")[0].GetProperty("canCreateDepartment").GetBoolean());
        Assert.Equal(2, body.GetProperty("departments").GetArrayLength());
    }
    [Fact]
    public async Task SectorScopedActorCannotCreateSiblingsInItsAncestorUnit()
    {
        await using var f = await Fixture.Create("SETOR"); var body = await Read(f);
        Assert.False(body.GetProperty("units")[0].GetProperty("canCreateDepartment").GetBoolean());
        Assert.Single(body.GetProperty("departments").EnumerateArray());
    }
    [Fact]
    public async Task EmptyInstallationStillAllowsExplicitGlobalAdminToBootstrapACompany()
    {
        await using var f = await Fixture.Create("EMPRESA", "ADMIN", global: true);
        f.CompanyA.Deactivate(); f.CompanyB.Deactivate(); await f.Db.SaveChangesAsync();
        var body = await Read(f); Assert.True(body.GetProperty("canCreateCompany").GetBoolean());
        Assert.Empty(body.GetProperty("companies").EnumerateArray()); Assert.Empty(body.GetProperty("units").EnumerateArray());
    }
}

using SESMT.Domain.Modules.Organization.Entities;

namespace SESMT.Tests.Unit.Organization;

public sealed class TeamVersionTests
{
    [Fact]
    public void StandaloneAndGroupedCompaniesUseAnExplicitServerRoot()
    {
        var company = Guid.NewGuid(); var group = Guid.NewGuid();
        Assert.Equal(company, Team.OrganizationFor(company, null));
        Assert.Equal(group, Team.OrganizationFor(company, group));
        Assert.Throws<ArgumentException>(() => Team.OrganizationFor(Guid.Empty, group));
        Assert.Throws<ArgumentException>(() => Team.OrganizationFor(company, Guid.Empty));
    }

    [Fact]
    public void EveryRosterMutationAdvancesItsRevisionWithoutDuplicatingTheChief()
    {
        var company = Guid.NewGuid(); var chief = Guid.NewGuid(); var worker = Guid.NewGuid();
        var team = new Team(company, company, Guid.NewGuid(), "Equipe", "Descrição", chief, [chief]);
        Assert.Equal(1, team.Version); Assert.Empty(team.ParticipantIds);
        team.AddParticipant(chief); Assert.Equal(1, team.Version); Assert.Empty(team.ParticipantIds);
        team.AddParticipant(worker); Assert.Equal(2, team.Version);
        team.AddParticipant(worker); Assert.Equal(2, team.Version);
        team.RemoveParticipant(worker); Assert.Equal(3, team.Version);
        team.RemoveParticipant(worker); Assert.Equal(3, team.Version);
        team.Update("Alterada", null, null, chief, []); Assert.Equal(4, team.Version); Assert.Null(team.Description);
        team.Deactivate(); Assert.Equal(5, team.Version); Assert.NotNull(team.DeletedAt);
    }
}

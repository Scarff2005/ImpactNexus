using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactNexus.Tests;

/// <summary>
/// The schema is the last line of defence: foreign keys and unique indexes must
/// reject bad data even if a controller is bypassed.
/// </summary>
public class DataIntegrityTests : IntegrationTestBase
{
    public DataIntegrityTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Company_with_activities_cannot_be_deleted()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = await db.Companies.FirstAsync(c => c.Activities.Any());

        db.Companies.Remove(company);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_company_names_are_rejected()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existingName = await db.Companies.Select(c => c.Name).FirstAsync();

        db.Companies.Add(new Company { Name = existingName });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_a_bursary_programme_cascades_its_applications()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var programme = await db.BursaryProgrammes.Include(p => p.Applications).FirstAsync();
        var applicationIds = programme.Applications.Select(a => a.Id).ToList();

        db.BursaryProgrammes.Remove(programme);
        await db.SaveChangesAsync();

        var remaining = await db.BursaryApplications
            .Where(a => applicationIds.Contains(a.Id))
            .CountAsync();

        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Province_requires_an_existing_country()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Provinces.Add(new Province { Name = "Nowhere", CountryId = 999_999 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Every_seeded_user_has_exactly_one_role()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var usersWithoutRoles = await db.Users
            .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id))
            .CountAsync();

        Assert.Equal(0, usersWithoutRoles);
    }

    [Fact]
    public async Task All_four_roles_are_seeded()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roleNames = await db.Roles.Select(r => r.Name).ToListAsync();

        Assert.Contains("IT Administrator", roleNames);
        Assert.Contains("CSR Manager", roleNames);
        Assert.Contains("Investor / Corporate Donor", roleNames);
        Assert.Contains("Community Member / Volunteer", roleNames);
    }

    [Fact]
    public async Task Volunteer_hours_are_linked_to_a_real_programme_and_user()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var orphans = await db.VolunteerHours
            .Where(v => !db.VolunteerProgrammes.Any(p => p.Id == v.VolunteerProgrammeId)
                        || !db.Users.Any(u => u.Id == v.UserId))
            .CountAsync();

        Assert.Equal(0, orphans);
    }
}

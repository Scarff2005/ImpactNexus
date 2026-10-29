using System.Net;
using ImpactNexus.Web.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ImpactNexus.Tests;

public sealed class ModuleTests : IntegrationTestBase
{
    public ModuleTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Administrator_can_create_and_delete_a_company()
    {
        var client = await LoginAsAdministratorAsync();
        var token = await GetTokenAsync(client, "/Companies/Create");

        var created = await client.PostAsync("/Companies/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Test Harness Holdings",
                ["RegistrationNumber"] = "2026/000001/07",
                ["Industry"] = "Testing",
                ["Description"] = "Created by an automated test.",
                ["CountryId"] = CountryId().ToString(),
                ["IsActive"] = "true",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var id = Query(db => db.Companies.Where(c => c.Name == "Test Harness Holdings").Select(c => c.Id).Single());
        var details = await client.GetAsync($"/Companies/Details/{id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var deleteToken = await GetTokenAsync(client, $"/Companies/Delete/{id}");
        var deleted = await client.PostAsync($"/Companies/Delete/{id}", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = id.ToString(),
                ["__RequestVerificationToken"] = deleteToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.False(Query(db => db.Companies.Any(c => c.Id == id)));
    }

    [Fact]
    public async Task Company_creation_without_a_name_fails_validation()
    {
        var client = await LoginAsAdministratorAsync();
        var token = await GetTokenAsync(client, "/Companies/Create");

        var response = await client.PostAsync("/Companies/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "",
                ["CountryId"] = CountryId().ToString(),
                ["IsActive"] = "true",
                ["__RequestVerificationToken"] = token
            }));

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", html);
    }

    [Fact]
    public async Task Manager_cannot_create_an_activity_for_another_company()
    {
        var client = await LoginAsManagerAsync();
        var ownCompanyId = CompanyIdOf("csr.manager@impactnexus.local");
        var token = await GetTokenAsync(client, "/Activities/Create");

        var response = await client.PostAsync("/Activities/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Title"] = "Cross company activity",
                ["Description"] = "Should be rejected.",
                ["CompanyId"] = OtherCompanyId(ownCompanyId).ToString(),
                ["StartDate"] = "2026-05-01",
                ["Budget"] = "1000",
                ["Status"] = "Planned",
                ["__RequestVerificationToken"] = token
            }));

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("only create activities for your own company", html);
        Assert.False(Query(db => db.CsrActivities.Any(a => a.Title == "Cross company activity")));
    }

    [Fact]
    public async Task Manager_activity_list_is_scoped_to_their_company()
    {
        var client = await LoginAsManagerAsync();
        var ownCompanyId = CompanyIdOf("csr.manager@impactnexus.local");

        var ownTitle = Query(db => db.CsrActivities
            .Where(a => a.CompanyId == ownCompanyId).Select(a => a.Title).First());
        var otherTitle = Query(db => db.CsrActivities
            .Where(a => a.CompanyId != ownCompanyId).Select(a => a.Title).First());

        var html = await client.GetStringAsync("/Activities");

        Assert.Contains(ownTitle, html);
        Assert.DoesNotContain(otherTitle, html);
    }

    [Theory]
    [InlineData("/Reports/Activities?export=true", "Id,Title,Company,Location,StartDate,EndDate,Budget,Status,ImpactIndicator")]
    [InlineData("/Reports/Donations?export=true", "DonatedOn,DonorName")]
    [InlineData("/Reports/VolunteerHours?export=true", "Volunteer,Programme")]
    public async Task Report_exports_return_csv(string path, string expectedHeader)
    {
        var client = await LoginAsAdministratorAsync();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith(expectedHeader, body.TrimStart('\uFEFF'));
    }

    [Fact]
    public async Task Investor_export_only_contains_their_own_contributions()
    {
        var client = await LoginAsInvestorAsync();
        var investorName = Query(db => db.Users
            .Where(u => u.Email == "investor@impactnexus.local").Select(u => u.FullName).Single());

        var csv = await client.GetStringAsync("/Reports/Donations?export=true");

        Assert.Contains(investorName, csv);
        Assert.DoesNotContain("Deon Pillay", csv);
    }

    [Fact]
    public async Task Community_member_export_only_contains_their_own_hours()
    {
        var client = await LoginAsCommunityMemberAsync();
        var name = Query(db => db.Users
            .Where(u => u.Email == "community@impactnexus.local").Select(u => u.FullName).Single());

        var csv = await client.GetStringAsync("/Reports/VolunteerHours?export=true");

        Assert.Contains(name, csv);
        Assert.DoesNotContain("Lerato Ndlovu", csv);
    }

    private int CountryId() => Query(db => db.Countries.OrderBy(c => c.Id).Select(c => c.Id).First());

    private int OtherCompanyId(int companyId) => Query(db =>
        db.Companies.Where(c => c.Id != companyId).OrderBy(c => c.Id).Select(c => c.Id).First());
}

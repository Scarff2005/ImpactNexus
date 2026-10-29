using System.Net;
using Xunit;

namespace ImpactNexus.Tests;

public sealed class AccessControlTests : IntegrationTestBase
{
    public AccessControlTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Anonymous_user_is_redirected_to_login()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Administrator_can_open_user_management()
    {
        var client = await LoginAsAdministratorAsync();

        var response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("csr.manager@impactnexus.local", "Manager#2026")]
    [InlineData("investor@impactnexus.local", "Investor#2026")]
    [InlineData("community@impactnexus.local", "Volunteer#2026")]
    public async Task Non_administrators_are_denied_user_management(string email, string password)
    {
        var client = CreateClient();
        await LoginAsync(client, email, password);

        var response = await client.GetAsync("/Users");

        AssertAccessDenied(response);
    }

    [Fact]
    public async Task Manager_can_view_own_company_only()
    {
        var client = await LoginAsManagerAsync();
        var ownCompanyId = CompanyIdOf("csr.manager@impactnexus.local");

        var own = await client.GetAsync($"/Companies/Details/{ownCompanyId}");
        var other = await client.GetAsync($"/Companies/Details/{OtherCompanyId(ownCompanyId)}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        AssertAccessDenied(other);
    }

    [Fact]
    public async Task Manager_cannot_edit_another_companys_activity()
    {
        var client = await LoginAsManagerAsync();
        var ownCompanyId = CompanyIdOf("csr.manager@impactnexus.local");

        var own = await client.GetAsync($"/Activities/Edit/{OwnActivityId(ownCompanyId)}");
        var other = await client.GetAsync($"/Activities/Edit/{OtherActivityId(ownCompanyId)}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        AssertAccessDenied(other);
    }

    [Fact]
    public async Task Manager_cannot_view_another_companys_donation()
    {
        var client = await LoginAsManagerAsync();
        var ownCompanyId = CompanyIdOf("csr.manager@impactnexus.local");

        var own = await client.GetAsync($"/Donations/Details/{OwnDonationId("investor@impactnexus.local")}");
        var other = await client.GetAsync($"/Donations/Details/{DonationOfOtherCompany(ownCompanyId)}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        AssertAccessDenied(other);
    }

    [Fact]
    public async Task Investor_sees_own_contributions_and_not_other_donors()
    {
        var client = await LoginAsInvestorAsync();

        var own = await client.GetAsync($"/Donations/Details/{OwnDonationId("investor@impactnexus.local")}");
        var other = await client.GetAsync($"/Donations/Details/{OtherDonationId("investor@impactnexus.local")}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        AssertAccessDenied(other);
    }

    [Fact]
    public async Task Investor_cannot_create_donations_or_open_beneficiaries()
    {
        var client = await LoginAsInvestorAsync();

        AssertAccessDenied(await client.GetAsync("/Donations/Create"));
        AssertAccessDenied(await client.GetAsync("/Beneficiaries"));
    }

    [Fact]
    public async Task Community_member_sees_only_their_own_volunteer_hours()
    {
        var client = await LoginAsCommunityMemberAsync();

        var own = await client.GetAsync($"/VolunteerHours/Details/{OwnVolunteerHoursId("community@impactnexus.local")}");
        var other = await client.GetAsync($"/VolunteerHours/Details/{OtherVolunteerHoursId("community@impactnexus.local")}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        AssertAccessDenied(other);
    }

    [Fact]
    public async Task Antiforgery_token_is_required_for_posts()
    {
        var client = await LoginAsAdministratorAsync();

        var response = await client.PostAsync("/Companies/Create", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Name"] = "Tokenless Ltd",
                ["CountryId"] = "1"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_does_not_authenticate()
    {
        var client = CreateClient();
        var token = await GetTokenAsync(client, "/Account/Login");

        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "admin@impactnexus.local",
                ["Password"] = "NotThePassword1!",
                ["RememberMe"] = "false",
                ["ReturnUrl"] = "",
                ["__RequestVerificationToken"] = token
            }));

        var users = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, users.StatusCode);
    }

    private int OtherCompanyId(int companyId) => Query(db =>
        db.Companies.Where(c => c.Id != companyId).OrderBy(c => c.Id).Select(c => c.Id).First());

    private int DonationOfOtherCompany(int companyId) => Query(db =>
        db.Donations.Where(d => d.CompanyId != companyId).OrderBy(d => d.Id).Select(d => d.Id).First());

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }
}

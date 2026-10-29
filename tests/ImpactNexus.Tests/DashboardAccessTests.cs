using System.Net;

namespace ImpactNexus.Tests;

/// <summary>
/// Each role must land on its own dashboard and must be refused every other
/// role's dashboard, even when the URL is typed directly.
/// </summary>
public class DashboardAccessTests : IntegrationTestBase
{
    public DashboardAccessTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Dashboard_root_redirects_each_role_to_its_own_view()
    {
        var cases = new (string Email, string Password, string Action)[]
        {
            ("admin@impactnexus.local", "Admin#2026", "Admin"),
            ("csr.manager@impactnexus.local", "Manager#2026", "Manager"),
            ("investor@impactnexus.local", "Investor#2026", "Investor"),
            ("community@impactnexus.local", "Volunteer#2026", "Community")
        };

        foreach (var (email, password, action) in cases)
        {
            var client = CreateClient();
            await LoginAsync(client, email, password);

            var response = await client.GetAsync("/Dashboard");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains($"/Dashboard/{action}", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("/Dashboard/Admin")]
    [InlineData("/Dashboard/Manager")]
    [InlineData("/Dashboard/Investor")]
    [InlineData("/Dashboard/Community")]
    public async Task Administrator_can_reach_the_system_dashboard(string path)
    {
        var client = await LoginAsAdministratorAsync();

        var response = await client.GetAsync(path);

        // Only the administrative dashboard is own-role; the rest follow policy.
        if (path == "/Dashboard/Admin")
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            AssertDenied(response);
        }
    }

    [Theory]
    [InlineData("/Dashboard/Admin")]
    [InlineData("/Dashboard/Investor")]
    [InlineData("/Dashboard/Community")]
    public async Task Csr_manager_is_refused_other_dashboards(string path)
    {
        var client = await LoginAsManagerAsync();

        var response = await client.GetAsync(path);

        AssertDenied(response);
    }

    [Theory]
    [InlineData("/Dashboard/Admin")]
    [InlineData("/Dashboard/Manager")]
    [InlineData("/Dashboard/Community")]
    public async Task Investor_is_refused_other_dashboards(string path)
    {
        var client = await LoginAsInvestorAsync();

        var response = await client.GetAsync(path);

        AssertDenied(response);
    }

    [Theory]
    [InlineData("/Dashboard/Admin")]
    [InlineData("/Dashboard/Manager")]
    [InlineData("/Dashboard/Investor")]
    public async Task Community_member_is_refused_other_dashboards(string path)
    {
        var client = await LoginAsCommunityMemberAsync();

        var response = await client.GetAsync(path);

        AssertDenied(response);
    }

    [Fact]
    public async Task Community_member_can_open_their_own_dashboard()
    {
        var client = await LoginAsCommunityMemberAsync();

        var response = await client.GetAsync("/Dashboard/Community");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_dashboard_shows_only_their_company_name()
    {
        var client = await LoginAsManagerAsync();
        var ownCompany = Query(db => db.Users
            .Where(u => u.Email == "csr.manager@impactnexus.local")
            .Select(u => u.Company!.Name)
            .Single());
        var otherCompany = Query(db => db.Companies
            .Where(c => c.Name != ownCompany)
            .Select(c => c.Name)
            .First());

        var html = await client.GetStringAsync("/Dashboard/Manager");

        Assert.Contains(ownCompany, html);
        Assert.DoesNotContain(otherCompany, html);
    }

    [Fact]
    public async Task Investor_dashboard_charts_their_own_contributions_only()
    {
        var investorOneId = UserIdOf("investor@impactnexus.local");
        var investorTwoId = UserIdOf("investor2@impactnexus.local");

        var model = await WithServiceAsync<ImpactNexus.Web.Services.DashboardService,
            ImpactNexus.Web.Services.InvestorDashboardViewModel>(
            service => service.GetInvestorDashboardAsync(investorOneId));

        var ownTotal = Query(db => db.Donations
            .Where(d => d.DonorUserId == investorOneId)
            .Sum(d => (decimal?)d.Amount) ?? 0m);
        var otherTotal = Query(db => db.Donations
            .Where(d => d.DonorUserId == investorTwoId)
            .Sum(d => (decimal?)d.Amount) ?? 0m);

        Assert.Equal(ownTotal, model.TotalContributed);
        Assert.Equal(6, model.MonthlyContributions.Count);

        // The chart window is the last six months, shifting back to the newest
        // record when that window is empty, so assert the shape, not the exact dates.
        var charted = model.MonthlyContributions.Sum(p => p.Amount);
        Assert.True(charted > 0, "Investor chart should contain their own contributions.");
        Assert.True(charted <= ownTotal, "Chart cannot exceed the investor's total.");
        Assert.All(model.MonthlyContributions, p => Assert.False(string.IsNullOrWhiteSpace(p.Label)));
        Assert.NotEqual(otherTotal, model.TotalContributed);

        var client = await LoginAsInvestorAsync();
        var html = await client.GetStringAsync("/Dashboard/Investor");
        Assert.Contains("Your contributions, last six months", html);
    }

    [Fact]
    public async Task Community_dashboard_charts_six_months_of_their_own_hours()
    {
        var client = await LoginAsCommunityMemberAsync();

        var html = await client.GetStringAsync("/Dashboard/Community");

        Assert.Contains("Your volunteer hours, last six months", html);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(html, "class=\"bar\"").Count);
    }
}

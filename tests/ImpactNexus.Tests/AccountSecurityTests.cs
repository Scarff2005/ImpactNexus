using System.Net;
using ImpactNexus.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactNexus.Tests;

public class AccountSecurityTests : IntegrationTestBase
{
    public AccountSecurityTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await LoginAsManagerAsync();

        var token = await GetTokenAsync(client, "/Account/Profile");
        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var afterLogout = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains("/Account/Login", afterLogout.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Disabled_account_cannot_sign_in()
    {
        // Disable the second investor, then try the real login form.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "investor2@impactnexus.local");
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        var token = await GetTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "investor2@impactnexus.local",
                ["Password"] = "Investor#2026",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("disabled", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_page_shows_only_the_signed_in_users_details()
    {
        var client = await LoginAsInvestorAsync();
        var own = Query(db => db.Users
            .Where(u => u.Email == "investor@impactnexus.local")
            .Select(u => new { u.FullName, u.Email })
            .Single());
        var other = Query(db => db.Users
            .Where(u => u.Email == "investor2@impactnexus.local")
            .Select(u => new { u.FullName, u.Email })
            .Single());

        var html = await client.GetStringAsync("/Account/Profile");

        Assert.Contains(own.FullName, html);
        Assert.Contains(own.Email!, html);
        Assert.DoesNotContain(other.Email!, html);
    }

    [Fact]
    public async Task Profile_update_cannot_change_email_or_roles()
    {
        var client = await LoginAsInvestorAsync();
        var originalEmail = Query(db => db.Users
            .Where(u => u.Email == "investor@impactnexus.local")
            .Select(u => u.Email)
            .Single());

        var token = await GetTokenAsync(client, "/Account/Profile");
        await client.PostAsync("/Account/Profile", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["FullName"] = "Amara Okafor",
                ["Email"] = "attacker@example.com",
                ["PhoneNumber"] = "+27 11 555 0100",
                ["Roles"] = "IT Administrator",
                ["__RequestVerificationToken"] = token
            }));

        var after = Query(db => db.Users
            .Where(u => u.Id == UserIdOf("investor@impactnexus.local"))
            .Select(u => u.Email)
            .Single());

        Assert.Equal(originalEmail, after);

        var roles = Query(db => db.UserRoles
            .Where(ur => ur.UserId == UserIdOf("investor@impactnexus.local"))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToList());

        Assert.DoesNotContain("IT Administrator", roles);
    }

    [Fact]
    public async Task Community_member_cannot_open_administrative_pages()
    {
        var client = await LoginAsCommunityMemberAsync();

        foreach (var path in new[] { "/Users", "/Companies", "/Locations", "/Countries", "/Towns", "/Provinces" })
        {
            var response = await client.GetAsync(path);
            AssertDenied(response);
        }
    }

    [Fact]
    public async Task Manager_cannot_open_administrative_pages()
    {
        var client = await LoginAsManagerAsync();

        AssertDenied(await client.GetAsync("/Users"));
        AssertDenied(await client.GetAsync("/Countries"));
    }
}

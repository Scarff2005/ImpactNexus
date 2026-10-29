using System.Net;
using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Tests;

/// <summary>
/// Regression cover for the hardening pass: application windows, orphan records,
/// date sanity, session revocation, dropdown scoping and response headers.
/// </summary>
public class HardeningTests : IntegrationTestBase
{
    public HardeningTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private int ClosedProgrammeId() => Query(db => db.BursaryProgrammes
        .Where(p => p.CloseDate < Today)
        .OrderBy(p => p.Id)
        .Select(p => p.Id)
        .First());

    private int OpenProgrammeId() => Query(db => db.BursaryProgrammes
        .Where(p => p.IsActive && p.OpenDate <= Today && p.CloseDate >= Today)
        .OrderBy(p => p.Id)
        .Select(p => p.Id)
        .First());

    private int ActiveProgrammeId() => Query(db => db.VolunteerProgrammes
        .Where(p => p.Status == ProgrammeStatus.Active)
        .OrderBy(p => p.Id)
        .Select(p => p.Id)
        .First());

    private int InProgressActivityId() => Query(db => db.CsrActivities
        .Where(a => a.Status == ActivityStatus.InProgress)
        .OrderBy(a => a.Id)
        .Select(a => a.Id)
        .First());

    private int BeneficiaryCount() => Query(db => db.Beneficiaries.Count());

    private int ApplicationCount() => Query(db => db.BursaryApplications.Count());

    private async Task<HttpResponseMessage> PostApplicationAsync(HttpClient client, int programmeId, string motivation)
    {
        var token = await GetTokenAsync(client, "/BursaryApplications/Apply");
        return await client.PostAsync("/BursaryApplications/Apply", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["bursaryProgrammeId"] = programmeId.ToString(),
                ["motivation"] = motivation,
                ["__RequestVerificationToken"] = token
            }));
    }

    [Fact]
    public async Task Applying_outside_the_programme_window_is_rejected()
    {
        var client = await LoginAsCommunityMemberAsync();
        var before = ApplicationCount();

        var response = await PostApplicationAsync(client, ClosedProgrammeId(),
            "I maintain a 78% average and require funding to continue my diploma.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not accepting applications",
            await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, ApplicationCount());
    }

    [Fact]
    public async Task A_rejected_application_leaves_no_orphan_beneficiary()
    {
        var client = await LoginAsCommunityMemberAsync();
        var before = BeneficiaryCount();

        // Motivation too short, so the submission must fail before any beneficiary row is written.
        var response = await PostApplicationAsync(client, OpenProgrammeId(), "too short");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, BeneficiaryCount());
    }

    [Fact]
    public async Task An_open_programme_accepts_an_application()
    {
        var client = CreateClient();
        await LoginAsync(client, "volunteer@impactnexus.local", "Volunteer#2026");
        var before = ApplicationCount();

        var response = await PostApplicationAsync(client, OpenProgrammeId(),
            "I am a second-year mechanical engineering student with a 74% average and household income below the threshold.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/BursaryApplications", response.Headers.Location!.OriginalString);
        Assert.Equal(before + 1, ApplicationCount());
    }

    [Fact]
    public async Task Applying_twice_to_the_same_programme_is_rejected()
    {
        var client = CreateClient();
        await LoginAsync(client, "community@impactnexus.local", "Volunteer#2026");

        var programmeId = OpenProgrammeId();
        var motivation = "Repeating the same programme should be refused by the duplicate guard.";

        var first = await PostApplicationAsync(client, programmeId, motivation);
        var second = await PostApplicationAsync(client, programmeId, motivation);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("already applied",
            await second.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Volunteer_hours_cannot_be_logged_for_a_future_date()
    {
        var client = await LoginAsCommunityMemberAsync();
        var before = Query(db => db.VolunteerHours.Count());

        var token = await GetTokenAsync(client, "/VolunteerHours/Log");
        var response = await client.PostAsync("/VolunteerHours/Log", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["VolunteerProgrammeId"] = ActiveProgrammeId().ToString(),
                ["CsrActivityId"] = InProgressActivityId().ToString(),
                ["Hours"] = "4",
                ["Date"] = Today.AddDays(1).ToString("yyyy-MM-dd"),
                ["Notes"] = "Logged ahead of time",
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("future date",
            await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Query(db => db.VolunteerHours.Count()));
    }

    [Fact]
    public async Task Security_headers_are_sent_on_every_response()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/Account/Login");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Donor_dropdown_lists_investor_accounts_only()
    {
        var client = await LoginAsManagerAsync();
        var html = await client.GetStringAsync("/Donations/Create");

        Assert.Contains("investor@impactnexus.local", html);
        Assert.DoesNotContain("sipho.dlamini@impactnexus.local", html);
        Assert.DoesNotContain("admin@impactnexus.local", html);
    }

    [Fact]
    public async Task Volunteer_dropdown_lists_community_members_only()
    {
        var client = await LoginAsManagerAsync();
        var html = await client.GetStringAsync("/VolunteerHours/Create");

        Assert.Contains("volunteer@impactnexus.local", html);
        Assert.DoesNotContain("investor@impactnexus.local", html);
    }

    [Fact]
    public async Task Disabling_an_account_rolls_its_security_stamp()
    {
        var email = "csr.manager2@impactnexus.local";
        var stampBefore = Query(db => db.Users.AsNoTracking().Single(u => u.Email == email).SecurityStamp);

        var admin = await LoginAsAdministratorAsync();
        var userId = UserIdOf(email);
        var token = await GetTokenAsync(admin, "/Users");
        var response = await admin.PostAsync("/Users/ToggleActive", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = userId,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var after = Query(db => db.Users.AsNoTracking().Single(u => u.Email == email));
        Assert.False(after.IsActive);
        Assert.NotEqual(stampBefore, after.SecurityStamp);

        // Put the demo data back so later tests still see an active manager.
        var restoreToken = await GetTokenAsync(admin, "/Users");
        await admin.PostAsync("/Users/ToggleActive", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["id"] = userId,
                ["__RequestVerificationToken"] = restoreToken
            }));

        Assert.True(Query(db => db.Users.AsNoTracking().Single(u => u.Email == email).IsActive));
    }

    [Fact]
    public async Task Manager_without_a_company_gets_a_page_not_a_redirect_loop()
    {
        const string email = "unassigned.manager@impactnexus.local";
        const string password = "Manager#2026";

        await WithServiceAsync<UserManager<ApplicationUser>>(async users =>
        {
            if (await users.FindByEmailAsync(email) is not null)
            {
                return;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Unassigned Manager",
                IsActive = true,
                CompanyId = null
            };

            var created = await users.CreateAsync(user, password);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, Roles.CsrManager);
        });

        var client = CreateClient();
        await LoginAsync(client, email, password);

        var response = await client.GetAsync("/Dashboard/Manager");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not linked to a company",
            await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}

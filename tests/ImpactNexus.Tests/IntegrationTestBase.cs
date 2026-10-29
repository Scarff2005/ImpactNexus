using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ImpactNexus.Tests;

/// <summary>
/// Shared fixture: one application instance and one seeded database per test class,
/// plus browser-like helpers (cookies, antiforgery tokens, form posts).
/// </summary>
public abstract partial class IntegrationTestBase : IClassFixture<ImpactNexusFactory>
{
    private static readonly Regex TokenPattern = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
        RegexOptions.Compiled);

    protected ImpactNexusFactory Factory { get; }

    protected IntegrationTestBase(ImpactNexusFactory factory)
    {
        Factory = factory;
        Factory.InitialiseDatabase();
    }

    protected HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    protected static string ExtractToken(string html)
    {
        var match = TokenPattern.Match(html);
        Assert.True(match.Success, "Antiforgery token not found in the rendered form.");
        return match.Groups[1].Value;
    }

    protected static async Task<string> GetTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return ExtractToken(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Signs in and returns the same client, now carrying the auth cookie.</summary>
    protected static async Task<HttpClient> LoginAsync(HttpClient client, string email, string password)
    {
        var token = await GetTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = "false",
                ["ReturnUrl"] = "",
                ["__RequestVerificationToken"] = token
            }));

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Login for {email} returned {(int)response.StatusCode}.");

        return client;
    }

    protected async Task<HttpClient> LoginAsAdministratorAsync()
    {
        var client = CreateClient();
        return await LoginAsync(client, "admin@impactnexus.local", "Admin#2026");
    }

    protected async Task<HttpClient> LoginAsManagerAsync()
    {
        var client = CreateClient();
        return await LoginAsync(client, "csr.manager@impactnexus.local", "Manager#2026");
    }

    protected async Task<HttpClient> LoginAsSecondManagerAsync()
    {
        var client = CreateClient();
        return await LoginAsync(client, "csr.manager2@impactnexus.local", "Manager#2026");
    }

    protected async Task<HttpClient> LoginAsInvestorAsync()
    {
        var client = CreateClient();
        return await LoginAsync(client, "investor@impactnexus.local", "Investor#2026");
    }

    protected async Task<HttpClient> LoginAsCommunityMemberAsync()
    {
        var client = CreateClient();
        return await LoginAsync(client, "community@impactnexus.local", "Volunteer#2026");
    }
}

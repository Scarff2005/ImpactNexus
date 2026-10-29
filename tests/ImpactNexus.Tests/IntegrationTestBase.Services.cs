using Microsoft.Extensions.DependencyInjection;

namespace ImpactNexus.Tests;

public abstract partial class IntegrationTestBase
{
    /// <summary>
    /// A denied request may surface as 403 or as the cookie middleware's redirect
    /// to the access-denied page; both mean the resource was refused.
    /// </summary>
    protected static void AssertDenied(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return;
        }

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    /// <summary>
    /// Resolves a scoped application service (reports, dashboards) against the same
    /// database the HTTP pipeline is using, for calculation-level assertions.
    /// </summary>
    protected async Task<TResult> WithServiceAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await action(service);
    }

    protected async Task WithServiceAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        await action(service);
    }
}

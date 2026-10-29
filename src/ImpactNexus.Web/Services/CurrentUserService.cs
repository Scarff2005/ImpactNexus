using System.Security.Claims;
using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace ImpactNexus.Web.Services;

public static class AppClaims
{
    public const string CompanyId = "company_id";
    public const string FullName = "full_name";
}

/// <summary>
/// Adds the company claim at sign-in so company scoping never needs a per-request
/// database round trip. Company changes for a signed-in user require re-login.
/// </summary>
/// <remarks>
/// GenerateClaimsAsync is implemented here rather than calling the framework base:
/// the base implementation reads per-user and per-role claims from Identity's
/// satellite tables, which this application removes from the model because it never
/// writes to them. Everything the cookie actually needs is built directly.
/// </remarks>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(
            "Identity.Application",
            Options.ClaimsIdentity.UserNameClaimType,
            Options.ClaimsIdentity.RoleClaimType);

        identity.AddClaim(new Claim(Options.ClaimsIdentity.UserIdClaimType, await UserManager.GetUserIdAsync(user)));
        identity.AddClaim(new Claim(
            Options.ClaimsIdentity.UserNameClaimType,
            await UserManager.GetUserNameAsync(user) ?? string.Empty));

        if (UserManager.SupportsUserEmail)
        {
            var email = await UserManager.GetEmailAsync(user);
            if (!string.IsNullOrEmpty(email))
            {
                identity.AddClaim(new Claim(Options.ClaimsIdentity.EmailClaimType, email));
            }
        }

        if (UserManager.SupportsUserSecurityStamp)
        {
            identity.AddClaim(new Claim(
                Options.ClaimsIdentity.SecurityStampClaimType,
                await UserManager.GetSecurityStampAsync(user)));
        }

        if (UserManager.SupportsUserRole)
        {
            foreach (var roleName in await UserManager.GetRolesAsync(user))
            {
                identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, roleName));
            }
        }

        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));
        if (user.CompanyId.HasValue)
        {
            identity.AddClaim(new Claim(AppClaims.CompanyId, user.CompanyId.Value.ToString()));
        }

        return identity;
    }
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? FullName { get; }
    int? CompanyId { get; }
    bool IsAuthenticated { get; }
    bool IsAdministrator { get; }
    bool IsCsrManager { get; }
    bool IsInvestor { get; }
    bool IsCommunityMember { get; }
    bool IsStaff { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? FullName => User?.FindFirstValue(AppClaims.FullName);

    public int? CompanyId
    {
        get
        {
            var value = User?.FindFirstValue(AppClaims.CompanyId);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public bool IsAdministrator => User?.IsInRole(Roles.Administrator) == true;
    public bool IsCsrManager => User?.IsInRole(Roles.CsrManager) == true;
    public bool IsInvestor => User?.IsInRole(Roles.Investor) == true;
    public bool IsCommunityMember => User?.IsInRole(Roles.CommunityMember) == true;
    public bool IsStaff => IsAdministrator || IsCsrManager;
}

using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace ImpactNexus.Web.Services;

/// <summary>
/// Single source of truth for company-level data isolation. Every controller that
/// touches company-owned records must call one of these before reading or writing.
/// </summary>
public interface ICompanyAccessService
{
    /// <summary>Company the signed-in user may act within, or null when unrestricted / none.</summary>
    int? ScopedCompanyId { get; }

    bool CanAccessCompany(int companyId);
    bool IsUnrestricted { get; }
}

public class CompanyAccessService : ICompanyAccessService
{
    private readonly ICurrentUserService _currentUser;

    public CompanyAccessService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public int? ScopedCompanyId => _currentUser.IsCsrManager ? _currentUser.CompanyId : null;

    public bool IsUnrestricted => _currentUser.IsAdministrator;

    public bool CanAccessCompany(int companyId)
    {
        if (_currentUser.IsAdministrator)
        {
            return true;
        }

        return _currentUser.IsCsrManager && _currentUser.CompanyId == companyId;
    }
}

public static class CompanyAccessRequirement
{
    public const string Name = "CompanyAccess";
}

/// <summary>
/// Resource-based authorization for a specific company record.
/// </summary>
public class CompanyAccessHandler : AuthorizationHandler<CompanyAccessHandler.Requirement, Company>
{
    private readonly ICompanyAccessService _companyAccess;

    public CompanyAccessHandler(ICompanyAccessService companyAccess)
    {
        _companyAccess = companyAccess;
    }

    public class Requirement : IAuthorizationRequirement
    {
        public static readonly Requirement Instance = new();
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        Requirement requirement,
        Company resource)
    {
        if (context.User.IsInRole(Roles.Administrator) ||
            _companyAccess.CanAccessCompany(resource.Id))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

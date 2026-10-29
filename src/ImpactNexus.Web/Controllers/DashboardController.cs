using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(DashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    public IActionResult Index()
    {
        if (_currentUser.IsAdministrator)
        {
            return RedirectToAction(nameof(Admin));
        }

        if (_currentUser.IsCsrManager)
        {
            return RedirectToAction(nameof(Manager));
        }

        if (_currentUser.IsInvestor)
        {
            return RedirectToAction(nameof(Investor));
        }

        if (_currentUser.IsCommunityMember)
        {
            return RedirectToAction(nameof(Community));
        }

        return Forbid();
    }

    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Admin()
    {
        var model = await _dashboard.GetAdminDashboardAsync();
        return View(model);
    }

    [Authorize(Policy = Policies.CsrManagerOnly)]
    public async Task<IActionResult> Manager()
    {
        if (_currentUser.CompanyId is not int companyId)
        {
            // Returning a view rather than redirecting keeps this from looping back
            // through Index(), which would send the manager straight back here.
            return View("NoCompany");
        }

        var model = await _dashboard.GetManagerDashboardAsync(companyId);
        return View(model);
    }

    [Authorize(Policy = Policies.InvestorOnly)]
    public async Task<IActionResult> Investor()
    {
        var model = await _dashboard.GetInvestorDashboardAsync(_currentUser.UserId!);
        return View(model);
    }

    [Authorize(Policy = Policies.CommunityOnly)]
    public async Task<IActionResult> Community()
    {
        var model = await _dashboard.GetCommunityDashboardAsync(_currentUser.UserId!);
        return View(model);
    }
}

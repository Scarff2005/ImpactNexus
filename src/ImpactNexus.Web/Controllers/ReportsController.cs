using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportService _reports;
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(
        ReportService reports,
        ApplicationDbContext db,
        ICompanyAccessService companyAccess,
        ICurrentUserService currentUser)
    {
        _reports = reports;
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Companies = await VisibleCompaniesAsync();
        return View();
    }

    public async Task<IActionResult> Activities(ReportFilters filters, bool export = false)
    {
        ScopeCompany(ref filters);
        var rows = await _reports.GetActivityReportAsync(filters);
        if (export)
        {
            return Csv(rows, "csr-activities");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.TotalBudget = rows.Sum(r => r.Budget);
        return View(rows);
    }

    public async Task<IActionResult> Contributions(ReportFilters filters, bool export = false)
    {
        if (_currentUser.IsCommunityMember)
        {
            return Forbid();
        }

        if (_currentUser.IsInvestor)
        {
            filters.UserId = _currentUser.UserId;
        }

        ScopeCompany(ref filters);
        var rows = await _reports.GetContributionReportAsync(filters);
        if (export)
        {
            return Csv(rows, "company-contributions");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.Total = rows.Sum(r => r.Total);
        return View(rows);
    }

    public async Task<IActionResult> Donations(ReportFilters filters, bool export = false)
    {
        if (_currentUser.IsCommunityMember)
        {
            return Forbid();
        }

        if (_currentUser.IsInvestor)
        {
            filters.UserId = _currentUser.UserId;
        }

        ScopeCompany(ref filters);
        var rows = await _reports.GetDonationReportAsync(filters);
        if (export)
        {
            return Csv(rows, "donation-history");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.Total = rows.Sum(r => r.Amount);
        ViewBag.Statuses = Enum.GetNames<DonationStatus>();
        return View(rows);
    }

    public async Task<IActionResult> Bursaries(ReportFilters filters, bool export = false)
    {
        if (_currentUser.IsInvestor)
        {
            var fundedCompanyIds = await _db.Donations
                .Where(d => d.DonorUserId == _currentUser.UserId)
                .Select(d => d.CompanyId)
                .Distinct()
                .ToListAsync();

            if (filters.CompanyId.HasValue && !fundedCompanyIds.Contains(filters.CompanyId.Value))
            {
                return Forbid();
            }

            ViewBag.RestrictedCompanyIds = fundedCompanyIds;
        }

        ScopeCompany(ref filters);

        if (_currentUser.IsCommunityMember)
        {
            filters.UserId = _currentUser.UserId;
        }

        var rows = await _reports.GetBursaryReportAsync(filters);

        if (_currentUser.IsInvestor)
        {
            var fundedCompanies = await _db.Companies
                .Where(c => _db.Donations.Any(d => d.DonorUserId == _currentUser.UserId && d.CompanyId == c.Id))
                .Select(c => c.Name)
                .ToListAsync();
            rows = rows.Where(r => fundedCompanies.Contains(r.Company)).ToList();
        }

        if (export)
        {
            return Csv(rows, "bursary-summary");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.TotalAwarded = rows.Sum(r => r.AmountAwarded ?? 0m);
        ViewBag.Statuses = Enum.GetNames<BursaryApplicationStatus>();
        return View(rows);
    }

    public async Task<IActionResult> VolunteerHours(ReportFilters filters, bool export = false)
    {
        if (_currentUser.IsInvestor)
        {
            return Forbid();
        }

        ScopeCompany(ref filters);
        if (_currentUser.IsCommunityMember)
        {
            filters.UserId = _currentUser.UserId;
        }

        var rows = await _reports.GetVolunteerHoursReportAsync(filters);
        if (export)
        {
            return Csv(rows, "volunteer-hours");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.TotalHours = rows.Sum(r => r.Hours);
        ViewBag.ApprovedHours = rows.Where(r => r.Approved).Sum(r => r.Hours);
        return View(rows);
    }

    public async Task<IActionResult> Beneficiaries(ReportFilters filters, bool export = false)
    {
        if (!_currentUser.IsStaff)
        {
            return Forbid();
        }

        ScopeCompany(ref filters);
        var rows = await _reports.GetBeneficiaryStatsAsync(filters);
        if (export)
        {
            return Csv(rows, "beneficiary-statistics");
        }

        await PopulateAsync();
        ViewBag.Filters = filters;
        ViewBag.TotalBeneficiaries = rows.Sum(r => r.BeneficiaryCount);
        return View(rows);
    }

    private void ScopeCompany(ref ReportFilters filters)
    {
        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            filters.CompanyId = scoped;
        }
    }

    private FileContentResult Csv<T>(IEnumerable<T> rows, string name)
    {
        var csv = CsvExport.Write(rows);
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv", $"{name}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task<List<Company>> VisibleCompaniesAsync()
    {
        if (_currentUser.IsInvestor)
        {
            return await _db.Companies
                .Where(c => _db.Donations.Any(d => d.DonorUserId == _currentUser.UserId && d.CompanyId == c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            return await _db.Companies.Where(c => c.Id == scoped).ToListAsync();
        }

        return await _db.Companies.OrderBy(c => c.Name).ToListAsync();
    }

    private async Task PopulateAsync()
    {
        ViewBag.Companies = await VisibleCompaniesAsync();
    }
}

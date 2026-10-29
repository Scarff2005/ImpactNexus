using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class DonationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public DonationsController(ApplicationDbContext db, ICompanyAccessService companyAccess, ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, DonationStatus? status, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        IQueryable<Donation> query;

        if (_currentUser.IsAdministrator)
        {
            query = _db.Donations.AsNoTracking();
        }
        else if (_currentUser.IsCsrManager && _currentUser.CompanyId is int companyId)
        {
            query = _db.Donations.Where(d => d.CompanyId == companyId).AsNoTracking();
        }
        else if (_currentUser.IsInvestor)
        {
            query = _db.Donations.Where(d => d.DonorUserId == _currentUser.UserId).AsNoTracking();
        }
        else
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.DonorName.Contains(term) || d.Description.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var total = await query.CountAsync();
        var rows = await query
            .Include(d => d.Company)
            .Include(d => d.CsrActivity)
            .OrderByDescending(d => d.DonatedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CanManage = _currentUser.IsStaff;
        ViewBag.Total = await query.Where(d => d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated)
            .SumAsync(d => (decimal?)d.Amount) ?? 0m;

        return View(PagedList<Donation>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var donation = await _db.Donations
            .Include(d => d.Company)
            .Include(d => d.CsrActivity)
            .Include(d => d.DonorUser)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (donation is null)
        {
            return NotFound();
        }

        if (!CanView(donation))
        {
            return Forbid();
        }

        return View(donation);
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new Donation
        {
            CompanyId = _companyAccess.ScopedCompanyId ?? 0,
            DonatedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create(Donation model)
    {
        if (!_companyAccess.CanAccessCompany(model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only record donations for your own company.");
        }

        if (model.CsrActivityId.HasValue &&
            !await _db.CsrActivities.AnyAsync(a => a.Id == model.CsrActivityId && a.CompanyId == model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CsrActivityId), "Select an activity belonging to the same company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.Donations.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Donation recorded.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(donation.CompanyId))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(donation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id, Donation model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var donation = await _db.Donations.FindAsync(id);
        if (donation is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(donation.CompanyId) ||
            !_companyAccess.CanAccessCompany(model.CompanyId))
        {
            return Forbid();
        }

        if (model.CsrActivityId.HasValue &&
            !await _db.CsrActivities.AnyAsync(a => a.Id == model.CsrActivityId && a.CompanyId == model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CsrActivityId), "Select an activity belonging to the same company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        donation.DonorName = model.DonorName;
        donation.CompanyId = model.CompanyId;
        donation.DonorUserId = model.DonorUserId;
        donation.CsrActivityId = model.CsrActivityId;
        donation.Amount = model.Amount;
        donation.DonatedOn = model.DonatedOn;
        donation.Description = model.Description;
        donation.Status = model.Status;
        donation.Reference = model.Reference;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Donation updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var donation = await _db.Donations.Include(d => d.Company).FirstOrDefaultAsync(d => d.Id == id);
        if (donation is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(donation.CompanyId))
        {
            return Forbid();
        }

        return View(donation);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(donation.CompanyId))
        {
            return Forbid();
        }

        _db.Donations.Remove(donation);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Donation deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanView(Donation donation)
    {
        if (_currentUser.IsAdministrator)
        {
            return true;
        }

        if (_currentUser.IsCsrManager)
        {
            return _companyAccess.CanAccessCompany(donation.CompanyId);
        }

        return _currentUser.IsInvestor && donation.DonorUserId == _currentUser.UserId;
    }

    private async Task PopulateListsAsync()
    {
        var companies = _currentUser.IsAdministrator
            ? await _db.Companies.OrderBy(c => c.Name).ToListAsync()
            : await _db.Companies.Where(c => c.Id == _companyAccess.ScopedCompanyId).ToListAsync();

        ViewBag.Companies = companies;
        ViewBag.Activities = await _db.CsrActivities
            .Where(a => companies.Select(c => c.Id).Contains(a.CompanyId))
            .OrderBy(a => a.Title)
            .ToListAsync();

        // Only investor accounts belong in the donor dropdown; listing every active user
        // exposed other people's names and allowed nonsensical links.
        ViewBag.Investors = await _db.Users
            .Where(u => u.IsActive && _db.UserRoles
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .Any(x => x.UserId == u.Id && x.Name == Roles.Investor))
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}

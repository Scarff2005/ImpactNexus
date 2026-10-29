using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.StaffOnly)]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public ActivitiesController(ApplicationDbContext db, ICompanyAccessService companyAccess, ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, ActivityStatus? status, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = ScopedActivities();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Description.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync();
        var rows = await query
            .Include(a => a.Company)
            .Include(a => a.Location)
            .OrderByDescending(a => a.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        return View(PagedList<CsrActivity>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var activity = await _db.CsrActivities
            .Include(a => a.Company)
            .Include(a => a.Location).ThenInclude(l => l!.Town).ThenInclude(t => t!.Province)
            .Include(a => a.Manager)
            .Include(a => a.Donations)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(activity.CompanyId))
        {
            return Forbid();
        }

        return View(activity);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new CsrActivity
        {
            CompanyId = _companyAccess.ScopedCompanyId ?? 0,
            ManagerId = _currentUser.IsCsrManager ? _currentUser.UserId : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CsrActivity model)
    {
        if (!_companyAccess.CanAccessCompany(model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only create activities for your own company.");
        }

        if (model.EndDate.HasValue && model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date cannot be before the start date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.CsrActivities.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "CSR activity created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var activity = await _db.CsrActivities.FindAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(activity.CompanyId))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CsrActivity model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var activity = await _db.CsrActivities.FindAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(activity.CompanyId) ||
            !_companyAccess.CanAccessCompany(model.CompanyId))
        {
            return Forbid();
        }

        if (model.EndDate.HasValue && model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date cannot be before the start date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        activity.Title = model.Title;
        activity.Description = model.Description;
        activity.Objectives = model.Objectives;
        activity.CompanyId = model.CompanyId;
        activity.LocationId = model.LocationId;
        activity.StartDate = model.StartDate;
        activity.EndDate = model.EndDate;
        activity.Budget = model.Budget;
        activity.Status = model.Status;
        activity.ImpactIndicator = model.ImpactIndicator;
        activity.ManagerId = model.ManagerId;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Activity updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _db.CsrActivities
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(activity.CompanyId))
        {
            return Forbid();
        }

        return View(activity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _db.CsrActivities.FindAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(activity.CompanyId))
        {
            return Forbid();
        }

        if (await _db.Donations.AnyAsync(d => d.CsrActivityId == id) ||
            await _db.VolunteerHours.AnyAsync(v => v.CsrActivityId == id))
        {
            TempData["Error"] = "This activity has linked donations or volunteer hours. Cancel it instead of deleting it.";
            return RedirectToAction(nameof(Index));
        }

        _db.CsrActivities.Remove(activity);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Activity deleted.";
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<CsrActivity> ScopedActivities()
    {
        var query = _db.CsrActivities.AsNoTracking().AsQueryable();
        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(a => a.CompanyId == scoped);
        }
        return query;
    }

    private async Task PopulateListsAsync()
    {
        ViewBag.Companies = _currentUser.IsAdministrator
            ? await _db.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync()
            : await _db.Companies.Where(c => c.Id == _companyAccess.ScopedCompanyId).ToListAsync();

        ViewBag.Locations = await _db.Locations
            .Include(l => l.Town).ThenInclude(t => t!.Province)
            .OrderBy(l => l.Name)
            .ToListAsync();

        ViewBag.Managers = await _db.Users
            .Where(u => u.CompanyId == _companyAccess.ScopedCompanyId || _currentUser.IsAdministrator)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}

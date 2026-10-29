using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class VolunteerProgrammesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public VolunteerProgrammesController(
        ApplicationDbContext db,
        ICompanyAccessService companyAccess,
        ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, ProgrammeStatus? status)
    {
        var query = _db.VolunteerProgrammes
            .Include(p => p.Company)
            .Include(p => p.Location)
            .AsNoTracking()
            .AsQueryable();

        if (_currentUser.IsCsrManager && _companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(p => p.CompanyId == scoped);
        }
        else if (_currentUser.IsCommunityMember)
        {
            query = query.Where(p => p.Status == ProgrammeStatus.Active || p.Status == ProgrammeStatus.Planned);
        }
        else if (!_currentUser.IsStaff)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Description.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CanManage = _currentUser.IsStaff;
        return View(await query.OrderByDescending(p => p.StartDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var programme = await _db.VolunteerProgrammes
            .Include(p => p.Company)
            .Include(p => p.Location).ThenInclude(l => l!.Town)
            .Include(p => p.VolunteerHours).ThenInclude(v => v.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (programme is null)
        {
            return NotFound();
        }

        var canView = _currentUser.IsAdministrator ||
                      (_currentUser.IsCsrManager && _companyAccess.CanAccessCompany(programme.CompanyId)) ||
                      (_currentUser.IsCommunityMember &&
                       (programme.Status == ProgrammeStatus.Active || programme.Status == ProgrammeStatus.Planned));

        if (!canView)
        {
            return Forbid();
        }

        ViewBag.CanManage = _currentUser.IsStaff;
        return View(programme);
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new VolunteerProgramme { CompanyId = _companyAccess.ScopedCompanyId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create(VolunteerProgramme model)
    {
        if (!_companyAccess.CanAccessCompany(model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only create programmes for your own company.");
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

        _db.VolunteerProgrammes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer programme created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var programme = await _db.VolunteerProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(programme);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id, VolunteerProgramme model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var programme = await _db.VolunteerProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId) ||
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

        programme.Name = model.Name;
        programme.Description = model.Description;
        programme.CompanyId = model.CompanyId;
        programme.LocationId = model.LocationId;
        programme.StartDate = model.StartDate;
        programme.EndDate = model.EndDate;
        programme.Status = model.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer programme updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var programme = await _db.VolunteerProgrammes
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        return View(programme);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var programme = await _db.VolunteerProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        if (await _db.VolunteerHours.AnyAsync(v => v.VolunteerProgrammeId == id))
        {
            TempData["Error"] = "This programme has logged volunteer hours. Close it instead of deleting it.";
            return RedirectToAction(nameof(Index));
        }

        _db.VolunteerProgrammes.Remove(programme);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer programme deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateListsAsync()
    {
        ViewBag.Companies = _companyAccess.ScopedCompanyId is int scoped
            ? await _db.Companies.Where(c => c.Id == scoped).ToListAsync()
            : await _db.Companies.OrderBy(c => c.Name).ToListAsync();

        ViewBag.Locations = await _db.Locations.Include(l => l.Town).OrderBy(l => l.Name).ToListAsync();
    }
}

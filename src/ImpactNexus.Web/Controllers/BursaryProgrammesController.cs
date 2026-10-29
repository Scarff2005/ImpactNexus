using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.StaffOnly)]
public class BursaryProgrammesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;

    public BursaryProgrammesController(ApplicationDbContext db, ICompanyAccessService companyAccess)
    {
        _db = db;
        _companyAccess = companyAccess;
    }

    public async Task<IActionResult> Index(string? search, bool? active)
    {
        var query = _db.BursaryProgrammes
            .Include(b => b.Company)
            .AsNoTracking()
            .AsQueryable();

        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(b => b.CompanyId == scoped);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.Name.Contains(term) || b.Description.Contains(term));
        }

        if (active.HasValue)
        {
            query = query.Where(b => b.IsActive == active.Value);
        }

        ViewBag.Search = search;
        ViewBag.Active = active;
        return View(await query.OrderByDescending(b => b.AcademicYear).ThenBy(b => b.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var programme = await _db.BursaryProgrammes
            .Include(b => b.Company)
            .Include(b => b.Applications).ThenInclude(a => a.Beneficiary)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        ViewBag.Awarded = programme.Applications
            .Where(a => a.Status == BursaryApplicationStatus.Approved)
            .Sum(a => a.AmountAwarded ?? 0m);
        return View(programme);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateCompaniesAsync();
        return View(new BursaryProgramme
        {
            CompanyId = _companyAccess.ScopedCompanyId ?? 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BursaryProgramme model)
    {
        if (!_companyAccess.CanAccessCompany(model.CompanyId))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only create programmes for your own company.");
        }

        if (model.CloseDate < model.OpenDate)
        {
            ModelState.AddModelError(nameof(model.CloseDate), "Close date cannot be before the open date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCompaniesAsync();
            return View(model);
        }

        _db.BursaryProgrammes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary programme created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var programme = await _db.BursaryProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        await PopulateCompaniesAsync();
        return View(programme);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BursaryProgramme model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var programme = await _db.BursaryProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId) ||
            !_companyAccess.CanAccessCompany(model.CompanyId))
        {
            return Forbid();
        }

        if (model.CloseDate < model.OpenDate)
        {
            ModelState.AddModelError(nameof(model.CloseDate), "Close date cannot be before the open date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCompaniesAsync();
            return View(model);
        }

        programme.Name = model.Name;
        programme.Description = model.Description;
        programme.CompanyId = model.CompanyId;
        programme.AcademicYear = model.AcademicYear;
        programme.Budget = model.Budget;
        programme.OpenDate = model.OpenDate;
        programme.CloseDate = model.CloseDate;
        programme.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary programme updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var programme = await _db.BursaryProgrammes
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == id);

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
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var programme = await _db.BursaryProgrammes.FindAsync(id);
        if (programme is null)
        {
            return NotFound();
        }

        if (!_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            return Forbid();
        }

        if (await _db.BursaryApplications.AnyAsync(a => a.BursaryProgrammeId == id))
        {
            TempData["Error"] = "This programme has applications. Close it instead of deleting it.";
            return RedirectToAction(nameof(Index));
        }

        _db.BursaryProgrammes.Remove(programme);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary programme deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCompaniesAsync()
    {
        ViewBag.Companies = _companyAccess.ScopedCompanyId is int scoped
            ? await _db.Companies.Where(c => c.Id == scoped).ToListAsync()
            : await _db.Companies.OrderBy(c => c.Name).ToListAsync();
    }
}

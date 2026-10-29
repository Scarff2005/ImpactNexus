using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.StaffOnly)]
public class StakeholdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;

    public StakeholdersController(ApplicationDbContext db, ICompanyAccessService companyAccess)
    {
        _db = db;
        _companyAccess = companyAccess;
    }

    public async Task<IActionResult> Index(string? search, StakeholderType? type, int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        var query = _db.Stakeholders
            .Include(s => s.Company)
            .AsNoTracking()
            .AsQueryable();

        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(s => s.CompanyId == scoped);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.FullName.Contains(term) ||
                                     (s.Email != null && s.Email.Contains(term)));
        }

        if (type.HasValue)
        {
            query = query.Where(s => s.Type == type.Value);
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(s => s.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Type = type;
        return View(PagedList<Stakeholder>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var stakeholder = await _db.Stakeholders
            .Include(s => s.Company)
            .Include(s => s.Beneficiaries)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (stakeholder is null)
        {
            return NotFound();
        }

        if (stakeholder.CompanyId.HasValue && !_companyAccess.CanAccessCompany(stakeholder.CompanyId.Value))
        {
            return Forbid();
        }

        return View(stakeholder);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new Stakeholder
        {
            CompanyId = _companyAccess.ScopedCompanyId,
            Type = StakeholderType.CommunityMember
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Stakeholder model)
    {
        if (model.CompanyId.HasValue && !_companyAccess.CanAccessCompany(model.CompanyId.Value))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only add stakeholders for your own company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        _db.Stakeholders.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Stakeholder created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var stakeholder = await _db.Stakeholders.FindAsync(id);
        if (stakeholder is null)
        {
            return NotFound();
        }

        if (stakeholder.CompanyId.HasValue && !_companyAccess.CanAccessCompany(stakeholder.CompanyId.Value))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(stakeholder);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Stakeholder model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var stakeholder = await _db.Stakeholders.FindAsync(id);
        if (stakeholder is null)
        {
            return NotFound();
        }

        if ((stakeholder.CompanyId.HasValue && !_companyAccess.CanAccessCompany(stakeholder.CompanyId.Value)) ||
            (model.CompanyId.HasValue && !_companyAccess.CanAccessCompany(model.CompanyId.Value)))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        stakeholder.FullName = model.FullName;
        stakeholder.Email = model.Email;
        stakeholder.Phone = model.Phone;
        stakeholder.Type = model.Type;
        stakeholder.CompanyId = model.CompanyId;
        stakeholder.Notes = model.Notes;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Stakeholder updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var stakeholder = await _db.Stakeholders.Include(s => s.Company).FirstOrDefaultAsync(s => s.Id == id);
        if (stakeholder is null)
        {
            return NotFound();
        }

        if (stakeholder.CompanyId.HasValue && !_companyAccess.CanAccessCompany(stakeholder.CompanyId.Value))
        {
            return Forbid();
        }

        return View(stakeholder);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var stakeholder = await _db.Stakeholders.FindAsync(id);
        if (stakeholder is null)
        {
            return NotFound();
        }

        if (stakeholder.CompanyId.HasValue && !_companyAccess.CanAccessCompany(stakeholder.CompanyId.Value))
        {
            return Forbid();
        }

        _db.Stakeholders.Remove(stakeholder);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Stakeholder deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateListsAsync()
    {
        ViewBag.Companies = _companyAccess.ScopedCompanyId is int scoped
            ? await _db.Companies.Where(c => c.Id == scoped).ToListAsync()
            : await _db.Companies.OrderBy(c => c.Name).ToListAsync();
    }
}

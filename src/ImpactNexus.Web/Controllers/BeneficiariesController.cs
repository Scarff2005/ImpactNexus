using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.StaffOnly)]
public class BeneficiariesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;

    public BeneficiariesController(ApplicationDbContext db, ICompanyAccessService companyAccess)
    {
        _db = db;
        _companyAccess = companyAccess;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = _db.Beneficiaries
            .Include(b => b.Company)
            .Include(b => b.Town)
            .AsNoTracking()
            .AsQueryable();

        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(b => b.CompanyId == scoped);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.FullName.Contains(term) || (b.Email != null && b.Email.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(b => b.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        return View(PagedList<Beneficiary>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var beneficiary = await _db.Beneficiaries
            .Include(b => b.Company)
            .Include(b => b.Town)
            .Include(b => b.Applications).ThenInclude(a => a.BursaryProgramme)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (beneficiary is null)
        {
            return NotFound();
        }

        if (beneficiary.CompanyId.HasValue && !_companyAccess.CanAccessCompany(beneficiary.CompanyId.Value))
        {
            return Forbid();
        }

        return View(beneficiary);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new Beneficiary { CompanyId = _companyAccess.ScopedCompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Beneficiary model)
    {
        if (model.CompanyId.HasValue && !_companyAccess.CanAccessCompany(model.CompanyId.Value))
        {
            ModelState.AddModelError(nameof(model.CompanyId), "You may only create beneficiaries for your own company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.Beneficiaries.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Beneficiary created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var beneficiary = await _db.Beneficiaries.FindAsync(id);
        if (beneficiary is null)
        {
            return NotFound();
        }

        if (beneficiary.CompanyId.HasValue && !_companyAccess.CanAccessCompany(beneficiary.CompanyId.Value))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(beneficiary);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Beneficiary model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var beneficiary = await _db.Beneficiaries.FindAsync(id);
        if (beneficiary is null)
        {
            return NotFound();
        }

        if ((beneficiary.CompanyId.HasValue && !_companyAccess.CanAccessCompany(beneficiary.CompanyId.Value)) ||
            (model.CompanyId.HasValue && !_companyAccess.CanAccessCompany(model.CompanyId.Value)))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        beneficiary.FullName = model.FullName;
        beneficiary.Email = model.Email;
        beneficiary.Phone = model.Phone;
        beneficiary.TownId = model.TownId;
        beneficiary.CompanyId = model.CompanyId;
        beneficiary.Notes = model.Notes;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Beneficiary updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var beneficiary = await _db.Beneficiaries.Include(b => b.Company).FirstOrDefaultAsync(b => b.Id == id);
        if (beneficiary is null)
        {
            return NotFound();
        }

        if (beneficiary.CompanyId.HasValue && !_companyAccess.CanAccessCompany(beneficiary.CompanyId.Value))
        {
            return Forbid();
        }

        return View(beneficiary);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var beneficiary = await _db.Beneficiaries.FindAsync(id);
        if (beneficiary is null)
        {
            return NotFound();
        }

        if (beneficiary.CompanyId.HasValue && !_companyAccess.CanAccessCompany(beneficiary.CompanyId.Value))
        {
            return Forbid();
        }

        if (await _db.BursaryApplications.AnyAsync(a => a.BeneficiaryId == id))
        {
            TempData["Error"] = "This beneficiary has bursary applications and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Beneficiaries.Remove(beneficiary);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Beneficiary deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateListsAsync()
    {
        ViewBag.Companies = _companyAccess.ScopedCompanyId is int scoped
            ? await _db.Companies.Where(c => c.Id == scoped).ToListAsync()
            : await _db.Companies.OrderBy(c => c.Name).ToListAsync();

        ViewBag.Towns = await _db.Towns.Include(t => t.Province).OrderBy(t => t.Name).ToListAsync();
    }
}

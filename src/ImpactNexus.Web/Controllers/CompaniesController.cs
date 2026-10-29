using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.StaffOnly)]
public class CompaniesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public CompaniesController(
        ApplicationDbContext db,
        ICompanyAccessService companyAccess,
        IAuthorizationService authorization,
        ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, bool? active)
    {
        var query = _db.Companies.Include(c => c.Country).AsNoTracking().AsQueryable();

        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            query = query.Where(c => c.Id == scoped);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) ||
                                     (c.RegistrationNumber != null && c.RegistrationNumber.Contains(term)));
        }

        if (active.HasValue)
        {
            query = query.Where(c => c.IsActive == active.Value);
        }

        ViewBag.Search = search;
        ViewBag.Active = active;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var company = await _db.Companies
            .Include(c => c.Country)
            .Include(c => c.Activities)
            .Include(c => c.Donations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company is null)
        {
            return NotFound();
        }

        // Resource-based authorization: checks the actual company record, not the URL.
        var auth = await _authorization.AuthorizeAsync(User, company, new CompanyAccessHandler.Requirement());
        if (!auth.Succeeded)
        {
            return Forbid();
        }

        ViewBag.DonationTotal = company.Donations
            .Where(d => d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated)
            .Sum(d => d.Amount);
        return View(company);
    }

    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create()
    {
        await PopulateCountriesAsync();
        return View(new Company());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create(Company model)
    {
        if (await _db.Companies.AnyAsync(c => c.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "A company with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCountriesAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.Companies.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Company created.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!_companyAccess.CanAccessCompany(id))
        {
            return Forbid();
        }

        var company = await _db.Companies.FindAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        await PopulateCountriesAsync();
        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Company model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!_companyAccess.CanAccessCompany(id))
        {
            return Forbid();
        }

        if (await _db.Companies.AnyAsync(c => c.Name == model.Name && c.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "A company with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCountriesAsync();
            return View(model);
        }

        var company = await _db.Companies.FindAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        company.Name = model.Name;
        company.RegistrationNumber = model.RegistrationNumber;
        company.Industry = model.Industry;
        company.Description = model.Description;
        company.Website = model.Website;
        company.Email = model.Email;
        company.Phone = model.Phone;
        company.HeadOffice = model.HeadOffice;
        company.CountryId = model.CountryId;
        company.IsActive = _currentUser.IsAdministrator ? model.IsActive : company.IsActive;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Company updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _db.Companies
            .Include(c => c.Activities)
            .Include(c => c.Donations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company is null)
        {
            return NotFound();
        }

        return View(company);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null)
        {
            return NotFound();
        }

        var hasRecords = await _db.CsrActivities.AnyAsync(a => a.CompanyId == id) ||
                         await _db.Donations.AnyAsync(d => d.CompanyId == id) ||
                         await _db.BursaryProgrammes.AnyAsync(b => b.CompanyId == id) ||
                         await _db.VolunteerProgrammes.AnyAsync(v => v.CompanyId == id) ||
                         await _db.Users.AnyAsync(u => u.CompanyId == id);

        if (hasRecords)
        {
            TempData["Error"] = "This company has linked records. Deactivate it instead of deleting it.";
            return RedirectToAction(nameof(Index));
        }

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Company deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCountriesAsync()
    {
        ViewBag.Countries = await _db.Countries.OrderBy(c => c.Name).AsNoTracking().ToListAsync();
    }
}

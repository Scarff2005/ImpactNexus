using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class CountriesController : Controller
{
    private readonly ApplicationDbContext _db;

    public CountriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.Countries.Include(c => c.Provinces).OrderBy(c => c.Name).ToListAsync());

    public IActionResult Create() => View(new Country());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Country model)
    {
        if (await _db.Countries.AnyAsync(c => c.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "This country already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.Countries.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Country created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var country = await _db.Countries.FindAsync(id);
        return country is null ? NotFound() : View(country);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Country model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (await _db.Countries.AnyAsync(c => c.Name == model.Name && c.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "This country already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var country = await _db.Countries.FindAsync(id);
        if (country is null)
        {
            return NotFound();
        }

        country.Name = model.Name;
        country.IsoCode = model.IsoCode;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Country updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var country = await _db.Countries.Include(c => c.Provinces).FirstOrDefaultAsync(c => c.Id == id);
        return country is null ? NotFound() : View(country);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var country = await _db.Countries
            .Include(c => c.Provinces)
            .Include(c => c.Companies)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (country is null)
        {
            return NotFound();
        }

        if (country.Provinces.Count > 0 || country.Companies.Count > 0)
        {
            TempData["Error"] = "This country has provinces or companies linked to it and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Countries.Remove(country);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Country deleted.";
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Policy = Policies.AdminOnly)]
public class ProvincesController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProvincesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? countryId)
    {
        var query = _db.Provinces.Include(p => p.Country).Include(p => p.Towns).AsQueryable();
        if (countryId.HasValue)
        {
            query = query.Where(p => p.CountryId == countryId.Value);
        }

        ViewBag.Countries = await _db.Countries.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CountryId = countryId;
        return View(await query.OrderBy(p => p.Name).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAsync();
        return View(new Province());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Province model)
    {
        if (await _db.Provinces.AnyAsync(p => p.CountryId == model.CountryId && p.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "This province already exists in the selected country.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        _db.Provinces.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Province created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var province = await _db.Provinces.FindAsync(id);
        if (province is null)
        {
            return NotFound();
        }

        await PopulateAsync();
        return View(province);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Province model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (await _db.Provinces.AnyAsync(p => p.CountryId == model.CountryId && p.Name == model.Name && p.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "This province already exists in the selected country.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        var province = await _db.Provinces.FindAsync(id);
        if (province is null)
        {
            return NotFound();
        }

        province.Name = model.Name;
        province.CountryId = model.CountryId;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Province updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var province = await _db.Provinces.Include(p => p.Country).Include(p => p.Towns).FirstOrDefaultAsync(p => p.Id == id);
        return province is null ? NotFound() : View(province);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var province = await _db.Provinces.Include(p => p.Towns).FirstOrDefaultAsync(p => p.Id == id);
        if (province is null)
        {
            return NotFound();
        }

        if (province.Towns.Count > 0)
        {
            TempData["Error"] = "This province has towns linked to it and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Provinces.Remove(province);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Province deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync()
    {
        ViewBag.Countries = await _db.Countries.OrderBy(c => c.Name).ToListAsync();
    }
}

[Authorize(Policy = Policies.AdminOnly)]
public class TownsController : Controller
{
    private readonly ApplicationDbContext _db;

    public TownsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? provinceId)
    {
        var query = _db.Towns.Include(t => t.Province).ThenInclude(p => p!.Country).AsQueryable();
        if (provinceId.HasValue)
        {
            query = query.Where(t => t.ProvinceId == provinceId.Value);
        }

        ViewBag.Provinces = await _db.Provinces.Include(p => p.Country).OrderBy(p => p.Name).ToListAsync();
        ViewBag.ProvinceId = provinceId;
        return View(await query.OrderBy(t => t.Name).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAsync();
        return View(new Town());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Town model)
    {
        if (await _db.Towns.AnyAsync(t => t.ProvinceId == model.ProvinceId && t.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "This town already exists in the selected province.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        _db.Towns.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Town created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var town = await _db.Towns.FindAsync(id);
        if (town is null)
        {
            return NotFound();
        }

        await PopulateAsync();
        return View(town);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Town model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (await _db.Towns.AnyAsync(t => t.ProvinceId == model.ProvinceId && t.Name == model.Name && t.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "This town already exists in the selected province.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        var town = await _db.Towns.FindAsync(id);
        if (town is null)
        {
            return NotFound();
        }

        town.Name = model.Name;
        town.ProvinceId = model.ProvinceId;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Town updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var town = await _db.Towns.Include(t => t.Province).Include(t => t.Locations).FirstOrDefaultAsync(t => t.Id == id);
        return town is null ? NotFound() : View(town);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var town = await _db.Towns.Include(t => t.Locations).FirstOrDefaultAsync(t => t.Id == id);
        if (town is null)
        {
            return NotFound();
        }

        if (town.Locations.Count > 0)
        {
            TempData["Error"] = "This town has locations linked to it and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Towns.Remove(town);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Town deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync()
    {
        ViewBag.Provinces = await _db.Provinces.Include(p => p.Country).OrderBy(p => p.Name).ToListAsync();
    }
}

[Authorize(Policy = Policies.AdminOnly)]
public class LocationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public LocationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? townId)
    {
        var query = _db.Locations
            .Include(l => l.Town).ThenInclude(t => t!.Province).ThenInclude(p => p!.Country)
            .AsQueryable();

        if (townId.HasValue)
        {
            query = query.Where(l => l.TownId == townId.Value);
        }

        ViewBag.Towns = await _db.Towns.Include(t => t.Province).OrderBy(t => t.Name).ToListAsync();
        ViewBag.TownId = townId;
        return View(await query.OrderBy(l => l.Name).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAsync();
        return View(new Models.Location());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Models.Location model)
    {
        if (await _db.Locations.AnyAsync(l => l.TownId == model.TownId && l.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "This location already exists in the selected town.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        _db.Locations.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Location created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var location = await _db.Locations.FindAsync(id);
        if (location is null)
        {
            return NotFound();
        }

        await PopulateAsync();
        return View(location);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Models.Location model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (await _db.Locations.AnyAsync(l => l.TownId == model.TownId && l.Name == model.Name && l.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "This location already exists in the selected town.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync();
            return View(model);
        }

        var location = await _db.Locations.FindAsync(id);
        if (location is null)
        {
            return NotFound();
        }

        location.Name = model.Name;
        location.AddressLine = model.AddressLine;
        location.TownId = model.TownId;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Location updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var location = await _db.Locations.Include(l => l.Town).FirstOrDefaultAsync(l => l.Id == id);
        return location is null ? NotFound() : View(location);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var location = await _db.Locations.FindAsync(id);
        if (location is null)
        {
            return NotFound();
        }

        if (await _db.CsrActivities.AnyAsync(a => a.LocationId == id) ||
            await _db.VolunteerProgrammes.AnyAsync(p => p.LocationId == id))
        {
            TempData["Error"] = "This location is linked to activities or programmes and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Locations.Remove(location);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Location deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync()
    {
        ViewBag.Towns = await _db.Towns.Include(t => t.Province).OrderBy(t => t.Name).ToListAsync();
    }
}

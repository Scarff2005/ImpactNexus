using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class VolunteerHoursController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public VolunteerHoursController(
        ApplicationDbContext db,
        ICompanyAccessService companyAccess,
        ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        IQueryable<VolunteerHour> query;

        if (_currentUser.IsAdministrator)
        {
            query = _db.VolunteerHours.AsNoTracking();
        }
        else if (_currentUser.IsCsrManager && _currentUser.CompanyId is int companyId)
        {
            query = _db.VolunteerHours
                .Where(v => v.VolunteerProgramme!.CompanyId == companyId)
                .AsNoTracking();
        }
        else if (_currentUser.IsCommunityMember)
        {
            query = _db.VolunteerHours.Where(v => v.UserId == _currentUser.UserId).AsNoTracking();
        }
        else
        {
            return Forbid();
        }

        var total = await query.CountAsync();
        var rows = await query
            .Include(v => v.User)
            .Include(v => v.VolunteerProgramme).ThenInclude(p => p!.Company)
            .Include(v => v.CsrActivity)
            .OrderByDescending(v => v.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalHours = await query.SumAsync(v => (decimal?)v.Hours) ?? 0m;
        ViewBag.PendingHours = await query.Where(v => !v.Approved).SumAsync(v => (decimal?)v.Hours) ?? 0m;
        ViewBag.CanManage = _currentUser.IsStaff;
        ViewBag.CanLog = _currentUser.IsCommunityMember;
        return View(PagedList<VolunteerHour>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var hour = await _db.VolunteerHours
            .Include(v => v.User)
            .Include(v => v.VolunteerProgramme).ThenInclude(p => p!.Company)
            .Include(v => v.CsrActivity)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (!_currentUser.IsAdministrator &&
            !(_currentUser.IsCsrManager && hour.VolunteerProgramme is not null &&
              _companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId)) &&
            !(_currentUser.IsCommunityMember && hour.UserId == _currentUser.UserId))
        {
            return Forbid();
        }

        ViewBag.CanManage = _currentUser.IsStaff;
        return View(hour);
    }

    [Authorize(Policy = Policies.CommunityOnly)]
    public async Task<IActionResult> Log()
    {
        await PopulateLogListsAsync();
        return View(new VolunteerHour
        {
            UserId = _currentUser.UserId!,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CommunityOnly)]
    public async Task<IActionResult> Log(VolunteerHour model)
    {
        var programme = await _db.VolunteerProgrammes.FindAsync(model.VolunteerProgrammeId);
        if (programme is null)
        {
            ModelState.AddModelError(nameof(model.VolunteerProgrammeId), "Select a valid programme.");
        }

        model.UserId = _currentUser.UserId!;
        model.Approved = false;
        model.CreatedAt = DateTime.UtcNow;

        if (model.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(model.Date), "Hours cannot be logged for a future date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateLogListsAsync();
            return View(model);
        }

        _db.VolunteerHours.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer hours submitted for approval.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create()
    {
        await PopulateStaffListsAsync();
        return View(new VolunteerHour
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Approved = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create(VolunteerHour model)
    {
        var programme = await _db.VolunteerProgrammes.FindAsync(model.VolunteerProgrammeId);
        if (programme is null || !_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            ModelState.AddModelError(nameof(model.VolunteerProgrammeId), "Select a programme you manage.");
        }

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            ModelState.AddModelError(nameof(model.UserId), "Select a volunteer.");
        }

        if (model.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(model.Date), "Hours cannot be recorded for a future date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateStaffListsAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.VolunteerHours.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer hours recorded.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var hour = await _db.VolunteerHours
            .Include(v => v.VolunteerProgramme)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (hour.VolunteerProgramme is null || !_companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId))
        {
            return Forbid();
        }

        await PopulateStaffListsAsync();
        return View(hour);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id, VolunteerHour model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var hour = await _db.VolunteerHours
            .Include(v => v.VolunteerProgramme)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (hour.VolunteerProgramme is null || !_companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId))
        {
            return Forbid();
        }

        if (model.Date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(model.Date), "Hours cannot be recorded for a future date.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateStaffListsAsync();
            return View(model);
        }

        hour.Hours = model.Hours;
        hour.Date = model.Date;
        hour.Notes = model.Notes;
        hour.Approved = model.Approved;
        hour.CsrActivityId = model.CsrActivityId;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer hours updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Approve(int id)
    {
        var hour = await _db.VolunteerHours
            .Include(v => v.VolunteerProgramme)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (hour.VolunteerProgramme is null || !_companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId))
        {
            return Forbid();
        }

        hour.Approved = !hour.Approved;
        await _db.SaveChangesAsync();
        TempData["Success"] = hour.Approved ? "Hours approved." : "Approval withdrawn.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var hour = await _db.VolunteerHours
            .Include(v => v.User)
            .Include(v => v.VolunteerProgramme)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (hour.VolunteerProgramme is null || !_companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId))
        {
            return Forbid();
        }

        return View(hour);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var hour = await _db.VolunteerHours
            .Include(v => v.VolunteerProgramme)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (hour is null)
        {
            return NotFound();
        }

        if (hour.VolunteerProgramme is null || !_companyAccess.CanAccessCompany(hour.VolunteerProgramme.CompanyId))
        {
            return Forbid();
        }

        _db.VolunteerHours.Remove(hour);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Volunteer hours deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLogListsAsync()
    {
        ViewBag.Programmes = await _db.VolunteerProgrammes
            .Include(p => p.Company)
            .Where(p => p.Status == ProgrammeStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.Activities = await _db.CsrActivities
            .Where(a => a.Status == ActivityStatus.InProgress)
            .OrderBy(a => a.Title)
            .ToListAsync();
    }

    private async Task PopulateStaffListsAsync()
    {
        var programmes = _db.VolunteerProgrammes.Include(p => p.Company).AsQueryable();
        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            programmes = programmes.Where(p => p.CompanyId == scoped);
        }

        ViewBag.Programmes = await programmes.OrderBy(p => p.Name).ToListAsync();

        ViewBag.Activities = await _db.CsrActivities
            .Where(a => _companyAccess.ScopedCompanyId == null || a.CompanyId == _companyAccess.ScopedCompanyId)
            .OrderBy(a => a.Title)
            .ToListAsync();

        ViewBag.Volunteers = await _db.Users
            .Where(u => u.IsActive && _db.UserRoles
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .Any(x => x.UserId == u.Id && x.Name == Roles.CommunityMember))
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}

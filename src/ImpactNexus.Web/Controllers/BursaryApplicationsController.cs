using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize]
public class BursaryApplicationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAccessService _companyAccess;
    private readonly ICurrentUserService _currentUser;

    public BursaryApplicationsController(
        ApplicationDbContext db,
        ICompanyAccessService companyAccess,
        ICurrentUserService currentUser)
    {
        _db = db;
        _companyAccess = companyAccess;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(BursaryApplicationStatus? status, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        IQueryable<BursaryApplication> query;

        if (_currentUser.IsStaff)
        {
            query = _db.BursaryApplications.AsNoTracking();
            if (_companyAccess.ScopedCompanyId is int scoped)
            {
                query = query.Where(a => a.BursaryProgramme!.CompanyId == scoped);
            }
        }
        else if (_currentUser.IsCommunityMember)
        {
            query = _db.BursaryApplications
                .Where(a => a.Beneficiary!.UserId == _currentUser.UserId)
                .AsNoTracking();
        }
        else
        {
            return Forbid();
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync();
        var rows = await query
            .Include(a => a.Beneficiary)
            .Include(a => a.BursaryProgramme).ThenInclude(p => p!.Company)
            .OrderByDescending(a => a.AppliedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.CanManage = _currentUser.IsStaff;
        ViewBag.CanApply = _currentUser.IsCommunityMember;
        return View(PagedList<BursaryApplication>.Create(rows, total, page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var application = await _db.BursaryApplications
            .Include(a => a.Beneficiary)
            .Include(a => a.BursaryProgramme).ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null)
        {
            return NotFound();
        }

        if (!CanView(application))
        {
            return Forbid();
        }

        return View(application);
    }

    [Authorize(Policy = Policies.CommunityOnly)]
    public async Task<IActionResult> Apply()
    {
        await PopulateApplyListsAsync();
        return View(new BursaryApplication());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CommunityOnly)]
    public async Task<IActionResult> Apply(int bursaryProgrammeId, string motivation)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var programme = await _db.BursaryProgrammes.FindAsync(bursaryProgrammeId);

        if (programme is null || !programme.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Select an open bursary programme.");
        }
        else if (today < programme.OpenDate || today > programme.CloseDate)
        {
            ModelState.AddModelError(string.Empty,
                $"{programme.Name} is not accepting applications. " +
                $"The window was {programme.OpenDate:dd MMM yyyy} to {programme.CloseDate:dd MMM yyyy}.");
        }
        else if (await _db.BursaryApplications.AnyAsync(a =>
                     a.BursaryProgrammeId == bursaryProgrammeId &&
                     a.Beneficiary!.UserId == _currentUser.UserId))
        {
            ModelState.AddModelError(string.Empty, "You have already applied for this programme.");
        }

        if (string.IsNullOrWhiteSpace(motivation) || motivation.Trim().Length < 20)
        {
            ModelState.AddModelError(nameof(motivation), "Provide a motivation of at least 20 characters.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateApplyListsAsync();
            return View(new BursaryApplication());
        }

        // The beneficiary record is only created once the submission is known to be valid,
        // so a rejected form never leaves an orphan applicant behind.
        var beneficiary = await _db.Beneficiaries
            .FirstOrDefaultAsync(b => b.UserId == _currentUser.UserId);

        if (beneficiary is null)
        {
            beneficiary = new Beneficiary
            {
                FullName = _currentUser.FullName ?? "Applicant",
                Email = User.Identity?.Name,
                UserId = _currentUser.UserId,
                CompanyId = programme!.CompanyId
            };
            _db.Beneficiaries.Add(beneficiary);
            await _db.SaveChangesAsync();
        }

        var application = new BursaryApplication
        {
            BursaryProgrammeId = bursaryProgrammeId,
            BeneficiaryId = beneficiary.Id,
            Motivation = motivation.Trim(),
            AppliedOn = today,
            Status = BursaryApplicationStatus.Submitted
        };

        _db.BursaryApplications.Add(application);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Application submitted.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        return View(new BursaryApplication
        {
            AppliedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Create(BursaryApplication model)
    {
        var programme = await _db.BursaryProgrammes.FindAsync(model.BursaryProgrammeId);
        if (programme is null || !_companyAccess.CanAccessCompany(programme.CompanyId))
        {
            ModelState.AddModelError(nameof(model.BursaryProgrammeId), "Select a programme you are allowed to manage.");
        }

        if (!await _db.Beneficiaries.AnyAsync(b => b.Id == model.BeneficiaryId))
        {
            ModelState.AddModelError(nameof(model.BeneficiaryId), "Select a valid beneficiary.");
        }
        else if (await _db.BursaryApplications.AnyAsync(a =>
                     a.BursaryProgrammeId == model.BursaryProgrammeId &&
                     a.BeneficiaryId == model.BeneficiaryId))
        {
            ModelState.AddModelError(nameof(model.BeneficiaryId),
                "That beneficiary already has an application for this programme.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.BursaryApplications.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary application captured.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var application = await _db.BursaryApplications.FindAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(application))
        {
            return Forbid();
        }

        await PopulateListsAsync();
        return View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Edit(int id, BursaryApplication model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var application = await _db.BursaryApplications.FindAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(application))
        {
            return Forbid();
        }

        if (model.Status == BursaryApplicationStatus.Approved && (!model.AmountAwarded.HasValue || model.AmountAwarded <= 0))
        {
            ModelState.AddModelError(nameof(model.AmountAwarded), "An approved application needs an awarded amount.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            return View(model);
        }

        application.Status = model.Status;
        application.AmountAwarded = model.Status == BursaryApplicationStatus.Approved
            ? model.AmountAwarded
            : null;
        application.DecisionOn = model.DecisionOn;
        application.ReviewNotes = model.ReviewNotes;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Application updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var application = await _db.BursaryApplications
            .Include(a => a.Beneficiary)
            .Include(a => a.BursaryProgramme)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(application))
        {
            return Forbid();
        }

        return View(application);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.StaffOnly)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var application = await _db.BursaryApplications.FindAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(application))
        {
            return Forbid();
        }

        _db.BursaryApplications.Remove(application);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Application deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanView(BursaryApplication application)
    {
        if (_currentUser.IsAdministrator)
        {
            return true;
        }

        if (_currentUser.IsCsrManager)
        {
            return application.BursaryProgramme is not null &&
                   _companyAccess.CanAccessCompany(application.BursaryProgramme.CompanyId);
        }

        return _currentUser.IsCommunityMember && application.Beneficiary?.UserId == _currentUser.UserId;
    }

    private async Task<bool> CanManageAsync(BursaryApplication application)
    {
        var programme = await _db.BursaryProgrammes.FindAsync(application.BursaryProgrammeId);
        return programme is not null && _companyAccess.CanAccessCompany(programme.CompanyId);
    }

    private async Task PopulateListsAsync()
    {
        var programmes = _db.BursaryProgrammes.AsQueryable();
        if (_companyAccess.ScopedCompanyId is int scoped)
        {
            programmes = programmes.Where(p => p.CompanyId == scoped);
        }

        ViewBag.Programmes = await programmes.OrderByDescending(p => p.AcademicYear).ToListAsync();
        ViewBag.Beneficiaries = await _db.Beneficiaries
            .Where(b => _companyAccess.ScopedCompanyId == null || b.CompanyId == _companyAccess.ScopedCompanyId)
            .OrderBy(b => b.FullName)
            .ToListAsync();
    }

    private async Task PopulateApplyListsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ViewBag.Programmes = await _db.BursaryProgrammes
            .Include(p => p.Company)
            .Where(p => p.IsActive && p.OpenDate <= today && p.CloseDate >= today)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}

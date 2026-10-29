using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Data.ApplicationDbContext _db;

    public UsersController(UserManager<ApplicationUser> userManager, Data.ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, bool? active, int page = 1)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);

        var query = _db.Users.Include(u => u.Company).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term));
        }

        if (active.HasValue)
        {
            query = query.Where(u => u.IsActive == active.Value);
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // One join for the whole page instead of a query per user.
        var ids = users.Select(u => u.Id).ToList();
        var roles = await _db.UserRoles
            .Where(ur => ids.Contains(ur.UserId))
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var roleLookup = roles
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => r.Name).OrderBy(n => n)));

        ViewBag.Roles = roleLookup;
        ViewBag.Search = search;
        ViewBag.Active = active;
        return View(PagedList<ApplicationUser>.Create(users, total, page, pageSize));
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _db.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        ViewBag.Roles = await _userManager.GetRolesAsync(user);
        return View(user);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateCompaniesAsync();
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!Roles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        }

        if (model.Role == Roles.CsrManager && model.CompanyId is null)
        {
            ModelState.AddModelError(nameof(model.CompanyId), "A CSR Manager must be linked to a company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCompaniesAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            CompanyId = model.CompanyId,
            IsActive = model.IsActive
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateCompaniesAsync();
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        TempData["Success"] = $"User {user.Email} created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        await PopulateCompaniesAsync();

        return View(new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty,
            CompanyId = user.CompanyId,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        var user = await _db.Users.FindAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        if (!Roles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        }

        if (model.Role == Roles.CsrManager && model.CompanyId is null)
        {
            ModelState.AddModelError(nameof(model.CompanyId), "A CSR Manager must be linked to a company.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCompaniesAsync();
            return View(model);
        }

        var companyChanged = user.CompanyId != model.CompanyId;
        var wasActive = user.IsActive;

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.CompanyId = model.CompanyId;
        user.IsActive = model.IsActive;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var error in update.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateCompaniesAsync();
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Where(r => r != model.Role).ToList();
        if (rolesToRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (!currentRoles.Contains(model.Role))
        {
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        // Company, role and active-state all travel in the auth cookie's claims, so a
        // security stamp roll forces those claims to be rebuilt on the next request.
        if (!user.IsActive ||
            companyChanged ||
            wasActive != user.IsActive ||
            !currentRoles.SequenceEqual(new[] { model.Role }))
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        TempData["Success"] = $"User {user.Email} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        // Force existing sessions to be re-validated so a disabled account is signed out
        // instead of staying usable until its cookie expires.
        if (!user.IsActive)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        TempData["Success"] = user.IsActive
            ? $"Account {user.Email} enabled."
            : $"Account {user.Email} disabled.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return View(new ResetPasswordViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _db.Users.FindAsync(model.UserId);
        if (user is null)
        {
            return NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Success"] = $"Password reset for {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        var user = await _db.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }

        return View(user);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "The account could not be deleted because related records reference it. Disable it instead.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "User deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCompaniesAsync()
    {
        ViewBag.Companies = await _db.Companies.OrderBy(c => c.Name).AsNoTracking().ToListAsync();
        ViewBag.RoleList = Roles.All;
    }
}

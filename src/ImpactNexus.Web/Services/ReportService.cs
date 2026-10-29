using System.Globalization;
using System.Text;
using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Services;

public class ReportFilters
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? CompanyId { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? UserId { get; set; }
}

public class ActivityReportRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Budget { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ImpactIndicator { get; set; } = string.Empty;
}

public class CompanyContributionReportRow
{
    public string Company { get; set; } = string.Empty;
    public int DonationCount { get; set; }
    public decimal Total { get; set; }
    public DateOnly? LastDonationOn { get; set; }
}

public class DonationReportRow
{
    public DateOnly DonatedOn { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class BursaryReportRow
{
    public string Applicant { get; set; } = string.Empty;
    public string Programme { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public int AcademicYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? AmountAwarded { get; set; }
    public DateOnly AppliedOn { get; set; }
}

public class VolunteerHoursReportRow
{
    public string Volunteer { get; set; } = string.Empty;
    public string Programme { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public DateOnly Date { get; set; }
    public bool Approved { get; set; }
}

public class BeneficiaryStatsRow
{
    public string Company { get; set; } = string.Empty;
    public int BeneficiaryCount { get; set; }
    public int ApprovedBursaries { get; set; }
    public decimal AwardedTotal { get; set; }
}

/// <summary>
/// Read-only reporting queries. Scope (company, donor, volunteer) is always applied by
/// the caller before these filters, so a report can never widen a user's access.
/// </summary>
public class ReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ActivityReportRow>> GetActivityReportAsync(ReportFilters filters)
    {
        var query = _db.CsrActivities
            .Include(a => a.Company)
            .Include(a => a.Location)
            .AsNoTracking()
            .AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == filters.CompanyId.Value);
        }

        if (filters.From.HasValue)
        {
            query = query.Where(a => a.StartDate >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(a => a.StartDate <= filters.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Description.Contains(term));
        }

        if (Enum.TryParse<ActivityStatus>(filters.Status, true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.StartDate)
            .Select(a => new ActivityReportRow
            {
                Id = a.Id,
                Title = a.Title,
                Company = a.Company!.Name,
                Location = a.Location != null
                    ? a.Location.Name + ", " + a.Location.Town!.Name
                    : "—",
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                Budget = a.Budget,
                Status = a.Status.ToString(),
                ImpactIndicator = a.ImpactIndicator ?? string.Empty
            })
            .ToListAsync();
    }

    public async Task<List<CompanyContributionReportRow>> GetContributionReportAsync(ReportFilters filters)
    {
        var query = _db.Donations
            .Include(d => d.Company)
            .AsNoTracking()
            .AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(d => d.CompanyId == filters.CompanyId.Value);
        }

        if (filters.UserId is not null)
        {
            query = query.Where(d => d.DonorUserId == filters.UserId);
        }

        if (filters.From.HasValue)
        {
            query = query.Where(d => d.DonatedOn >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(d => d.DonatedOn <= filters.To.Value);
        }

        var rows = await query
            .Where(d => d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated)
            .Select(d => new { d.Company!.Name, d.Amount, d.DonatedOn })
            .ToListAsync();

        return rows
            .GroupBy(r => r.Name)
            .Select(g => new CompanyContributionReportRow
            {
                Company = g.Key,
                DonationCount = g.Count(),
                Total = g.Sum(r => r.Amount),
                LastDonationOn = g.Max(r => r.DonatedOn)
            })
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    public async Task<List<DonationReportRow>> GetDonationReportAsync(ReportFilters filters)
    {
        var query = _db.Donations
            .Include(d => d.Company)
            .Include(d => d.CsrActivity)
            .AsNoTracking()
            .AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(d => d.CompanyId == filters.CompanyId.Value);
        }

        if (filters.UserId is not null)
        {
            query = query.Where(d => d.DonorUserId == filters.UserId);
        }

        if (filters.From.HasValue)
        {
            query = query.Where(d => d.DonatedOn >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(d => d.DonatedOn <= filters.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(d => d.DonorName.Contains(term) || d.Description.Contains(term));
        }

        if (Enum.TryParse<DonationStatus>(filters.Status, true, out var status))
        {
            query = query.Where(d => d.Status == status);
        }

        return await query
            .OrderByDescending(d => d.DonatedOn)
            .Select(d => new DonationReportRow
            {
                DonatedOn = d.DonatedOn,
                DonorName = d.DonorName,
                Company = d.Company!.Name,
                Activity = d.CsrActivity != null ? d.CsrActivity.Title : "Unallocated",
                Amount = d.Amount,
                Status = d.Status.ToString(),
                Reference = d.Reference ?? string.Empty
            })
            .ToListAsync();
    }

    public async Task<List<BursaryReportRow>> GetBursaryReportAsync(ReportFilters filters)
    {
        var query = _db.BursaryApplications
            .Include(a => a.Beneficiary)
            .Include(a => a.BursaryProgramme).ThenInclude(p => p!.Company)
            .AsNoTracking()
            .AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(a => a.BursaryProgramme!.CompanyId == filters.CompanyId.Value);
        }

        if (filters.UserId is not null)
        {
            query = query.Where(a => a.Beneficiary!.UserId == filters.UserId);
        }

        if (filters.From.HasValue)
        {
            query = query.Where(a => a.AppliedOn >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(a => a.AppliedOn <= filters.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(a => a.Beneficiary!.FullName.Contains(term) ||
                                     a.BursaryProgramme!.Name.Contains(term));
        }

        if (Enum.TryParse<BursaryApplicationStatus>(filters.Status, true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.AppliedOn)
            .Select(a => new BursaryReportRow
            {
                Applicant = a.Beneficiary!.FullName,
                Programme = a.BursaryProgramme!.Name,
                Company = a.BursaryProgramme.Company!.Name,
                AcademicYear = a.BursaryProgramme.AcademicYear,
                Status = a.Status.ToString(),
                AmountAwarded = a.AmountAwarded,
                AppliedOn = a.AppliedOn
            })
            .ToListAsync();
    }

    public async Task<List<VolunteerHoursReportRow>> GetVolunteerHoursReportAsync(ReportFilters filters)
    {
        var query = _db.VolunteerHours
            .Include(v => v.User)
            .Include(v => v.VolunteerProgramme).ThenInclude(p => p!.Company)
            .AsNoTracking()
            .AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(v => v.VolunteerProgramme!.CompanyId == filters.CompanyId.Value);
        }

        if (filters.UserId is not null)
        {
            query = query.Where(v => v.UserId == filters.UserId);
        }

        if (filters.From.HasValue)
        {
            query = query.Where(v => v.Date >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(v => v.Date <= filters.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(v => v.User!.FullName.Contains(term) ||
                                     v.VolunteerProgramme!.Name.Contains(term));
        }

        return await query
            .OrderByDescending(v => v.Date)
            .Select(v => new VolunteerHoursReportRow
            {
                Volunteer = v.User!.FullName,
                Programme = v.VolunteerProgramme!.Name,
                Company = v.VolunteerProgramme.Company!.Name,
                Hours = v.Hours,
                Date = v.Date,
                Approved = v.Approved
            })
            .ToListAsync();
    }

    public async Task<List<BeneficiaryStatsRow>> GetBeneficiaryStatsAsync(ReportFilters filters)
    {
        var query = _db.Beneficiaries.AsNoTracking().AsQueryable();

        if (filters.CompanyId.HasValue)
        {
            query = query.Where(b => b.CompanyId == filters.CompanyId.Value);
        }

        var beneficiaries = await query
            .Select(b => new
            {
                Company = b.Company != null ? b.Company.Name : "Unassigned",
                Approved = b.Applications.Count(a => a.Status == BursaryApplicationStatus.Approved),
                Awarded = b.Applications
                    .Where(a => a.Status == BursaryApplicationStatus.Approved)
                    .Sum(a => (decimal?)a.AmountAwarded) ?? 0m
            })
            .ToListAsync();

        return beneficiaries
            .GroupBy(b => b.Company)
            .Select(g => new BeneficiaryStatsRow
            {
                Company = g.Key,
                BeneficiaryCount = g.Count(),
                ApprovedBursaries = g.Sum(x => x.Approved),
                AwardedTotal = g.Sum(x => x.Awarded)
            })
            .OrderByDescending(r => r.BeneficiaryCount)
            .ToList();
    }
}

public static class CsvExport
{
    public static string Write<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", properties.Select(p => Escape(p.Name))));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", properties.Select(p =>
                Escape(FormatValue(p.GetValue(row))))));
        }
        return builder.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}

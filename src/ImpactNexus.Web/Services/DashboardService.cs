using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Services;

public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public DashboardService(ApplicationDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    public async Task<AdminDashboardViewModel> GetAdminDashboardAsync()
    {
        var receivedStatuses = new[] { DonationStatus.Received, DonationStatus.Allocated };
        var monthStart = new DateOnly(Today.Year, Today.Month, 1);

        return new AdminDashboardViewModel
        {
            CompanyCount = await _db.Companies.CountAsync(c => c.IsActive),
            UserCount = await _db.Users.CountAsync(),
            ActiveUserCount = await _db.Users.CountAsync(u => u.IsActive),
            ActivityCount = await _db.CsrActivities.CountAsync(),
            ActiveActivityCount = await _db.CsrActivities.CountAsync(a => a.Status == ActivityStatus.InProgress),
            DonationTotal = await _db.Donations.Where(d => receivedStatuses.Contains(d.Status))
                .SumAsync(d => (decimal?)d.Amount) ?? 0m,
            DonationsThisMonth = await _db.Donations
                .Where(d => receivedStatuses.Contains(d.Status) && d.DonatedOn >= monthStart)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m,
            BursaryAwardedTotal = await _db.BursaryApplications
                .Where(a => a.Status == BursaryApplicationStatus.Approved)
                .SumAsync(a => (decimal?)a.AmountAwarded) ?? 0m,
            ApprovedBursaryCount = await _db.BursaryApplications
                .CountAsync(a => a.Status == BursaryApplicationStatus.Approved),
            VolunteerHours = await _db.VolunteerHours.SumAsync(v => (decimal?)v.Hours) ?? 0m,
            BeneficiaryCount = await _db.Beneficiaries.CountAsync(),
            TopCompanies = await _db.Companies
                .Select(c => new CompanyContributionRow
                {
                    CompanyId = c.Id,
                    CompanyName = c.Name,
                    ActivityCount = c.Activities.Count(),
                    ContributionTotal = c.Donations
                        .Where(d => d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated)
                        .Sum(d => (decimal?)d.Amount) ?? 0m
                })
                .OrderByDescending(c => c.ContributionTotal)
                .Take(5)
                .ToListAsync(),
            RecentActivities = await _db.CsrActivities
                .Include(a => a.Company)
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync(),
            RecentDonations = await _db.Donations
                .Include(d => d.Company)
                .OrderByDescending(d => d.DonatedOn)
                .Take(6)
                .ToListAsync(),
            MonthlyContributions = await BuildMonthlySeriesAsync(null)
        };
    }

    public async Task<ManagerDashboardViewModel> GetManagerDashboardAsync(int companyId)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new InvalidOperationException("Company not found for the signed-in manager.");

        var receivedStatuses = new[] { DonationStatus.Received, DonationStatus.Allocated };

        return new ManagerDashboardViewModel
        {
            Company = company,
            ActivityCount = await _db.CsrActivities.CountAsync(a => a.CompanyId == companyId),
            ActiveActivityCount = await _db.CsrActivities
                .CountAsync(a => a.CompanyId == companyId && a.Status == ActivityStatus.InProgress),
            TotalBudget = await _db.CsrActivities.Where(a => a.CompanyId == companyId)
                .SumAsync(a => (decimal?)a.Budget) ?? 0m,
            ContributionTotal = await _db.Donations
                .Where(d => d.CompanyId == companyId && receivedStatuses.Contains(d.Status))
                .SumAsync(d => (decimal?)d.Amount) ?? 0m,
            BursaryBudget = await _db.BursaryProgrammes.Where(b => b.CompanyId == companyId)
                .SumAsync(b => (decimal?)b.Budget) ?? 0m,
            BursaryAwarded = await _db.BursaryApplications
                .Where(a => a.BursaryProgramme!.CompanyId == companyId &&
                            a.Status == BursaryApplicationStatus.Approved)
                .SumAsync(a => (decimal?)a.AmountAwarded) ?? 0m,
            OpenBursaryApplications = await _db.BursaryApplications
                .CountAsync(a => a.BursaryProgramme!.CompanyId == companyId &&
                                 (a.Status == BursaryApplicationStatus.Submitted ||
                                  a.Status == BursaryApplicationStatus.UnderReview)),
            VolunteerHours = await _db.VolunteerHours
                .Where(v => v.VolunteerProgramme!.CompanyId == companyId)
                .SumAsync(v => (decimal?)v.Hours) ?? 0m,
            VolunteerCount = await _db.VolunteerHours
                .Where(v => v.VolunteerProgramme!.CompanyId == companyId)
                .Select(v => v.UserId).Distinct().CountAsync(),
            BeneficiaryCount = await _db.Beneficiaries.CountAsync(b => b.CompanyId == companyId),
            RecentActivities = await _db.CsrActivities
                .Where(a => a.CompanyId == companyId)
                .OrderByDescending(a => a.StartDate)
                .Take(6)
                .ToListAsync(),
            RecentDonations = await _db.Donations
                .Where(d => d.CompanyId == companyId)
                .OrderByDescending(d => d.DonatedOn)
                .Take(6)
                .ToListAsync(),
            ActivityStatusBreakdown = await _db.CsrActivities
                .Where(a => a.CompanyId == companyId)
                .GroupBy(a => a.Status)
                .Select(g => new StatusCount { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),
            MonthlyContributions = await BuildMonthlySeriesAsync(companyId)
        };
    }

    public async Task<InvestorDashboardViewModel> GetInvestorDashboardAsync(string userId)
    {
        var receivedStatuses = new[] { DonationStatus.Received, DonationStatus.Allocated };

        var donations = await _db.Donations
            .Include(d => d.Company)
            .Include(d => d.CsrActivity)
            .Where(d => d.DonorUserId == userId)
            .OrderByDescending(d => d.DonatedOn)
            .ToListAsync();

        var fundedActivityIds = donations
            .Where(d => d.CsrActivityId.HasValue)
            .Select(d => d.CsrActivityId!.Value)
            .Distinct()
            .ToList();

        return new InvestorDashboardViewModel
        {
            DonationCount = donations.Count,
            TotalContributed = donations
                .Where(d => receivedStatuses.Contains(d.Status))
                .Sum(d => d.Amount),
            FundedActivityCount = fundedActivityIds.Count,
            LastDonationOn = donations.Count > 0 ? donations[0].DonatedOn : null,
            RecentDonations = donations.Take(6).ToList(),
            FundedActivities = await _db.CsrActivities
                .Include(a => a.Company)
                .Include(a => a.Location)
                .Where(a => fundedActivityIds.Contains(a.Id))
                .OrderByDescending(a => a.StartDate)
                .ToListAsync(),
            CompanyBreakdown = donations
                .GroupBy(d => d.Company?.Name ?? "Unassigned")
                .Select(g => new CompanyContributionRow
                {
                    CompanyName = g.Key,
                    ContributionTotal = g.Where(d => receivedStatuses.Contains(d.Status)).Sum(d => d.Amount),
                    ActivityCount = g.Select(d => d.CsrActivityId).Distinct().Count()
                })
                .OrderByDescending(g => g.ContributionTotal)
                .ToList(),
            MonthlyContributions = await BuildMonthlySeriesAsync(null, userId)
        };
    }
    public async Task<CommunityDashboardViewModel> GetCommunityDashboardAsync(string userId)
    {
        var hours = await _db.VolunteerHours
            .Include(v => v.VolunteerProgramme)
            .Include(v => v.CsrActivity)
            .Where(v => v.UserId == userId)
            .ToListAsync();

        var programmeIds = hours.Select(h => h.VolunteerProgrammeId).Distinct().ToList();

        return new CommunityDashboardViewModel
        {
            TotalHours = hours.Sum(h => h.Hours),
            ApprovedHours = hours.Where(h => h.Approved).Sum(h => h.Hours),
            PendingHours = hours.Where(h => !h.Approved).Sum(h => h.Hours),
            ProgrammeCount = programmeIds.Count,
            RecentHours = hours.OrderByDescending(h => h.Date).Take(6).ToList(),
            Programmes = await _db.VolunteerProgrammes
                .Include(p => p.Company)
                .Include(p => p.Location)
                .Where(p => programmeIds.Contains(p.Id) || p.Status == ProgrammeStatus.Active)
                .OrderByDescending(p => p.StartDate)
                .Take(8)
                .ToListAsync(),
            BursaryApplications = await _db.BursaryApplications
                .Include(a => a.BursaryProgramme).ThenInclude(p => p!.Company)
                .Include(a => a.Beneficiary)
                .Where(a => a.Beneficiary!.UserId == userId)
                .OrderByDescending(a => a.AppliedOn)
                .ToListAsync(),
            Activities = await _db.CsrActivities
                .Include(a => a.Company)
                .Include(a => a.Location)
                .Where(a => a.VolunteerHours.Any(v => v.UserId == userId))
                .OrderByDescending(a => a.StartDate)
                .Take(8)
                .ToListAsync(),
            MonthlyHours = BuildMonthlyHours(hours)
        };
    }

    /// <summary>
    /// Six-month window ending in the current month. If that window holds no
    /// records (typical for seeded history or a lapsed account) the window ends
    /// at the newest record instead, so the chart is never misleadingly empty.
    /// </summary>
    private DateOnly WindowStart(IEnumerable<DateOnly> dates)
    {
        var thisMonth = new DateOnly(Today.Year, Today.Month, 1);
        var start = thisMonth.AddMonths(-5);

        var latest = dates.Cast<DateOnly?>().Max();
        if (latest is null)
        {
            return start;
        }

        var latestMonth = new DateOnly(latest.Value.Year, latest.Value.Month, 1);
        return latestMonth < thisMonth.AddMonths(-5) ? latestMonth.AddMonths(-5) : start;
    }

    /// <summary>
    /// Volunteer hours for the signed-in volunteer over six months.
    /// Kept in memory because the source list is already scoped to one user.
    /// </summary>
    private List<MonthlyContributionPoint> BuildMonthlyHours(IEnumerable<VolunteerHour> hours)
    {
        var list = hours.ToList();
        var start = WindowStart(list.Select(h => h.Date));
        var series = new List<MonthlyContributionPoint>();

        for (var i = 0; i < 6; i++)
        {
            var month = start.AddMonths(i);
            series.Add(new MonthlyContributionPoint
            {
                Label = month.ToString("MMM yyyy"),
                Amount = list
                    .Where(h => h.Date.Year == month.Year && h.Date.Month == month.Month)
                    .Sum(h => h.Hours)
            });
        }

        return series;
    }

    private async Task<List<MonthlyContributionPoint>> BuildMonthlySeriesAsync(int? companyId, string? userId = null)
    {
        var query = _db.Donations.AsNoTracking().Where(d =>
            d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated);

        if (companyId.HasValue)
        {
            query = query.Where(d => d.CompanyId == companyId.Value);
        }

        if (userId is not null)
        {
            query = query.Where(d => d.DonorUserId == userId);
        }

        var rows = await query
            .Select(d => new { d.DonatedOn, d.Amount })
            .ToListAsync();

        var start = WindowStart(rows.Select(r => r.DonatedOn));
        var series = new List<MonthlyContributionPoint>();
        for (var i = 0; i < 6; i++)
        {
            var month = start.AddMonths(i);
            series.Add(new MonthlyContributionPoint
            {
                Label = month.ToString("MMM yyyy"),
                Amount = rows.Where(r => r.DonatedOn.Year == month.Year && r.DonatedOn.Month == month.Month)
                    .Sum(r => r.Amount)
            });
        }

        return series;
    }
}

public class StatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MonthlyContributionPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CompanyContributionRow
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
    public decimal ContributionTotal { get; set; }
}

public class AdminDashboardViewModel
{
    public int CompanyCount { get; set; }
    public int UserCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int ActivityCount { get; set; }
    public int ActiveActivityCount { get; set; }
    public decimal DonationTotal { get; set; }
    public decimal DonationsThisMonth { get; set; }
    public decimal BursaryAwardedTotal { get; set; }
    public int ApprovedBursaryCount { get; set; }
    public decimal VolunteerHours { get; set; }
    public int BeneficiaryCount { get; set; }
    public List<CompanyContributionRow> TopCompanies { get; set; } = new();
    public List<CsrActivity> RecentActivities { get; set; } = new();
    public List<Donation> RecentDonations { get; set; } = new();
    public List<MonthlyContributionPoint> MonthlyContributions { get; set; } = new();
}

public class ManagerDashboardViewModel
{
    public Company Company { get; set; } = null!;
    public int ActivityCount { get; set; }
    public int ActiveActivityCount { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal ContributionTotal { get; set; }
    public decimal BursaryBudget { get; set; }
    public decimal BursaryAwarded { get; set; }
    public int OpenBursaryApplications { get; set; }
    public decimal VolunteerHours { get; set; }
    public int VolunteerCount { get; set; }
    public int BeneficiaryCount { get; set; }
    public List<CsrActivity> RecentActivities { get; set; } = new();
    public List<Donation> RecentDonations { get; set; } = new();
    public List<StatusCount> ActivityStatusBreakdown { get; set; } = new();
    public List<MonthlyContributionPoint> MonthlyContributions { get; set; } = new();
}

public class InvestorDashboardViewModel
{
    public int DonationCount { get; set; }
    public decimal TotalContributed { get; set; }
    public int FundedActivityCount { get; set; }
    public DateOnly? LastDonationOn { get; set; }
    public List<Donation> RecentDonations { get; set; } = new();
    public List<CsrActivity> FundedActivities { get; set; } = new();
    public List<CompanyContributionRow> CompanyBreakdown { get; set; } = new();
    public List<MonthlyContributionPoint> MonthlyContributions { get; set; } = new();
}

public class CommunityDashboardViewModel
{
    public decimal TotalHours { get; set; }
    public decimal ApprovedHours { get; set; }
    public decimal PendingHours { get; set; }
    public int ProgrammeCount { get; set; }
    public List<VolunteerHour> RecentHours { get; set; } = new();
    public List<VolunteerProgramme> Programmes { get; set; } = new();
    public List<BursaryApplication> BursaryApplications { get; set; } = new();
    public List<CsrActivity> Activities { get; set; } = new();
    public List<MonthlyContributionPoint> MonthlyHours { get; set; } = new();
}

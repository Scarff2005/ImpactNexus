using ImpactNexus.Web.Data;
using ImpactNexus.Web.Models;
using ImpactNexus.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Tests;

/// <summary>
/// Reporting logic must agree with the database. These tests recompute the raw
/// totals with LINQ and compare them to what the report service returns.
/// </summary>
public class ReportCalculationTests : IntegrationTestBase
{
    public ReportCalculationTests(ImpactNexusFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Contribution_report_totals_match_the_database()
    {
        var expected = Query(db => db.Donations
            .Where(d => d.Status == DonationStatus.Received || d.Status == DonationStatus.Allocated)
            .GroupBy(d => d.CompanyId)
            .Select(g => new { g.Key, Total = g.Sum(d => d.Amount) })
            .ToList());

        var rows = await WithServiceAsync<ReportService, List<CompanyContributionReportRow>>(
            service => service.GetContributionReportAsync(new ReportFilters()));

        foreach (var group in expected)
        {
            var companyName = Query(db => db.Companies.Where(c => c.Id == group.Key).Select(c => c.Name).Single());
            var row = rows.Single(r => r.Company == companyName);
            Assert.Equal(group.Total, row.Total);
        }
    }

    [Fact]
    public async Task Activity_report_respects_the_date_range()
    {
        var all = await WithServiceAsync<ReportService, List<ActivityReportRow>>(
            service => service.GetActivityReportAsync(new ReportFilters()));

        var farFuture = await WithServiceAsync<ReportService, List<ActivityReportRow>>(
            service => service.GetActivityReportAsync(new ReportFilters
            {
                From = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5)),
                To = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(6))
            }));

        Assert.NotEmpty(all);
        Assert.Empty(farFuture);
    }

    [Fact]
    public async Task Activity_report_can_be_filtered_by_company()
    {
        var companyId = Query(db => db.Companies.OrderBy(c => c.Id).Select(c => c.Id).First());
        var expectedCount = Query(db => db.CsrActivities.Count(a => a.CompanyId == companyId));

        var rows = await WithServiceAsync<ReportService, List<ActivityReportRow>>(
            service => service.GetActivityReportAsync(new ReportFilters { CompanyId = companyId }));

        Assert.Equal(expectedCount, rows.Count);
    }

    [Fact]
    public async Task Volunteer_hour_report_totals_match_the_database()
    {
        var expected = Query(db => db.VolunteerHours.Sum(v => (decimal?)v.Hours) ?? 0m);
        var approved = Query(db => db.VolunteerHours.Where(v => v.Approved).Sum(v => (decimal?)v.Hours) ?? 0m);

        var rows = await WithServiceAsync<ReportService, List<VolunteerHoursReportRow>>(
            service => service.GetVolunteerHoursReportAsync(new ReportFilters()));

        Assert.Equal(expected, rows.Sum(r => r.Hours));
        Assert.Equal(approved, rows.Where(r => r.Approved).Sum(r => r.Hours));
    }

    [Fact]
    public async Task Donation_report_filters_by_status()
    {
        var rows = await WithServiceAsync<ReportService, List<DonationReportRow>>(
            service => service.GetDonationReportAsync(new ReportFilters { Status = nameof(DonationStatus.Received) }));

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(nameof(DonationStatus.Received), row.Status));
    }

    [Fact]
    public async Task Donation_report_returns_nothing_outside_the_date_range()
    {
        var rows = await WithServiceAsync<ReportService, List<DonationReportRow>>(
            service => service.GetDonationReportAsync(new ReportFilters
            {
                From = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)),
                To = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-9))
            }));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Bursary_report_award_total_matches_approved_applications()
    {
        var expected = Query(db => db.BursaryApplications
            .Where(a => a.Status == BursaryApplicationStatus.Approved)
            .Sum(a => (decimal?)a.AmountAwarded) ?? 0m);

        var rows = await WithServiceAsync<ReportService, List<BursaryReportRow>>(
            service => service.GetBursaryReportAsync(new ReportFilters()));

        Assert.Equal(expected, rows.Sum(r => r.AmountAwarded ?? 0m));
    }

    [Fact]
    public async Task Beneficiary_statistics_count_seeded_records()
    {
        var expected = Query(db => db.Beneficiaries.Count());

        var rows = await WithServiceAsync<ReportService, List<BeneficiaryStatsRow>>(
            service => service.GetBeneficiaryStatsAsync(new ReportFilters()));

        Assert.Equal(expected, rows.Sum(r => r.BeneficiaryCount));
    }

    [Fact]
    public async Task Csv_export_escapes_commas_and_quotes()
    {
        var csv = CsvExport.Write(new[]
        {
            new DonationReportRow
            {
                DonorName = "Okafor, Amara",
                Reference = "Quotes \"inside\" text",
                Company = "BMW South Africa",
                Activity = "Training",
                Amount = 100m,
                Status = "Received",
                DonatedOn = new DateOnly(2026, 1, 1)
            }
        });

        Assert.Contains("\"Okafor, Amara\"", csv);
        Assert.Contains("\"Quotes \"\"inside\"\" text\"", csv);
        Assert.Contains("2026-01-01", csv);
        Assert.Contains("100.00", csv);
    }
}

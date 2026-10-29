using ImpactNexus.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactNexus.Tests;

public abstract partial class IntegrationTestBase
{
    /// <summary>Runs a query against the same seeded database the application is using.</summary>
    protected T Query<T>(Func<ApplicationDbContext, T> query)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return query(db);
    }

    protected int CompanyIdOf(string email) => Query(db =>
    {
        var user = db.Users.AsNoTracking().Single(u => u.Email == email);
        return user.CompanyId ?? 0;
    });

    protected string UserIdOf(string email) => Query(db =>
        db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).Single());

    protected int OwnDonationId(string donorEmail) => Query(db =>
    {
        var donorId = db.Users.Where(u => u.Email == donorEmail).Select(u => u.Id).Single();
        return db.Donations.Where(d => d.DonorUserId == donorId).OrderBy(d => d.Id).Select(d => d.Id).First();
    });

    protected int OtherDonationId(string donorEmail) => Query(db =>
    {
        var donorId = db.Users.Where(u => u.Email == donorEmail).Select(u => u.Id).Single();
        return db.Donations.Where(d => d.DonorUserId != donorId).OrderBy(d => d.Id).Select(d => d.Id).First();
    });

    protected int OwnVolunteerHoursId(string email) => Query(db =>
    {
        var userId = db.Users.Where(u => u.Email == email).Select(u => u.Id).Single();
        return db.VolunteerHours.Where(v => v.UserId == userId).OrderBy(v => v.Id).Select(v => v.Id).First();
    });

    protected int OtherVolunteerHoursId(string email) => Query(db =>
    {
        var userId = db.Users.Where(u => u.Email == email).Select(u => u.Id).Single();
        return db.VolunteerHours.Where(v => v.UserId != userId).OrderBy(v => v.Id).Select(v => v.Id).First();
    });

    protected int OwnActivityId(int companyId) => Query(db =>
        db.CsrActivities.Where(a => a.CompanyId == companyId).OrderBy(a => a.Id).Select(a => a.Id).First());

    protected int OtherActivityId(int companyId) => Query(db =>
        db.CsrActivities.Where(a => a.CompanyId != companyId).OrderBy(a => a.Id).Select(a => a.Id).First());
}

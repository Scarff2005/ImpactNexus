using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Town> Towns => Set<Town>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Stakeholder> Stakeholders => Set<Stakeholder>();
    public DbSet<CsrActivity> CsrActivities => Set<CsrActivity>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<BursaryProgramme> BursaryProgrammes => Set<BursaryProgramme>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<BursaryApplication> BursaryApplications => Set<BursaryApplication>();
    public DbSet<VolunteerProgramme> VolunteerProgrammes => Set<VolunteerProgramme>();
    public DbSet<VolunteerHour> VolunteerHours => Set<VolunteerHour>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The application signs users in with a local password only. It issues no
        // per-user or per-role claims (the factory in CurrentUserService adds them to
        // the cookie), accepts no external logins, and generates password-reset tokens
        // in memory through the DataProtection token provider. Nothing would ever write
        // to Identity's four satellite tables, so they are removed from the model and
        // from the database instead of being carried as empty scaffolding.
        builder.Ignore<IdentityUserClaim<string>>();
        builder.Ignore<IdentityRoleClaim<string>>();
        builder.Ignore<IdentityUserLogin<string>>();
        builder.Ignore<IdentityUserToken<string>>();

        builder.Entity<Company>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
            e.HasOne(c => c.Country).WithMany(c => c.Companies)
                .HasForeignKey(c => c.CountryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Country>(e => e.HasIndex(c => c.Name).IsUnique());

        builder.Entity<Province>(e =>
        {
            e.HasIndex(p => new { p.CountryId, p.Name }).IsUnique();
            e.HasOne(p => p.Country).WithMany(c => c.Provinces)
                .HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Town>(e =>
        {
            e.HasIndex(t => new { t.ProvinceId, t.Name }).IsUnique();
            e.HasOne(t => t.Province).WithMany(p => p.Towns)
                .HasForeignKey(t => t.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Location>(e =>
        {
            e.HasIndex(l => new { l.TownId, l.Name }).IsUnique();
            e.HasOne(l => l.Town).WithMany(t => t.Locations)
                .HasForeignKey(l => l.TownId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Company).WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Stakeholder>(e =>
        {
            e.HasOne(s => s.Company).WithMany(c => c.Stakeholders)
                .HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CsrActivity>(e =>
        {
            e.HasOne(a => a.Company).WithMany(c => c.Activities)
                .HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Location).WithMany(l => l.Activities)
                .HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.Manager).WithMany(u => u.ManagedActivities)
                .HasForeignKey(a => a.ManagerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Donation>(e =>
        {
            e.HasOne(d => d.Company).WithMany(c => c.Donations)
                .HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.DonorUser).WithMany(u => u.Donations)
                .HasForeignKey(d => d.DonorUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.CsrActivity).WithMany(a => a.Donations)
                .HasForeignKey(d => d.CsrActivityId).OnDelete(DeleteBehavior.NoAction);
            e.Property(d => d.Amount).HasPrecision(18, 2);
        });

        builder.Entity<BursaryProgramme>(e =>
        {
            e.HasOne(b => b.Company).WithMany(c => c.BursaryProgrammes)
                .HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.Property(b => b.Budget).HasPrecision(18, 2);
        });

        builder.Entity<Beneficiary>(e =>
        {
            e.HasOne(b => b.Company).WithMany()
                .HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(b => b.Town).WithMany()
                .HasForeignKey(b => b.TownId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(b => b.User).WithMany()
                .HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(b => b.Stakeholder).WithMany(s => s.Beneficiaries)
                .HasForeignKey(b => b.StakeholderId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<BursaryApplication>(e =>
        {
            e.HasOne(a => a.BursaryProgramme).WithMany(p => p.Applications)
                .HasForeignKey(a => a.BursaryProgrammeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Beneficiary).WithMany(b => b.Applications)
                .HasForeignKey(a => a.BeneficiaryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.AmountAwarded).HasPrecision(18, 2);
        });

        builder.Entity<VolunteerProgramme>(e =>
        {
            e.HasOne(p => p.Company).WithMany(c => c.VolunteerProgrammes)
                .HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Location).WithMany(l => l.VolunteerProgrammes)
                .HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<VolunteerHour>(e =>
        {
            e.HasOne(v => v.VolunteerProgramme).WithMany(p => p.VolunteerHours)
                .HasForeignKey(v => v.VolunteerProgrammeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.CsrActivity).WithMany(a => a.VolunteerHours)
                .HasForeignKey(v => v.CsrActivityId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(v => v.User).WithMany(u => u.VolunteerHours)
                .HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.NoAction);
            e.Property(v => v.Hours).HasPrecision(5, 2);
        });
    }
}

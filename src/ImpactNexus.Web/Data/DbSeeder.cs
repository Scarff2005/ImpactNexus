using ImpactNexus.Web.Authorization;
using ImpactNexus.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ImpactNexus.Web.Data;

/// <summary>
/// Development seeding. Only runs when Database:SeedDemoData is true, and is
/// idempotent: every record is created only when missing.
/// </summary>
public static class DbSeeder
{
    public const string AdminEmail = "admin@impactnexus.local";
    public const string AdminPassword = "Admin#2026";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();

        await SeedDataAsync(services);
    }

    /// <summary>
    /// Seeds roles, users and demo records without touching the schema.
    /// Callers are responsible for creating the database first (migrate or EnsureCreated).
    /// </summary>
    public static async Task SeedDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var countries = await SeedLocationsAsync(db);
        var companies = await SeedCompaniesAsync(db, countries);
        var users = await SeedUsersAsync(userManager, companies);
        await SeedProgrammesAsync(db, companies, users, countries);
    }

    private static async Task<Dictionary<string, Country>> SeedLocationsAsync(ApplicationDbContext db)
    {
        var countries = new Dictionary<string, Country>();

        async Task<Country> GetCountry(string name, string iso)
        {
            var country = await db.Countries.FirstOrDefaultAsync(c => c.Name == name);
            if (country is null)
            {
                country = new Country { Name = name, IsoCode = iso };
                db.Countries.Add(country);
                await db.SaveChangesAsync();
            }
            countries[name] = country;
            return country;
        }

        async Task<Province> GetProvince(Country country, string name)
        {
            var province = await db.Provinces.FirstOrDefaultAsync(p => p.CountryId == country.Id && p.Name == name);
            if (province is null)
            {
                province = new Province { Name = name, CountryId = country.Id };
                db.Provinces.Add(province);
                await db.SaveChangesAsync();
            }
            return province;
        }

        async Task<Town> GetTown(Province province, string name)
        {
            var town = await db.Towns.FirstOrDefaultAsync(t => t.ProvinceId == province.Id && t.Name == name);
            if (town is null)
            {
                town = new Town { Name = name, ProvinceId = province.Id };
                db.Towns.Add(town);
                await db.SaveChangesAsync();
            }
            return town;
        }

        async Task<Location> GetLocation(Town town, string name, string address)
        {
            var location = await db.Locations.FirstOrDefaultAsync(l => l.TownId == town.Id && l.Name == name);
            if (location is null)
            {
                location = new Location { Name = name, AddressLine = address, TownId = town.Id };
                db.Locations.Add(location);
                await db.SaveChangesAsync();
            }
            return location;
        }

        var southAfrica = await GetCountry("South Africa", "ZA");
        var gauteng = await GetProvince(southAfrica, "Gauteng");
        var westernCape = await GetProvince(southAfrica, "Western Cape");
        var easternCape = await GetProvince(southAfrica, "Eastern Cape");
        var kwazuluNatal = await GetProvince(southAfrica, "KwaZulu-Natal");
        await GetCountry("Namibia", "NA");

        var johannesburg = await GetTown(gauteng, "Johannesburg");
        await GetTown(gauteng, "Pretoria");
        var capeTown = await GetTown(westernCape, "Cape Town");
        await GetTown(westernCape, "Stellenbosch");
        var gqeberha = await GetTown(easternCape, "Gqeberha");
        var durban = await GetTown(kwazuluNatal, "Durban");

        await GetLocation(johannesburg, "Diepsloot Community Hall", "1 Peach Road, Diepsloot");
        await GetLocation(johannesburg, "Soweto Skills Centre", "42 Vilakazi Street, Orlando West");
        await GetLocation(capeTown, "Khayelitsha Resource Centre", "Site C, Ntlazane Road");
        await GetLocation(gqeberha, "Zwide Primary School", "12 Nkosi Street, Zwide");
        await GetLocation(durban, "Umlazi Technical Hub", "5 Mangosuthu Highway");

        return countries;
    }

    private static async Task<Dictionary<string, Company>> SeedCompaniesAsync(
        ApplicationDbContext db, Dictionary<string, Country> countries)
    {
        var result = new Dictionary<string, Company>();
        var seed = new[]
        {
            new Company
            {
                Name = "BMW South Africa",
                RegistrationNumber = "1994/005512/07",
                Description = "Vehicle manufacturer with an assembly plant in Rosslyn, Pretoria. CSR focus on technical skills development, road safety, and local supplier growth.",
                Website = "https://www.bmw.co.za",
                Email = "csr@bmwsa.example.com",
                Phone = "011 552 7000",
                HeadOffice = "Midrand, Johannesburg",
                CountryId = countries["South Africa"].Id
            },
            new Company
            {
                Name = "Toyota South Africa Motors",
                RegistrationNumber = "1961/001870/07",
                Description = "Vehicle manufacturer and importer with a plant in Prospecton, Durban. CSI focus on education, rural mobility, and learnerships.",
                Website = "https://www.toyota.co.za",
                Email = "impact@toyotasa.example.com",
                Phone = "031 910 5000",
                HeadOffice = "Prospecton, Durban",
                CountryId = countries["South Africa"].Id
            },
            new Company
            {
                Name = "Volkswagen Group South Africa",
                RegistrationNumber = "1955/002041/07",
                Description = "Vehicle manufacturer with a plant in Kariega, Eastern Cape. Community programmes across the Eastern Cape and KwaZulu-Natal.",
                Website = "https://www.vw.co.za",
                Email = "hello@vwsouthafrica.example.com",
                Phone = "041 994 2111",
                HeadOffice = "Kariega, Eastern Cape",
                CountryId = countries["South Africa"].Id
            }
        };

        foreach (var company in seed)
        {
            if (!await db.Companies.AnyAsync(c => c.Name == company.Name))
            {
                db.Companies.Add(company);
                await db.SaveChangesAsync();
            }
            result[company.Name] = await db.Companies.FirstAsync(c => c.Name == company.Name);
        }

        return result;
    }

    private static async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync(
        UserManager<ApplicationUser> userManager, Dictionary<string, Company> companies)
    {
        var result = new Dictionary<string, ApplicationUser>();

        async Task<ApplicationUser> Ensure(string email, string password, string fullName,
            string role, int? companyId)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    CompanyId = companyId,
                    IsActive = true
                };

                var create = await userManager.CreateAsync(user, password);
                if (!create.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to seed user {email}: {string.Join("; ", create.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }

            result[email] = user;
            return user;
        }

        await Ensure(AdminEmail, AdminPassword, "Naledi Khumalo", Roles.Administrator, null);
        await Ensure("csr.manager@impactnexus.local", "Manager#2026", "Thabo Mokoena",
            Roles.CsrManager, companies["BMW South Africa"].Id);
        await Ensure("csr.manager2@impactnexus.local", "Manager#2026", "Riaan van Wyk",
            Roles.CsrManager, companies["Toyota South Africa Motors"].Id);
        await Ensure("investor@impactnexus.local", "Investor#2026", "Amara Okafor",
            Roles.Investor, null);
        await Ensure("investor2@impactnexus.local", "Investor#2026", "Deon Pillay",
            Roles.Investor, null);
        await Ensure("volunteer@impactnexus.local", "Volunteer#2026", "Lerato Ndlovu",
            Roles.CommunityMember, null);
        await Ensure("community@impactnexus.local", "Volunteer#2026", "Sipho Dlamini",
            Roles.CommunityMember, null);

        return result;
    }

    private static async Task SeedProgrammesAsync(
        ApplicationDbContext db,
        Dictionary<string, Company> companies,
        Dictionary<string, ApplicationUser> users,
        Dictionary<string, Country> countries)
    {
        if (await db.CsrActivities.AnyAsync())
        {
            return;
        }

        var bmw = companies["BMW South Africa"];
        var toyota = companies["Toyota South Africa Motors"];
        var vw = companies["Volkswagen Group South Africa"];
        var manager = users["csr.manager@impactnexus.local"];
        var manager2 = users["csr.manager2@impactnexus.local"];
        var investor = users["investor@impactnexus.local"];
        var investor2 = users["investor2@impactnexus.local"];
        var volunteer = users["volunteer@impactnexus.local"];
        var community = users["community@impactnexus.local"];

        var locations = await db.Locations.OrderBy(l => l.Id).ToListAsync();

        var activities = new List<CsrActivity>
        {
            new()
            {
                Title = "Diepsloot Automotive Skills Academy",
                Description = "Twelve-month artisan development programme training 60 young people in vehicle maintenance, diagnostics, and workshop safety.",
                Objectives = "Place 45 graduates in paid apprenticeships; achieve 80% course completion.",
                CompanyId = bmw.Id,
                LocationId = locations[0].Id,
                StartDate = new DateOnly(2026, 1, 15),
                EndDate = new DateOnly(2026, 12, 15),
                Budget = 1_850_000m,
                Status = ActivityStatus.InProgress,
                ImpactIndicator = "Learners certified",
                ManagerId = manager.Id
            },
            new()
            {
                Title = "Soweto Road Safety Programme",
                Description = "School-based road safety education reaching 12 primary schools, with reflective kit distribution and pedestrian crossing upgrades.",
                Objectives = "Reach 9 000 learners; reduce reported pedestrian incidents near participating schools.",
                CompanyId = bmw.Id,
                LocationId = locations[1].Id,
                StartDate = new DateOnly(2026, 3, 1),
                EndDate = new DateOnly(2026, 10, 31),
                Budget = 640_000m,
                Status = ActivityStatus.InProgress,
                ImpactIndicator = "Learners reached",
                ManagerId = manager.Id
            },
            new()
            {
                Title = "Khayelitsha Mobility Bursary Fund",
                Description = "Full-cost bursaries for 25 students from Khayelitsha studying mechanical and electrical engineering.",
                Objectives = "Fund 25 students for the 2026 academic year; maintain a 90% pass rate.",
                CompanyId = toyota.Id,
                LocationId = locations[2].Id,
                StartDate = new DateOnly(2026, 1, 8),
                EndDate = new DateOnly(2026, 11, 30),
                Budget = 1_250_000m,
                Status = ActivityStatus.InProgress,
                ImpactIndicator = "Students funded",
                ManagerId = manager2.Id
            },
            new()
            {
                Title = "Zwide School Mobile Library",
                Description = "Monthly mobile library visits and a refurbished reading room for Zwide Primary School.",
                Objectives = "Deliver 10 visits; lend 4 000 books.",
                CompanyId = toyota.Id,
                LocationId = locations[3].Id,
                StartDate = new DateOnly(2025, 7, 1),
                EndDate = new DateOnly(2026, 6, 30),
                Budget = 320_000m,
                Status = ActivityStatus.Completed,
                ImpactIndicator = "Books lent",
                ManagerId = manager2.Id
            },
            new()
            {
                Title = "Umlazi E-Mobility Technician Hub",
                Description = "Workshop and certification pathway for electric vehicle technicians, run with a local TVET college.",
                Objectives = "Certify 40 technicians; support 10 micro-enterprises.",
                CompanyId = vw.Id,
                LocationId = locations[4].Id,
                StartDate = new DateOnly(2026, 2, 1),
                EndDate = new DateOnly(2027, 1, 31),
                Budget = 980_000m,
                Status = ActivityStatus.Planned,
                ImpactIndicator = "Technicians certified",
                ManagerId = manager2.Id
            }
        };

        db.CsrActivities.AddRange(activities);
        await db.SaveChangesAsync();

        db.Donations.AddRange(
            new Donation
            {
                DonorName = "Amara Okafor", CompanyId = bmw.Id, DonorUserId = investor.Id,
                CsrActivityId = activities[0].Id, Amount = 450_000m,
                DonatedOn = new DateOnly(2026, 1, 10), Status = DonationStatus.Allocated,
                Reference = "DON-2026-0001", Description = "Seed funding for the Diepsloot skills academy workshop equipment."
            },
            new Donation
            {
                DonorName = "Amara Okafor", CompanyId = bmw.Id, DonorUserId = investor.Id,
                CsrActivityId = activities[1].Id, Amount = 120_000m,
                DonatedOn = new DateOnly(2026, 3, 5), Status = DonationStatus.Received,
                Reference = "DON-2026-0002", Description = "Reflective kit and classroom materials for the road safety programme."
            },
            new Donation
            {
                DonorName = "Deon Pillay", CompanyId = toyota.Id, DonorUserId = investor2.Id,
                CsrActivityId = activities[2].Id, Amount = 300_000m,
                DonatedOn = new DateOnly(2026, 1, 6), Status = DonationStatus.Allocated,
                Reference = "DON-2026-0003", Description = "Top-up funding for two additional bursary recipients."
            },
            new Donation
            {
                DonorName = "Deon Pillay", CompanyId = toyota.Id, DonorUserId = investor2.Id,
                CsrActivityId = activities[3].Id, Amount = 75_000m,
                DonatedOn = new DateOnly(2025, 9, 12), Status = DonationStatus.Received,
                Reference = "DON-2025-0091", Description = "Mobile library fuel and book procurement."
            },
            new Donation
            {
                DonorName = "BMW South Africa", CompanyId = bmw.Id,
                CsrActivityId = activities[1].Id, Amount = 250_000m,
                DonatedOn = new DateOnly(2026, 2, 20), Status = DonationStatus.Received,
                Reference = "CORP-2026-0014", Description = "Corporate contribution to road safety infrastructure."
            }
        );

        var bursaryProgramme = new BursaryProgramme
        {
            Name = "Toyota SA Engineering Bursary 2026",
            Description = "Full tuition, accommodation, and textbook funding for undergraduate engineering students.",
            CompanyId = toyota.Id,
            AcademicYear = 2026,
            Budget = 1_200_000m,
            OpenDate = new DateOnly(2025, 8, 1),
            CloseDate = new DateOnly(2025, 10, 31),
            IsActive = true
        };

        var bursaryProgramme2 = new BursaryProgramme
        {
            Name = "BMW SA Technical Skills Bursary 2026",
            Description = "Bursary for diploma students in automotive technology and mechatronics.",
            CompanyId = bmw.Id,
            AcademicYear = 2026,
            Budget = 800_000m,
            OpenDate = new DateOnly(2025, 9, 1),
            CloseDate = new DateOnly(2025, 11, 30),
            IsActive = true
        };

        db.BursaryProgrammes.AddRange(bursaryProgramme, bursaryProgramme2);

        // A currently-open programme so the community "apply" flow can be demonstrated.
        // Its window is relative to seed time so it stays open whenever the demo is set up.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.BursaryProgrammes.Add(new BursaryProgramme
        {
            Name = $"BMW SA Engineering Bursary {today.Year + 1}",
            Description = "Full tuition, accommodation and textbook funding for undergraduate engineering students.",
            CompanyId = bmw.Id,
            AcademicYear = today.Year + 1,
            Budget = 1_350_000m,
            OpenDate = today.AddDays(-21),
            CloseDate = today.AddDays(60),
            IsActive = true
        });

        await db.SaveChangesAsync();

        var beneficiaries = new List<Beneficiary>
        {
            new()
            {
                FullName = "Zanele Mthembu", Email = "zanele.mthembu@example.com",
                Phone = "082 555 0111", CompanyId = toyota.Id, TownId = locations[2].TownId,
                UserId = volunteer.Id,
                Notes = "Second-year mechanical engineering student at CPUT."
            },
            new()
            {
                FullName = "Kagiso Sithole", Email = "kagiso.sithole@example.com",
                Phone = "073 555 0134", CompanyId = bmw.Id, TownId = locations[0].TownId,
                UserId = community.Id,
                Notes = "Automotive technology diploma student."
            }
        };

        db.Beneficiaries.AddRange(beneficiaries);
        await db.SaveChangesAsync();

        db.BursaryApplications.AddRange(
            new BursaryApplication
            {
                BursaryProgrammeId = bursaryProgramme.Id, BeneficiaryId = beneficiaries[0].Id,
                Status = BursaryApplicationStatus.Approved, AmountAwarded = 96_000m,
                AppliedOn = new DateOnly(2025, 9, 2), DecisionOn = new DateOnly(2025, 11, 15),
                Motivation = "Top-quartile academic results with demonstrated financial need.",
                ReviewNotes = "Approved by committee on 15 November 2025."
            },
            new BursaryApplication
            {
                BursaryProgrammeId = bursaryProgramme2.Id, BeneficiaryId = beneficiaries[1].Id,
                Status = BursaryApplicationStatus.UnderReview,
                AppliedOn = new DateOnly(2025, 10, 11),
                Motivation = "Applicant maintains a 72% average and supports two siblings."
            }
        );

        var volunteerProgramme = new VolunteerProgramme
        {
            Name = "BMW SA Skills Mentorship Volunteers",
            Description = "Employee and community volunteers mentor learners at the Diepsloot skills academy.",
            CompanyId = bmw.Id,
            LocationId = locations[0].Id,
            StartDate = new DateOnly(2026, 1, 20),
            EndDate = new DateOnly(2026, 12, 10),
            Status = ProgrammeStatus.Active
        };

        var volunteerProgramme2 = new VolunteerProgramme
        {
            Name = "Toyota Mobile Library Readers",
            Description = "Volunteers run reading circles during mobile library visits.",
            CompanyId = toyota.Id,
            LocationId = locations[2].Id,
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 6, 30),
            Status = ProgrammeStatus.Active
        };

        db.VolunteerProgrammes.AddRange(volunteerProgramme, volunteerProgramme2);
        await db.SaveChangesAsync();

        db.VolunteerHours.AddRange(
            new VolunteerHour
            {
                VolunteerProgrammeId = volunteerProgramme.Id, CsrActivityId = activities[0].Id,
                UserId = volunteer.Id, Hours = 18.5m, Date = new DateOnly(2026, 2, 14),
                Notes = "Workshop mentoring session.", Approved = true
            },
            new VolunteerHour
            {
                VolunteerProgrammeId = volunteerProgramme.Id, CsrActivityId = activities[0].Id,
                UserId = volunteer.Id, Hours = 12m, Date = new DateOnly(2026, 3, 7),
                Notes = "Assessment support.", Approved = true
            },
            new VolunteerHour
            {
                VolunteerProgrammeId = volunteerProgramme.Id, CsrActivityId = activities[1].Id,
                UserId = community.Id, Hours = 8m, Date = new DateOnly(2026, 3, 21),
                Notes = "Road safety campaign hand-out.", Approved = true
            },
            new VolunteerHour
            {
                VolunteerProgrammeId = volunteerProgramme2.Id, CsrActivityId = activities[3].Id,
                UserId = community.Id, Hours = 5.5m, Date = new DateOnly(2026, 4, 4),
                Notes = "Reading circle facilitation.", Approved = false
            }
        );

        db.Stakeholders.AddRange(
            new Stakeholder
            {
                FullName = "Lerato Ndlovu", Email = "volunteer@impactnexus.local",
                Phone = "081 555 0166", Type = StakeholderType.Volunteer,
                CompanyId = bmw.Id, UserId = volunteer.Id,
                Notes = "Community volunteer and bursary beneficiary."
            },
            new Stakeholder
            {
                FullName = "Sipho Dlamini", Email = "community@impactnexus.local",
                Phone = "084 555 0188", Type = StakeholderType.CommunityMember,
                CompanyId = bmw.Id, UserId = community.Id
            },
            new Stakeholder
            {
                FullName = "Zwide Primary School", Email = "admin@zwideprimary.example.com",
                Phone = "041 555 0102", Type = StakeholderType.Partner,
                CompanyId = toyota.Id,
                Notes = "Programme partner for the mobile library initiative."
            }
        );

        await db.SaveChangesAsync();
    }
}

using System.ComponentModel.DataAnnotations;

namespace ImpactNexus.Web.Models;

public class Company
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Company name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Registration number")]
    public string? RegistrationNumber { get; set; }

    [StringLength(80)]
    public string Industry { get; set; } = "Automotive";

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    [Url]
    public string? Website { get; set; }

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [StringLength(250)]
    [Display(Name = "Head office")]
    public string? HeadOffice { get; set; }

    [Display(Name = "Country")]
    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<CsrActivity> Activities { get; set; } = new List<CsrActivity>();
    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
    public ICollection<BursaryProgramme> BursaryProgrammes { get; set; } = new List<BursaryProgramme>();
    public ICollection<VolunteerProgramme> VolunteerProgrammes { get; set; } = new List<VolunteerProgramme>();
    public ICollection<Stakeholder> Stakeholders { get; set; } = new List<Stakeholder>();
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ImpactNexus.Web.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Company")]
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
    public ICollection<VolunteerHour> VolunteerHours { get; set; } = new List<VolunteerHour>();
    public ICollection<CsrActivity> ManagedActivities { get; set; } = new List<CsrActivity>();
}

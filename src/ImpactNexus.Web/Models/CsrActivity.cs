using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImpactNexus.Web.Models;

public class CsrActivity
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Objectives { get; set; }

    [Required]
    [Display(Name = "Company")]
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Location")]
    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateOnly? EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999_999_999)]
    public decimal Budget { get; set; }

    [Required]
    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    [StringLength(100)]
    [Display(Name = "Impact indicator")]
    public string? ImpactIndicator { get; set; }

    [Display(Name = "Project lead")]
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
    public ICollection<VolunteerHour> VolunteerHours { get; set; } = new List<VolunteerHour>();
}

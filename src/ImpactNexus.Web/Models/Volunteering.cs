using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImpactNexus.Web.Models;

public class VolunteerProgramme
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

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

    [Required]
    public ProgrammeStatus Status { get; set; } = ProgrammeStatus.Planned;

    public ICollection<VolunteerHour> VolunteerHours { get; set; } = new List<VolunteerHour>();
}

public class VolunteerHour
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Programme")]
    public int VolunteerProgrammeId { get; set; }
    public VolunteerProgramme? VolunteerProgramme { get; set; }

    [Display(Name = "Activity")]
    public int? CsrActivityId { get; set; }
    public CsrActivity? CsrActivity { get; set; }

    [Required]
    [Display(Name = "Volunteer account")]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    [Range(0.25, 24)]
    public decimal Hours { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date worked")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool Approved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

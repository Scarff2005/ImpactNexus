using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImpactNexus.Web.Models;

public class BursaryProgramme
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

    [Range(2000, 2100)]
    [Display(Name = "Academic year")]
    public int AcademicYear { get; set; } = DateTime.UtcNow.Year;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999_999_999)]
    public decimal Budget { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Opens")]
    public DateOnly OpenDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    [Display(Name = "Closes")]
    public DateOnly CloseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True when the programme is active and today falls inside its application window.
    /// Not mapped: it is a presentation convenience, not a stored column.
    /// </summary>
    [NotMapped]
    public bool IsAcceptingApplications
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return IsActive && OpenDate <= today && CloseDate >= today;
        }
    }

    public ICollection<BursaryApplication> Applications { get; set; } = new List<BursaryApplication>();
}

public class Beneficiary
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [Display(Name = "Town")]
    public int? TownId { get; set; }
    public Town? Town { get; set; }

    [Display(Name = "Company")]
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Linked user account")]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Display(Name = "Stakeholder record")]
    public int? StakeholderId { get; set; }
    public Stakeholder? Stakeholder { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BursaryApplication> Applications { get; set; } = new List<BursaryApplication>();
}

public class BursaryApplication
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Programme")]
    public int BursaryProgrammeId { get; set; }
    public BursaryProgramme? BursaryProgramme { get; set; }

    [Required]
    [Display(Name = "Beneficiary")]
    public int BeneficiaryId { get; set; }
    public Beneficiary? Beneficiary { get; set; }

    [Required]
    public BursaryApplicationStatus Status { get; set; } = BursaryApplicationStatus.Submitted;

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 999_999_999)]
    [Display(Name = "Amount awarded")]
    public decimal? AmountAwarded { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Applied on")]
    public DateOnly AppliedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    [Display(Name = "Decision date")]
    public DateOnly? DecisionOn { get; set; }

    [Required, StringLength(2000)]
    public string Motivation { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Review notes")]
    public string? ReviewNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

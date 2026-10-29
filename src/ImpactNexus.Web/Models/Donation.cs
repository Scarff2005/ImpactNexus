using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImpactNexus.Web.Models;

public class Donation
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Donor name")]
    public string DonorName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Company")]
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Investor account")]
    public string? DonorUserId { get; set; }
    public ApplicationUser? DonorUser { get; set; }

    [Display(Name = "Activity")]
    public int? CsrActivityId { get; set; }
    public CsrActivity? CsrActivity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 999_999_999)]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Donation date")]
    public DateOnly DonatedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DonationStatus Status { get; set; } = DonationStatus.Received;

    [StringLength(60)]
    public string? Reference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

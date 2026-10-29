using System.ComponentModel.DataAnnotations;

namespace ImpactNexus.Web.Models;

public class Stakeholder
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [Required]
    public StakeholderType Type { get; set; } = StakeholderType.CommunityMember;

    [Display(Name = "Company")]
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Display(Name = "Linked user account")]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
}

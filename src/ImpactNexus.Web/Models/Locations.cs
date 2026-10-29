using System.ComponentModel.DataAnnotations;

namespace ImpactNexus.Web.Models;

public class Country
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(3)]
    [Display(Name = "ISO code")]
    public string? IsoCode { get; set; }

    public ICollection<Province> Provinces { get; set; } = new List<Province>();
    public ICollection<Company> Companies { get; set; } = new List<Company>();
}

public class Province
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Country")]
    public int CountryId { get; set; }
    public Country? Country { get; set; }

    public ICollection<Town> Towns { get; set; } = new List<Town>();
}

public class Town
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Province")]
    public int ProvinceId { get; set; }
    public Province? Province { get; set; }

    public ICollection<Models.Location> Locations { get; set; } = new List<Models.Location>();
  }

public class Location
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    [Display(Name = "Address")]
    public string? AddressLine { get; set; }

    [Required]
    [Display(Name = "Town")]
    public int TownId { get; set; }
    public Town? Town { get; set; }

    public ICollection<CsrActivity> Activities { get; set; } = new List<CsrActivity>();
    public ICollection<VolunteerProgramme> VolunteerProgrammes { get; set; } = new List<VolunteerProgramme>();
}

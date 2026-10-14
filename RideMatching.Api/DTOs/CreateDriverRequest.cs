using System.ComponentModel.DataAnnotations;

namespace RideMatching.Api.DTOs;
public class CreateDriverRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

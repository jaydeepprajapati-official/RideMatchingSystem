
using System.ComponentModel.DataAnnotations;

namespace RideMatching.Api.DTOs;

public class DriverActionRequest
{
    [Required]
    public Guid DriverId { get; set; }
}

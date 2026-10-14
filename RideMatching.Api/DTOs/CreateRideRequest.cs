using System.ComponentModel.DataAnnotations;

namespace RideMatching.Api.DTOs;
public class CreateRideRequest
{
    [Required]
    public string RiderId { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double PickupLatitude { get; set; }

    [Range(-180, 180)]
    public double PickupLongitude { get; set; }

    [Range(-90, 90)]
    public double DestinationLatitude { get; set; }

    [Range(-180, 180)]
    public double DestinationLongitude { get; set; }
}

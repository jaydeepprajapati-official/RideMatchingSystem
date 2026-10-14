using RideMatching.Api.Enums;
namespace RideMatching.Api.Entities;

public class Ride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RiderId { get; set; } = string.Empty;
    public Guid? DriverId { get; set; }
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public RideStatus Status { get; set; } = RideStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

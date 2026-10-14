namespace RideMatching.Api.Enums;
public enum RideStatus
{
    Pending = 0,
    Matching = 1,
    DriverAssigned = 2,
    Accepted = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
    NoDriverAvailable = 7
}
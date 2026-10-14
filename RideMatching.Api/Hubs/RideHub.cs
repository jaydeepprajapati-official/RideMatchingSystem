using Microsoft.AspNetCore.SignalR;

namespace RideMatching.Api.Hubs;

public sealed class RideHub : Hub
{
    // Demo only: production must authorize group membership.
    public Task JoinRide(string rideId) =>
        Groups.AddToGroupAsync(
            Context.ConnectionId, $"ride:{rideId}");

    public Task JoinDriver(string driverId) =>
        Groups.AddToGroupAsync(
            Context.ConnectionId, $"driver:{driverId}");
}

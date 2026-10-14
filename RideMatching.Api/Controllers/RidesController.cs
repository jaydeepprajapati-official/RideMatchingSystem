
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideMatching.Api.Data;
using RideMatching.Api.DTOs;
using RideMatching.Api.Entities;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/v1/rides")]
public class RidesController : ControllerBase
{
    private readonly RideDbContext _db;

    public RidesController(RideDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateRideRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RiderId))
        {
            return BadRequest(new
            {
                message = "RiderId is required."
            });
        }

        var ride = new Ride
        {
            RiderId = request.RiderId.Trim(),
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            DestinationLatitude = request.DestinationLatitude,
            DestinationLongitude = request.DestinationLongitude
        };

        var job = new MatchingJob
        {
            RideId = ride.Id
        };

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.Rides.Add(ride);
        _db.MatchingJobs.Add(job);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AcceptedAtAction(
            nameof(GetById),
            new { id = ride.Id },
            new
            {
                ride.Id,
                Status = ride.Status.ToString(),
                Message = "Ride created and queued for matching."
            });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ride = await _db.Rides
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (ride is null)
            return NotFound();

        return Ok(new
        {
            ride.Id,
            ride.RiderId,
            ride.DriverId,
            Status = ride.Status.ToString(),
            ride.PickupLatitude,
            ride.PickupLongitude,
            ride.DestinationLatitude,
            ride.DestinationLongitude,
            ride.CreatedAt,
            ride.UpdatedAt
        });
    }
}


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideMatching.Api.Data;
using RideMatching.Api.DTOs;
using System.Data;
using RideMatching.Api.Entities;
using RideMatching.Api.Enums;

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

        var riderId = request.RiderId.Trim();

        // Use a serializable transaction to prevent race conditions where
        // two concurrent requests create duplicate pending rides for the
        // same rider.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Don't allow creating a new ride if there is already a pending
        // or active ride for this rider. Active statuses include: Pending,
        // Matching, DriverAssigned, Accepted, InProgress.
        var disallowedStatuses = new[]
        {
            RideStatus.Pending,
            RideStatus.Matching,
            RideStatus.DriverAssigned,
            RideStatus.Accepted,
            RideStatus.InProgress
        };

        var existing = await _db.Rides
            .AsNoTracking()
            .Where(x => x.RiderId == riderId)
            .FirstOrDefaultAsync(x => disallowedStatuses.Contains(x.Status), cancellationToken);

        if (existing is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new
            {
                message = "An active or pending ride already exists for this rider.",
                existingRideId = existing.Id,
                existingStatus = existing.Status.ToString()
            });
        }

        // If there is an existing ride where no driver was available previously,
        // allow re-queuing that ride instead of creating a new one. Update the
        // ride status back to Pending and add a fresh MatchingJob.
        var existingNoDriver = await _db.Rides
            .FirstOrDefaultAsync(x => x.RiderId == riderId
                                   && x.Status == RideStatus.NoDriverAvailable,
                                   cancellationToken);

        if (existingNoDriver is not null)
        {
            existingNoDriver.Status = RideStatus.Pending;
            existingNoDriver.UpdatedAt = DateTime.UtcNow;

            var existingJob = await _db.MatchingJobs
                .FirstOrDefaultAsync(x => x.RideId == existingNoDriver.Id, cancellationToken);

            if (existingJob is not null)
            {
                // Re-queue by updating the existing matching job instead of inserting
                // a new one (unique index on RideId prevents duplicate rows).
                existingJob.Status = "Pending";
                existingJob.AttemptCount = 0;
                existingJob.NextAttemptAt = DateTime.UtcNow;
            }
            else
            {
                var retryJob = new MatchingJob
                {
                    RideId = existingNoDriver.Id
                };

                _db.MatchingJobs.Add(retryJob);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return AcceptedAtAction(
                nameof(GetById),
                new { id = existingNoDriver.Id },
                new
                {
                    existingNoDriver.Id,
                    Status = existingNoDriver.Status.ToString(),
                    Message = "Existing ride re-queued for matching (no driver previously available)."
                });
        }

        var ride = new Ride
        {
            RiderId = riderId,
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

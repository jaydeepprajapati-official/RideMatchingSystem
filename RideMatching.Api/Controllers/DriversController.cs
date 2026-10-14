
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideMatching.Api.Data;
using RideMatching.Api.DTOs;
using RideMatching.Api.Entities;
using RideMatching.Api.Enums;
using RideMatching.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/v1/drivers")]
public class DriversController : ControllerBase
{
    private readonly RideDbContext _db;
    private readonly IHubContext<RideHub> _hub;

    public DriversController(RideDbContext db, IHubContext<RideHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateDriverRequest request,
        CancellationToken cancellationToken)
    {
        var driver = new Driver
        {
            Name = request.Name.Trim()
        };

        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = driver.Id },
            driver);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var driver = await _db.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return driver is null ? NotFound() : Ok(driver);
    }

    [HttpPost("{id:guid}/online")]
    public async Task<IActionResult> GoOnline(
        Guid id,
        CancellationToken cancellationToken)
    {
        var driver = await _db.Drivers.FindAsync(
            new object[] { id }, cancellationToken);

        if (driver is null)
            return NotFound();

        driver.IsOnline = true;
        driver.IsAvailable = true;
        driver.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            driver.Id,
            driver.IsOnline,
            driver.IsAvailable
        });
    }

    [HttpPost("{id:guid}/offline")]
    public async Task<IActionResult> GoOffline(
        Guid id,
        CancellationToken cancellationToken)
    {
        var driver = await _db.Drivers.FindAsync(
            new object[] { id }, cancellationToken);

        if (driver is null)
            return NotFound();

        if (!driver.IsAvailable)
            return Conflict(new
            {
                message = "Driver has an active assignment."
            });

        driver.IsOnline = false;
        driver.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Driver is offline." });
    }

    [HttpPut("{id:guid}/location")]
    public async Task<IActionResult> UpdateLocation(
        Guid id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var driver = await _db.Drivers.FindAsync(
            new object[] { id }, cancellationToken);

        if (driver is null)
            return NotFound();

        driver.Latitude = request.Latitude;
        driver.Longitude = request.Longitude;
        driver.LastLocationAt = DateTime.UtcNow;
        driver.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            driver.Id,
            driver.Latitude,
            driver.Longitude,
            driver.LastLocationAt
        });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(
        Guid id,
        DriverActionRequest request,
        CancellationToken cancellationToken)
    {
        var changed = await _db.Rides
            .Where(x => x.Id == id
                     && x.DriverId == request.DriverId
                     && x.Status == RideStatus.DriverAssigned)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RideStatus.Accepted)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        if (changed == 0)
            return Conflict(new
            {
                message = "Ride is not assigned to this driver or is no longer awaiting acceptance."
            });

        await _hub.Clients.Group($"ride:{id}")
            .SendAsync("RideStatusChanged", new
            {
                RideId = id,
                Status = RideStatus.Accepted.ToString()
            }, cancellationToken);

        return Ok(new { RideId = id, Status = "Accepted" });
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(
        Guid id,
        DriverActionRequest request,
        CancellationToken cancellationToken)
    {
        var changed = await _db.Rides
            .Where(x => x.Id == id
                     && x.DriverId == request.DriverId
                     && x.Status == RideStatus.Accepted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, RideStatus.InProgress)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        if (changed == 0)
            return Conflict(new
            {
                message = "Ride cannot be started in its current state."
            });

        await _hub.Clients.Group($"ride:{id}")
            .SendAsync("RideStatusChanged", new
            {
                RideId = id,
                Status = RideStatus.InProgress.ToString()
            }, cancellationToken);

        return Ok(new { RideId = id, Status = "InProgress" });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid id,
        DriverActionRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        var ride = await _db.Rides
            .FirstOrDefaultAsync(
                x => x.Id == id
                  && x.DriverId == request.DriverId
                  && x.Status == RideStatus.InProgress,
                cancellationToken);

        if (ride is null)
            return Conflict(new
            {
                message = "Ride cannot be completed in its current state."
            });

        ride.Status = RideStatus.Completed;
        ride.UpdatedAt = DateTime.UtcNow;

        var driver = await _db.Drivers
            .FirstOrDefaultAsync(
                x => x.Id == request.DriverId,
                cancellationToken);

        if (driver is not null && driver.IsOnline)
        {
            driver.IsAvailable = true;
            driver.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _hub.Clients.Group($"ride:{id}")
            .SendAsync("RideStatusChanged", new
            {
                RideId = id,
                Status = RideStatus.Completed.ToString()
            }, cancellationToken);

        return Ok(new { RideId = id, Status = "Completed" });
    }
}


using Microsoft.EntityFrameworkCore;
using RideMatching.Api.Data;
using RideMatching.Api.Entities;
using RideMatching.Api.Enums;
using RideMatching.Api.Services;
using Microsoft.AspNetCore.SignalR;
using RideMatching.Api.Hubs;


namespace RideMatching.Api.BackgroundJobs;

public sealed class RideMatchingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RideMatchingWorker> _logger;
    private readonly IHubContext<RideHub> _hub;

    public RideMatchingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RideMatchingWorker> logger,
        IHubContext<RideHub> hub)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOneJobAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ride matching job failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task ProcessOneJobAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RideDbContext>();

        var now = DateTime.UtcNow;

        var job = await db.MatchingJobs
            .AsNoTracking()
            .Where(x => x.Status == "Pending"
                     && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
            return;

        // Claim the job atomically. Only one worker can change
        // this job from Pending to Processing successfully.
        var claimed = await db.MatchingJobs
            .Where(x => x.Id == job.Id && x.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Processing")
                .SetProperty(x => x.AttemptCount,
                    x => x.AttemptCount + 1),
                cancellationToken);

        if (claimed == 0)
            return;

        var ride = await db.Rides
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == job.RideId, cancellationToken);

        if (ride is null)
        {
            await SetJobStatusAsync(
                db, job.Id, "Failed", cancellationToken);
            return;
        }

        var freshnessCutoff = DateTime.UtcNow.AddSeconds(-30);

        var drivers = await db.Drivers
            .AsNoTracking()
            .Where(x => x.IsOnline
                     && x.IsAvailable
                     && x.LastLocationAt != null
                     && x.LastLocationAt >= freshnessCutoff)
            .ToListAsync(cancellationToken);

        var candidates = drivers
            .Select(driver => new
            {
                Driver = driver,
                Distance = DistanceCalculator.CalculateKm(
                    ride.PickupLatitude,
                    ride.PickupLongitude,
                    driver.Latitude,
                    driver.Longitude)
            })
            .Where(x => x.Distance <= 5.0)
            .OrderBy(x => x.Distance)
            .ToList();

        foreach (var candidate in candidates)
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    cancellationToken);

            // The database, not the earlier candidate query,
            // decides whether the driver is still available.
            var reserved = await db.Drivers
                .Where(x => x.Id == candidate.Driver.Id
                         && x.IsOnline
                         && x.IsAvailable
                         && x.LastLocationAt != null
                         && x.LastLocationAt >= freshnessCutoff)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsAvailable, false)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);

            if (reserved == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }

            var assigned = await db.Rides
                .Where(x => x.Id == ride.Id
                         && x.Status == RideStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.DriverId,
                        (Guid?)candidate.Driver.Id)
                    .SetProperty(x => x.Status,
                        RideStatus.DriverAssigned)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);

            if (assigned == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }

            await db.MatchingJobs
                .Where(x => x.Id == job.Id
                         && x.Status == "Processing")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Completed"),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await _hub.Clients.Group($"ride:{ride.Id}")
                .SendAsync("RideMatched", new
                {
                    RideId = ride.Id,
                    DriverId = candidate.Driver.Id,
                    DistanceKm = Math.Round(candidate.Distance, 2)
                }, cancellationToken);

            await _hub.Clients.Group($"driver:{candidate.Driver.Id}")
                .SendAsync("RideAssigned", new
                {
                    RideId = ride.Id,
                    RiderId = ride.RiderId
                }, cancellationToken);

            _logger.LogInformation(
                "Ride {RideId} assigned to driver {DriverId}, distance {DistanceKm:F2} km.",
                ride.Id, candidate.Driver.Id, candidate.Distance);

            return;
        }

        // MVP policy: finish this attempt when no eligible driver
        // could be reserved. Retry policies can be added later.
        await db.Rides
            .Where(x => x.Id == ride.Id
                     && x.Status == RideStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status,
                    RideStatus.NoDriverAvailable)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        await SetJobStatusAsync(
            db, job.Id, "Completed", cancellationToken);
    }

    private static async Task SetJobStatusAsync(
        RideDbContext db,
        Guid jobId,
        string status,
        CancellationToken cancellationToken)
    {
        await db.MatchingJobs
            .Where(x => x.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, status),
                cancellationToken);
    }
}

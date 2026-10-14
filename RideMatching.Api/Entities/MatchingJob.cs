namespace RideMatching.Api.Entities;
public class MatchingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RideId { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

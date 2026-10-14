using Microsoft.EntityFrameworkCore;
using RideMatching.Api.Entities;

namespace RideMatching.Api.Data
{
    public class RideDbContext : DbContext
    {
        public RideDbContext(DbContextOptions<RideDbContext> options) : base(options)
        {
        }

        public DbSet<Ride> Rides => Set<Ride>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<MatchingJob> MatchingJobs => Set<MatchingJob>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Driver>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.IsOnline,
                    x.IsAvailable
                });
            });

            modelBuilder.Entity<Ride>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.RiderId)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.DriverId);
            });

            modelBuilder.Entity<MatchingJob>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => new
                {
                    x.Status,
                    x.NextAttemptAt
                });

                entity.HasIndex(x => x.RideId)
                    .IsUnique();

                entity.Property(x => x.Status)
                    .HasMaxLength(30)
                    .IsRequired();
            });
        }
    }
}

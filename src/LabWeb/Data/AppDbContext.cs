using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Point> Points => Set<Point>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<StatusReport> StatusReports => Set<StatusReport>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Operator>(e =>
        {
            e.HasIndex(x => x.Phone);
        });

        b.Entity<Service>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Point>(e =>
        {
            e.HasOne(x => x.Operator)
             .WithMany(o => o.Points)
             .HasForeignKey(x => x.OperatorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Services)
             .WithMany(s => s.Points)
             .UsingEntity(j => j.ToTable("PointServices"));

            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.Settlement);
            e.HasIndex(x => x.Status);
        });

        b.Entity<StatusReport>(e =>
        {
            e.HasOne(x => x.Point)
             .WithMany(p => p.Reports)
             .HasForeignKey(x => x.PointId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.PointId, x.CreatedAt });
        });
    }
}

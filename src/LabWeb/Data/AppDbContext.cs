using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Point> Points => Set<Point>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<StatusReport> StatusReports => Set<StatusReport>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<BoardItem> BoardItems => Set<BoardItem>();
    public DbSet<GuideBlock> GuideBlocks => Set<GuideBlock>();
    public DbSet<PointSubscription> PointSubscriptions => Set<PointSubscription>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Community>(e =>
        {
            e.Property(x => x.Plan).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.InviteCode).IsUnique();
        });

        b.Entity<AppUser>(e =>
        {
            e.HasOne(x => x.Community)
             .WithMany(c => c.Users)
             .HasForeignKey(x => x.CommunityId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.GoogleId).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.ApiTokenHash).IsUnique();
        });

        b.Entity<Operator>(e =>
        {
            e.HasOne(x => x.Community)
             .WithMany(c => c.Operators)
             .HasForeignKey(x => x.CommunityId)
             .OnDelete(DeleteBehavior.Restrict);

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

        b.Entity<PointSubscription>(e =>
        {
            e.HasKey(x => new { x.UserId, x.PointId });
            e.HasIndex(x => x.PointId);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.PointName).HasMaxLength(120);
            e.HasOne(x => x.Point).WithMany().HasForeignKey(x => x.PointId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.UserId, x.EventId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ReadAt });
        });

        b.Entity<GuideBlock>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Text).HasMaxLength(5000);
            e.HasOne(x => x.Community).WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CommunityId, x.Position });
        });

        b.Entity<BoardItem>(e =>
        {
            e.HasOne(x => x.Community)
             .WithMany()
             .HasForeignKey(x => x.CommunityId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Author)
             .WithMany()
             .HasForeignKey(x => x.AuthorId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Points).HasColumnType("jsonb");
            e.HasIndex(x => x.CommunityId);
        });

        b.Entity<Settlement>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Region).HasMaxLength(120);
            e.HasIndex(x => x.Name);
        });
    }
}

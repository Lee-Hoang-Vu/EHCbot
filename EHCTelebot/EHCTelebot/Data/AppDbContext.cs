using EHCTelebot.Models;
using Microsoft.EntityFrameworkCore;

namespace EHCTelebot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<DailyNotificationLog> DailyNotificationLogs
        => Set<DailyNotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(x => x.ChatId);

            entity.Property(x => x.ChatId)
                .HasColumnName("ChatId");

            entity.Property(x => x.Name)
                .HasColumnName("Name")
                .HasMaxLength(100);

            entity.Property(x => x.StartDate)
                .HasColumnName("StartDate")
                .HasColumnType("date");

            entity.Property(x => x.ChatState)
                .HasColumnName("ChatState")
                .HasDefaultValue(2);

            entity.Property(x => x.IsActive)
                .HasColumnName("IsActive")
                .HasDefaultValue(true);

            entity.Property(x => x.LastNotificationDate)
                .HasColumnName("LastNotificationDate")
                .HasColumnType("date");
        });

        modelBuilder.Entity<DailyNotificationLog>(entity =>
        {
            entity.ToTable("DailyNotificationLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ChatId)
                .IsRequired();

            entity.Property(x => x.NotificationDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(x => x.SentAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(x => x.ErrorMessage)
                .HasColumnType("text");

            entity.HasIndex(x => new
            {
                x.ChatId,
                x.NotificationDate
            })
            .IsUnique();
        });
    }
}
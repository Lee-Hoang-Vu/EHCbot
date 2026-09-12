using Microsoft.EntityFrameworkCore;
using EHCTelebot.Models;
namespace EHCTelebot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

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
                    .HasDefaultValue(0);

                entity.Property(x => x.IsActive)
                    .HasColumnName("IsActive")
                    .HasDefaultValue(true);

                entity.Property(x => x.LastNotificationDate)
                    .HasColumnName("LastNotificationDate")
                    .HasColumnType("date");
            });
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Memesploding.Shared.Entities;

namespace Memesploding.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Friendship> Friendships { get; set; } = null!;
    public DbSet<CardSet> CardSets { get; set; } = null!;
    public DbSet<Card> Cards { get; set; } = null!;
    public DbSet<Match> Matches { get; set; } = null!;
    public DbSet<MatchParticipant> MatchParticipants { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User Mapping (bao gồm các trường game stats sau khi gộp Profile)
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProviderId).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.Bio).HasMaxLength(255);
        });

        // 2. Friendship Mapping (Composite Key User1 + User2)
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("friendships");
            entity.HasKey(e => new { e.UserId1, e.UserId2 });
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.HasOne(f => f.User1)
                  .WithMany(u => u.InitiatedFriendships)
                  .HasForeignKey(f => f.UserId1)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.User2)
                  .WithMany(u => u.ReceivedFriendships)
                  .HasForeignKey(f => f.UserId2)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. CardSet & Card Mapping
        modelBuilder.Entity<CardSet>(entity =>
        {
            entity.ToTable("card_sets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            
            entity.HasMany(cs => cs.Cards)
                  .WithOne(c => c.CardSet)
                  .HasForeignKey(c => c.CardSetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Card>(entity =>
        {
            entity.ToTable("cards");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        // 4. Match & MatchParticipant Mapping
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("matches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Settings).HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Stats).HasColumnType("jsonb").IsRequired();

            entity.HasOne(m => m.Winner)
                  .WithMany()
                  .HasForeignKey(m => m.WinnerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.ToTable("match_participants");
            entity.HasKey(e => new { e.MatchId, e.UserId });

            entity.HasOne(mp => mp.Match)
                  .WithMany(m => m.Participants)
                  .HasForeignKey(mp => mp.MatchId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mp => mp.User)
                  .WithMany(u => u.MatchParticipants)
                  .HasForeignKey(mp => mp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. Notification Mapping
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("jsonb");

            entity.HasOne(n => n.Receiver)
                  .WithMany(u => u.ReceivedNotifications)
                  .HasForeignKey(n => n.ReceiverId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Sender)
                  .WithMany(u => u.SentNotifications)
                  .HasForeignKey(n => n.SenderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

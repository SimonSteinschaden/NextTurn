using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Entities;

namespace NextTurn.Api.Data;

public class NextTurnDbContext(DbContextOptions<NextTurnDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<PlayerSession> Sessions => Set<PlayerSession>();
    public DbSet<GameInteraction> Interactions => Set<GameInteraction>();
    public DbSet<InteractionEvent> InteractionEvents => Set<InteractionEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasPostgresExtension("pg_trgm");
        var games = model.Entity<Game>();
        games.HasKey(g => g.BggId);
        games.Property(g => g.BggId).ValueGeneratedNever();
        games.Property(g => g.Name).HasMaxLength(500);
        games.HasIndex(g => g.BggRank);
        games.HasIndex(g => g.AverageRating);
        games.HasIndex(g => g.YearPublished);
        games.HasIndex(g => g.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
        games.HasIndex(g => g.Categories).HasMethod("gin");
        games.HasIndex(g => g.Mechanics).HasMethod("gin");
        games.HasIndex(g => g.DetailsUpdatedAt);
        model.Entity<PlayerSession>().HasKey(s => s.Id);
        model.Entity<PlayerSession>().Property(s => s.Id).HasMaxLength(100);
        var interactions = model.Entity<GameInteraction>();
        interactions.HasKey(i => new { i.SessionId, i.BggId });
        interactions.Property(i => i.SessionId).HasMaxLength(100);
        interactions
            .HasOne(i => i.Game)
            .WithMany()
            .HasForeignKey(i => i.BggId)
            .OnDelete(DeleteBehavior.Restrict);
        interactions
            .HasOne<PlayerSession>()
            .WithMany()
            .HasForeignKey(i => i.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        interactions.HasIndex(i => new { i.SessionId, i.Action });
        model.Entity<InteractionEvent>().HasKey(e => e.Id);
        model.Entity<InteractionEvent>().Property(e => e.SessionId).HasMaxLength(100);
        model.Entity<InteractionEvent>().HasIndex(e => new { e.SessionId, e.CreatedAt });
        model
            .Entity<InteractionEvent>()
            .HasOne<PlayerSession>()
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        model
            .Entity<InteractionEvent>()
            .HasOne<Game>()
            .WithMany()
            .HasForeignKey(e => e.BggId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

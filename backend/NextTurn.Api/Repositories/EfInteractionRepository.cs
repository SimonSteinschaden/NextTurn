using System.Data;
using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Data;
using NextTurn.Api.Entities;

namespace NextTurn.Api.Repositories;

public class EfInteractionRepository(NextTurnDbContext db) : IInteractionRepository
{
    public Task<List<GameInteraction>> CurrentAsync(string sessionId, CancellationToken ct) =>
        db
            .Interactions.AsNoTracking()
            .Where(i => i.SessionId == sessionId)
            .Select(i => new GameInteraction
            {
                SessionId = i.SessionId,
                BggId = i.BggId,
                Action = i.Action,
                IsOwned = i.IsOwned,
                UpdatedAt = i.UpdatedAt,
                Game = new Game
                {
                    BggId = i.Game.BggId,
                    Designers = i.Game.Designers,
                    BggRank = i.Game.BggRank,
                    Categories = i.Game.Categories,
                    Mechanics = i.Game.Mechanics,
                    AverageRating = i.Game.AverageRating,
                    ComplexityWeight = i.Game.ComplexityWeight,
                    PlayingTime = i.Game.PlayingTime,
                    MinPlayTime = i.Game.MinPlayTime,
                    MaxPlayTime = i.Game.MaxPlayTime,
                    MinPlayers = i.Game.MinPlayers,
                    MaxPlayers = i.Game.MaxPlayers,
                    BestPlayerCounts = i.Game.BestPlayerCounts,
                    RecommendedPlayerCounts = i.Game.RecommendedPlayerCounts,
                },
            })
            .ToListAsync(ct);

    public async Task SaveAsync(
        string sessionId,
        int gameId,
        SwipeAction action,
        CancellationToken ct
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        // Serialize changes per session, including first-ever concurrent requests.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({sessionId}, 0))",
            ct
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Sessions" ("Id", "CreatedAt") VALUES ({sessionId}, {DateTimeOffset.UtcNow}) ON CONFLICT ("Id") DO NOTHING
            """,
            ct
        );
        var current = await db.Interactions.FindAsync([sessionId, gameId], ct);
        if (current?.Action == action)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (current == null)
        {
            current = new() { SessionId = sessionId, BggId = gameId };
            db.Interactions.Add(current);
        }
        current.Action = action;
        current.UpdatedAt = DateTimeOffset.UtcNow;
        db.InteractionEvents.Add(
            new()
            {
                SessionId = sessionId,
                BggId = gameId,
                Action = action,
                CreatedAt = current.UpdatedAt,
            }
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task SetOwnershipAsync(
        string sessionId,
        int gameId,
        bool owned,
        CancellationToken ct
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({sessionId}, 0))",
            ct
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Sessions" ("Id", "CreatedAt") VALUES ({sessionId}, {DateTimeOffset.UtcNow}) ON CONFLICT ("Id") DO NOTHING
            """,
            ct
        );
        var current = await db.Interactions.FindAsync([sessionId, gameId], ct);
        if (current == null)
        {
            current = new() { SessionId = sessionId, BggId = gameId };
            db.Interactions.Add(current);
        }
        current.IsOwned = owned;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}

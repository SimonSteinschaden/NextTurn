using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Data;
using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;

namespace NextTurn.Api.Repositories;

public class EfGameNightRepository(NextTurnDbContext db) : IGameNightRepository
{
    private IQueryable<Game> Available(GameNight night) =>
        db
            .Games.AsNoTracking()
            .Where(g =>
                g.DetailsUpdatedAt != null
                && g.MinPlayers > 0
                && g.MaxPlayers > 0
                && g.MinPlayers <= night.Players
                && g.MaxPlayers >= night.Players
                && db.Interactions.Any(i =>
                    i.SessionId == night.SessionId
                    && i.BggId == g.BggId
                    && (
                        i.IsOwned
                        || (
                            night.IncludeUnowned
                            && (i.Action == SwipeAction.Like || i.Action == SwipeAction.SuperLike)
                        )
                    )
                )
            );

    private async Task<GameNight> RequireAsync(Guid id, string sessionId, CancellationToken ct) =>
        await db.GameNights.FirstOrDefaultAsync(n => n.Id == id && n.SessionId == sessionId, ct)
        ?? throw new KeyNotFoundException("Spieleabend nicht gefunden.");

    public Task<GameNight?> FindAsync(Guid id, string sessionId, CancellationToken ct) =>
        db
            .GameNights.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.SessionId == sessionId, ct);

    public async Task<PagedResult<GameNightDto>> ListAsync(
        string sessionId,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        var query = db.GameNights.AsNoTracking().Where(n => n.SessionId == sessionId);
        return new(
            await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenBy(n => n.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new GameNightDto(n.Id, n.Name, n.Date, n.Players, n.IncludeUnowned))
                .ToListAsync(ct),
            await query.CountAsync(ct),
            page,
            pageSize
        );
    }

    public async Task<GameNight> CreateAsync(
        string sessionId,
        GameNightRequest request,
        CancellationToken ct
    )
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Sessions" ("Id", "CreatedAt") VALUES ({sessionId}, {DateTimeOffset.UtcNow}) ON CONFLICT ("Id") DO NOTHING
            """,
            ct
        );
        var night = new GameNight
        {
            SessionId = sessionId,
            Name = request.Name.Trim(),
            Date = request.Date,
            Players = request.Players,
            IncludeUnowned = request.IncludeUnowned,
        };
        db.GameNights.Add(night);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return night;
    }

    public async Task UpdateAsync(
        Guid id,
        string sessionId,
        GameNightRequest request,
        CancellationToken ct
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id, ct);
        var night = await RequireAsync(id, sessionId, ct);
        night.Name = request.Name.Trim();
        night.Date = request.Date;
        night.Players = request.Players;
        night.IncludeUnowned = request.IncludeUnowned;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<(List<BoardGameDto> Games, int Remaining)> CandidatesAsync(
        GameNight night,
        CancellationToken ct,
        int? maxRank = null
    )
    {
        // Global Likes/Dislikes/Skips are not night decisions; only this night's choices are excluded.
        var query = Available(night)
            .Where(g =>
                !db.GameNightChoices.Any(c => c.GameNightId == night.Id && c.BggId == g.BggId)
            );
        if (maxRank.HasValue)
            query = query.Where(g => g.BggRank > 0 && g.BggRank <= maxRank);
        var remaining = await query.CountAsync(ct);
        var games = await query
            .OrderBy(g => EF.Functions.Random())
            .Take(300)
            .Select(EfGameRepository.Summary)
            .ToListAsync(ct);
        var ids = games.Select(g => g.BggId).ToArray();
        var states = await db
            .Interactions.AsNoTracking()
            .Where(i => i.SessionId == night.SessionId && ids.Contains(i.BggId))
            .ToDictionaryAsync(i => i.BggId, ct);
        var dtos = games
            .Select(g =>
            {
                var dto = GameMapping.ToDto(
                    g,
                    states.TryGetValue(g.BggId, out var state) ? state.Action : null
                );
                dto.IsOwned = state?.IsOwned ?? false;
                return dto;
            })
            .ToList();
        return (dtos, remaining);
    }

    public async Task<PagedResult<GameNightSelection>> SelectionsAsync(
        GameNight night,
        int page,
        CancellationToken ct,
        int? maxRank = null
    )
    {
        const int size = 24;
        var query = db
            .GameNightChoices.AsNoTracking()
            .Where(c =>
                c.GameNightId == night.Id
                && (c.Action == SwipeAction.Like || c.Action == SwipeAction.SuperLike)
            );
        if (maxRank.HasValue)
            query = query.Where(c => c.Game.BggRank > 0 && c.Game.BggRank <= maxRank);
        var rows = await query
            .OrderByDescending(c => c.Action == SwipeAction.SuperLike)
            .ThenByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new { c.Action, c.BggId })
            .ToListAsync(ct);
        var ids = rows.Select(c => c.BggId).ToArray();
        var games = await db
            .Games.AsNoTracking()
            .Where(g => ids.Contains(g.BggId))
            .Select(EfGameRepository.Summary)
            .ToDictionaryAsync(g => g.BggId, ct);
        var states = await db
            .Interactions.AsNoTracking()
            .Where(i => i.SessionId == night.SessionId && ids.Contains(i.BggId))
            .ToDictionaryAsync(i => i.BggId, ct);
        var items = rows.Select(c =>
            {
                var dto = GameMapping.ToDto(
                    games[c.BggId],
                    states.TryGetValue(c.BggId, out var state) ? state.Action : null
                );
                dto.IsOwned = state?.IsOwned ?? false;
                return new GameNightSelection(dto, c.Action);
            })
            .ToList();
        return new(items, await query.CountAsync(ct), page, size);
    }

    public async Task SaveChoiceAsync(
        Guid id,
        string sessionId,
        int gameId,
        SwipeAction action,
        CancellationToken ct
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(id, ct);
        var night = await RequireAsync(id, sessionId, ct);
        var choice = await db.GameNightChoices.FindAsync([id, gameId], ct);
        if (choice?.Action == action)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (
            (choice == null || action is SwipeAction.Like or SwipeAction.SuperLike)
            && !await Available(night).AnyAsync(g => g.BggId == gameId, ct)
        )
            throw new ArgumentException(
                "Dieses Spiel gehört nicht zum verfügbaren Spieleabend-Pool."
            );
        if (choice == null)
        {
            choice = new() { GameNightId = id, BggId = gameId };
            db.GameNightChoices.Add(choice);
        }
        choice.Action = action;
        choice.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    private Task<int> LockAsync(Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 1))",
            ct
        );
}

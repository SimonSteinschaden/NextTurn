using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Data;
using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;

namespace NextTurn.Api.Repositories;

public class EfGameRepository(NextTurnDbContext db) : IGameRepository
{
    private static readonly System.Linq.Expressions.Expression<Func<Game, Game>> Summary =
        g => new Game
        {
            BggId = g.BggId,
            Name = g.Name,
            YearPublished = g.YearPublished,
            BggRank = g.BggRank,
            ImageUrl = g.ImageUrl,
            ThumbnailUrl = g.ThumbnailUrl,
            AverageRating = g.AverageRating,
            BayesAverage = g.BayesAverage,
            UsersRated = g.UsersRated,
            MinPlayers = g.MinPlayers,
            MaxPlayers = g.MaxPlayers,
            BestPlayerCounts = g.BestPlayerCounts,
            RecommendedPlayerCounts = g.RecommendedPlayerCounts,
            PlayingTime = g.PlayingTime,
            MinPlayTime = g.MinPlayTime,
            MaxPlayTime = g.MaxPlayTime,
            MinAge = g.MinAge,
            ComplexityWeight = g.ComplexityWeight,
            Categories = g.Categories,
            Mechanics = g.Mechanics,
            GameTypes = g.GameTypes,
            DetailsUpdatedAt = g.DetailsUpdatedAt,
        };
    private IQueryable<Game> Catalog =>
        db.Games.AsNoTracking().Where(g => g.BggRank > 0 && g.BggRank <= 10000);

    public Task<Game?> FindAsync(int id, CancellationToken ct) =>
        db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.BggId == id, ct);

    public async Task<PagedResult<BoardGameDto>> SearchAsync(
        CatalogQuery q,
        string? sessionId,
        bool favorites,
        CancellationToken ct
    )
    {
        var query = Catalog;
        if (favorites)
            query = db
                .Games.AsNoTracking()
                .Where(g =>
                    db.Interactions.Any(i =>
                        i.BggId == g.BggId
                        && i.SessionId == sessionId
                        && (i.Action == SwipeAction.Like || i.Action == SwipeAction.SuperLike)
                        && (!q.SuperOnly || i.Action == SwipeAction.SuperLike)
                    )
                );
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var escaped = q.Q.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            query = query.Where(g => EF.Functions.ILike(g.Name, "%" + escaped + "%", @"\"));
        }
        if (q.Players.HasValue)
            query = query.Where(g =>
                g.MinPlayers > 0
                && g.MaxPlayers > 0
                && g.MinPlayers <= q.Players
                && g.MaxPlayers >= q.Players
            );
        if (q.MaxMinutes.HasValue)
            query = query.Where(g =>
                (g.MaxPlayTime > 0 ? g.MaxPlayTime : g.PlayingTime) > 0
                && (g.MaxPlayTime > 0 ? g.MaxPlayTime : g.PlayingTime) <= q.MaxMinutes
            );
        if (q.MinComplexity.HasValue)
            query = query.Where(g => g.ComplexityWeight >= q.MinComplexity);
        if (q.MaxComplexity.HasValue)
            query = query.Where(g => g.ComplexityWeight <= q.MaxComplexity);
        if (!string.IsNullOrWhiteSpace(q.Category))
            query = query.Where(g => g.Categories.Contains(q.Category));
        if (!string.IsNullOrWhiteSpace(q.Mechanic))
            query = query.Where(g => g.Mechanics.Contains(q.Mechanic));
        var total = await query.CountAsync(ct);
        var ordered = q.Sort switch
        {
            "rating" => query.OrderByDescending(g => g.AverageRating ?? -1).ThenBy(g => g.BggId),
            "year" => query
                .OrderByDescending(g =>
                    g.YearPublished == 0 ? int.MinValue : g.YearPublished ?? int.MinValue
                )
                .ThenBy(g => g.BggId),
            "name" => query.OrderBy(g => g.Name).ThenBy(g => g.BggId),
            _ => query.OrderBy(g => g.BggRank ?? int.MaxValue).ThenBy(g => g.BggId),
        };
        var games = await ordered
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(Summary)
            .ToListAsync(ct);
        var ids = games.Select(g => g.BggId).ToArray();
        var states = string.IsNullOrEmpty(sessionId)
            ? new Dictionary<int, SwipeAction>()
            : await db
                .Interactions.Where(i => i.SessionId == sessionId && ids.Contains(i.BggId))
                .ToDictionaryAsync(i => i.BggId, i => i.Action, ct);
        return new(
            games
                .Select(g =>
                    GameMapping.ToDto(
                        g,
                        states.TryGetValue(g.BggId, out var action) ? action : null
                    )
                )
                .ToList(),
            total,
            q.Page,
            q.PageSize
        );
    }

    public async Task<Facets> FacetsAsync(CancellationToken ct) =>
        new(
            await Catalog.SelectMany(g => g.Categories).Distinct().OrderBy(x => x).ToArrayAsync(ct),
            await Catalog.SelectMany(g => g.Mechanics).Distinct().OrderBy(x => x).ToArrayAsync(ct)
        );

    public async Task<List<Game>> CandidatesAsync(
        string? sessionId,
        int? players,
        string[] categories,
        string[] mechanics,
        bool exploration,
        int limit,
        CancellationToken ct
    )
    {
        var query = Catalog.Where(g =>
            g.DetailsUpdatedAt != null
            && !db.Interactions.Any(i => i.SessionId == sessionId && i.BggId == g.BggId)
        );
        if (players.HasValue)
            query = query.Where(g =>
                g.MinPlayers > 0
                && g.MaxPlayers > 0
                && g.MinPlayers <= players
                && g.MaxPlayers >= players
            );
        if (!exploration && (categories.Length > 0 || mechanics.Length > 0))
            query = query.Where(g =>
                g.Categories.Any(c => categories.Contains(c))
                || g.Mechanics.Any(m => mechanics.Contains(m))
            );
        if (exploration)
            return await query
                .OrderBy(g => EF.Functions.Random())
                .Take(limit)
                .Select(Summary)
                .ToListAsync(ct);
        return await query
            .OrderBy(g => g.BggRank)
            .ThenBy(g => g.BggId)
            .Take(limit)
            .Select(Summary)
            .ToListAsync(ct);
    }

    public async Task SeedAsync(IReadOnlyList<BoardGameRankEntry> ranks, CancellationToken ct)
    {
        var existing = await db.Games.ToDictionaryAsync(g => g.BggId, ct);
        var selectedIds = ranks.Select(r => r.BggId).ToHashSet();
        foreach (var old in existing.Values.Where(g => !selectedIds.Contains(g.BggId)))
            old.BggRank = null;
        foreach (var r in ranks)
        {
            if (!existing.TryGetValue(r.BggId, out var game))
            {
                game = new() { BggId = r.BggId };
                db.Games.Add(game);
            }
            game.BggRank = r.BggRank;
            if (game.DetailsUpdatedAt == null)
            {
                game.Name = r.Name;
                game.YearPublished = r.YearPublished;
                game.AverageRating = r.AverageRating;
                game.BayesAverage = r.BayesAverage;
                game.UsersRated = r.UsersRated;
            }
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<List<int>> ImportIdsAsync(DateTimeOffset staleBefore, CancellationToken ct) =>
        Catalog
            .Where(g => g.DetailsUpdatedAt == null || g.DetailsUpdatedAt < staleBefore)
            .OrderBy(g => g.LastImportAttemptAt != null)
            .ThenBy(g => g.BggRank)
            .Select(g => g.BggId)
            .ToListAsync(ct);

    public async Task SaveDetailsAsync(
        IReadOnlyList<BoardGameDto> details,
        IReadOnlyList<int> requested,
        string? error,
        CancellationToken ct
    )
    {
        var games = await db.Games.Where(g => requested.Contains(g.BggId)).ToListAsync(ct);
        foreach (var game in games)
        {
            game.LastImportAttemptAt = DateTimeOffset.UtcNow;
            var dto = details.FirstOrDefault(d => d.Id == game.BggId);
            if (dto == null)
            {
                game.ImportError = error ?? "BGG returned no metadata for this ID.";
                continue;
            }
            game.Name = dto.Name;
            game.YearPublished = dto.YearPublished;
            game.Description = dto.Description;
            game.ImageUrl = dto.Image;
            game.ThumbnailUrl = dto.Thumbnail;
            game.AverageRating = dto.AverageRating;
            game.BayesAverage = dto.BayesAverage;
            game.UsersRated = dto.UsersRated;
            game.MinPlayers = dto.MinPlayers;
            game.MaxPlayers = dto.MaxPlayers;
            game.BestPlayerCounts = dto.BestPlayerCounts;
            game.RecommendedPlayerCounts = dto.RecommendedPlayerCounts;
            game.PlayingTime = dto.PlayingTime;
            game.MinPlayTime = dto.MinPlayTime;
            game.MaxPlayTime = dto.MaxPlayTime;
            game.MinAge = dto.MinAge;
            game.ComplexityWeight = dto.Complexity;
            game.Categories = dto.Categories.Distinct().ToArray();
            game.Mechanics = dto.Mechanics.Distinct().ToArray();
            game.GameTypes = dto.GameTypes;
            game.DetailsUpdatedAt = DateTimeOffset.UtcNow;
            game.ImportError = null;
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<CatalogStatus> StatusAsync(bool running, CancellationToken ct)
    {
        var total = await Catalog.CountAsync(ct);
        var detailed = await Catalog.CountAsync(g => g.DetailsUpdatedAt != null, ct);
        var failed = await Catalog.CountAsync(g => g.ImportError != null, ct);
        return new(total, detailed, failed, total - detailed, running);
    }
}

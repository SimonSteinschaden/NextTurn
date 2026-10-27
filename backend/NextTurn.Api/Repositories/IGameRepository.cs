using NextTurn.Api.Entities;
using NextTurn.Api.Models;

namespace NextTurn.Api.Repositories;

public interface IGameRepository
{
    Task<PagedResult<BoardGameDto>> SearchAsync(
        CatalogQuery query,
        string? sessionId,
        bool favorites,
        CancellationToken ct
    );
    Task<Game?> FindAsync(int id, CancellationToken ct);
    Task<Facets> FacetsAsync(CancellationToken ct);
    Task<List<Game>> CandidatesAsync(
        string? sessionId,
        int? players,
        string[] categories,
        string[] mechanics,
        bool exploration,
        int limit,
        CancellationToken ct,
        int topRankCutoff = 500,
        int? maxRank = null,
        string[]? preferredDesigners = null
    );
    Task SeedAsync(IReadOnlyList<BoardGameRankEntry> ranks, CancellationToken ct);
    Task<List<int>> ImportIdsAsync(DateTimeOffset staleBefore, CancellationToken ct);
    Task SaveDetailsAsync(
        IReadOnlyList<BoardGameDto> details,
        IReadOnlyList<int> requested,
        string? error,
        CancellationToken ct
    );
    Task<CatalogStatus> StatusAsync(bool running, CancellationToken ct);
}

public interface IInteractionRepository
{
    Task<List<GameInteraction>> CurrentAsync(string sessionId, CancellationToken ct);
    Task SaveAsync(string sessionId, int gameId, SwipeAction action, CancellationToken ct);
    Task SetOwnershipAsync(string sessionId, int gameId, bool owned, CancellationToken ct);
}

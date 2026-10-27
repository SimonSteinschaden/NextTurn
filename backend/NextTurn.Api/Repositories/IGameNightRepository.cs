using NextTurn.Api.Entities;
using NextTurn.Api.Models;

namespace NextTurn.Api.Repositories;

public interface IGameNightRepository
{
    Task<PagedResult<GameNightDto>> ListAsync(
        string sessionId,
        int page,
        int pageSize,
        CancellationToken ct
    );
    Task<GameNight?> FindAsync(Guid id, string sessionId, CancellationToken ct);
    Task<GameNight> CreateAsync(string sessionId, GameNightRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, string sessionId, GameNightRequest request, CancellationToken ct);
    Task<(List<BoardGameDto> Games, int Remaining)> CandidatesAsync(
        GameNight night,
        CancellationToken ct
    );
    Task<PagedResult<GameNightSelection>> SelectionsAsync(
        GameNight night,
        int page,
        CancellationToken ct
    );
    Task SaveChoiceAsync(
        Guid id,
        string sessionId,
        int gameId,
        SwipeAction action,
        CancellationToken ct
    );
}

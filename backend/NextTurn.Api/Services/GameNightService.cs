using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class GameNightService(IGameNightRepository nights, MatchQualityService matches)
{
    public static GameNightDto ToDto(GameNight n) =>
        new(n.Id, n.Name, n.Date, n.Players, n.IncludeUnowned);

    private static void Validate(string sessionId, GameNightRequest? request = null)
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            throw new ArgumentException("Ungültige Session.");
        if (
            request != null
            && (
                string.IsNullOrWhiteSpace(request.Name)
                || request.Name.Length > 120
                || request.Players is < 1 or > 100
            )
        )
            throw new ArgumentException("Name und Spieleranzahl prüfen.");
    }

    private static void ValidateRank(int? maxRank)
    {
        if (maxRank is < 1 or > 10000)
            throw new ArgumentException("BGG-Rang muss zwischen 1 und 10.000 liegen.");
    }

    public Task<PagedResult<GameNightDto>> ListAsync(
        string sessionId,
        int page,
        CancellationToken ct
    )
    {
        Validate(sessionId);
        return nights.ListAsync(sessionId, Math.Clamp(page, 1, 10000), 20, ct);
    }

    public async Task<GameNightDto> CreateAsync(
        string sessionId,
        GameNightRequest request,
        CancellationToken ct
    )
    {
        Validate(sessionId, request);
        return ToDto(await nights.CreateAsync(sessionId, request, ct));
    }

    public async Task<GameNightBoard> BoardAsync(
        Guid id,
        string sessionId,
        int page,
        CancellationToken ct,
        int? maxRank = null
    )
    {
        Validate(sessionId);
        ValidateRank(maxRank);
        var night =
            await nights.FindAsync(id, sessionId, ct)
            ?? throw new KeyNotFoundException("Spieleabend nicht gefunden.");
        var candidates = await nights.CandidatesAsync(night, ct, maxRank);
        var selected = await nights.SelectionsAsync(night, Math.Clamp(page, 1, 10000), ct, maxRank);
        var context = await matches.LoadAsync(sessionId, ct);
        MatchQualityService.Apply(candidates.Games, context);
        MatchQualityService.Apply(selected.Items.Select(s => s.Game).ToList(), context);
        var queue = candidates
            .Games.OrderByDescending(g => g.MatchQuality?.Percentage ?? -1)
            .ThenBy(g => g.BggRank ?? int.MaxValue)
            .Take(20)
            .ToList();
        return new(ToDto(night), queue, selected, candidates.Remaining);
    }

    public async Task<GameNightBoard> UpdateAsync(
        Guid id,
        string sessionId,
        GameNightRequest request,
        CancellationToken ct,
        int? maxRank = null
    )
    {
        Validate(sessionId, request);
        ValidateRank(maxRank);
        await nights.UpdateAsync(id, sessionId, request, ct);
        return await BoardAsync(id, sessionId, 1, ct, maxRank);
    }

    public async Task<GameNightBoard> ChooseAsync(
        Guid id,
        string sessionId,
        int gameId,
        SwipeAction action,
        CancellationToken ct,
        int? maxRank = null
    )
    {
        Validate(sessionId);
        ValidateRank(maxRank);
        if (gameId < 1 || !Enum.IsDefined(action))
            throw new ArgumentException("Ungültige Auswahl.");
        await nights.SaveChoiceAsync(id, sessionId, gameId, action, ct);
        return await BoardAsync(id, sessionId, 1, ct, maxRank);
    }
}

using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class OwnershipService(IGameRepository games, IInteractionRepository interactions)
{
    public async Task SetAsync(string sessionId, int id, bool owned, CancellationToken ct)
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            throw new ArgumentException("Ungültige Session.");
        if (await games.FindAsync(id, ct) == null)
            throw new KeyNotFoundException("Spiel nicht im Katalog.");
        await interactions.SetOwnershipAsync(sessionId, id, owned, ct);
    }
}

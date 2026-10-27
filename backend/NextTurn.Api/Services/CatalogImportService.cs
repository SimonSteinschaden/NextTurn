using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class ImportState
{
    public volatile bool Running;
    public string? LastError { get; set; }
}

public class CatalogImportService(
    IGameRepository games,
    BggService bgg,
    IConfiguration config,
    ImportState state,
    ILogger<CatalogImportService> logger
)
{
    public async Task RunAsync(CancellationToken ct)
    {
        state.Running = true;
        try
        {
            var ids = await games.ImportIdsAsync(
                DateTimeOffset.UtcNow.AddDays(
                    -Math.Max(1, config.GetValue("Import:RefreshDays", 30))
                ),
                ct
            );
            logger.LogInformation(
                "Catalog details: {Count} games to enrich or refresh.",
                ids.Count
            );
            foreach (var batch in ids.Chunk(20))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var details = await bgg.GetBoardGamesAsync(batch, ct);
                    await games.SaveDetailsAsync(details, batch, null, ct);
                    logger.LogInformation(
                        "Imported {Count}/{BatchSize} details; rank source remains CSV.",
                        details.Count,
                        batch.Length
                    );
                    state.LastError = null;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Do not log request headers, tokens or upstream response bodies.
                    state.LastError = ex is BggAuthorizationException or InvalidOperationException
                        ? ex.Message
                        : "BGG-Import fehlgeschlagen; erneuter Versuch beim nächsten Importlauf.";
                    logger.LogWarning(
                        "Catalog batch failed ({ErrorType}); {Count} IDs marked for retry.",
                        ex.GetType().Name,
                        batch.Length
                    );
                    await games.SaveDetailsAsync([], batch, state.LastError, ct);
                    if (ex is BggAuthorizationException or InvalidOperationException)
                        break;
                }
            }
        }
        finally
        {
            state.Running = false;
        }
    }
}

public class CatalogImportWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<CatalogImportWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Import:Enabled", true))
            return;
        try
        {
            using var scope = scopes.CreateScope();
            await scope
                .ServiceProvider.GetRequiredService<CatalogImportService>()
                .RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(
                "Catalog worker stopped ({ErrorType}); persisted progress will resume on restart.",
                ex.GetType().Name
            );
        }
    }
}

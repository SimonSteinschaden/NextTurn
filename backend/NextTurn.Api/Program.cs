using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Data;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddPolicy(
        "Frontend",
        policy =>
            policy
                .WithOrigins(
                    builder.Configuration.GetSection("Frontend:Origins").Get<string[]>()
                        ?? ["http://localhost:5173", "http://localhost:4173"]
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
    )
);
builder.Services.AddMemoryCache();
builder
    .Services.AddHttpClient("bgg", client => client.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var connectionString = builder.Configuration.GetConnectionString("NextTurn");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:NextTurn fehlt. Datenbankzugang lokal über User Secrets oder Umgebungsvariable konfigurieren."
    );
builder.Services.AddDbContext<NextTurnDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<BggService>();
builder.Services.AddSingleton<ImportState>();
builder.Services.AddScoped<IGameRepository, EfGameRepository>();
builder.Services.AddScoped<IInteractionRepository, EfInteractionRepository>();
builder.Services.AddScoped<CsvGameRepository>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<UserPreferenceService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<MatchQualityService>();
builder.Services.AddScoped<OwnershipService>();
builder.Services.AddScoped<IGameNightRepository, EfGameNightRepository>();
builder.Services.AddScoped<GameNightService>();
builder.Services.AddScoped<CatalogImportService>();
builder.Services.AddHostedService<CatalogImportWorker>();
var app = builder.Build();
app.UseExceptionHandler(handler =>
    handler.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(
            new { message = "Die Anfrage konnte nicht verarbeitet werden. Bitte erneut versuchen." }
        );
    })
);
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");
app.MapControllers();
if (builder.Configuration.GetValue("Database:BootstrapOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NextTurnDbContext>().Database.MigrateAsync();
    var ranks = scope.ServiceProvider.GetRequiredService<CsvGameRepository>().LoadRankedGames();
    await scope
        .ServiceProvider.GetRequiredService<IGameRepository>()
        .SeedAsync(ranks, CancellationToken.None);
}
app.Run();

public partial class Program;

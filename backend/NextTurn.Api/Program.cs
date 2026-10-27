using NextTurn.Api.Services;
using NextTurn.Api.Repositories;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>{
    options.AddPolicy("Frontend", policy =>{
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<BggService>();
builder.Services.AddScoped<CsvGameRepository>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddSingleton<UserPreferenceService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();


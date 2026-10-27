namespace NextTurn.Api.Entities;

public class GameNight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = "";
    public string Name { get; set; } = "Spieleabend";
    public DateOnly? Date { get; set; }
    public int Players { get; set; } = 3;
    public bool IncludeUnowned { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class GameNightChoice
{
    public Guid GameNightId { get; set; }
    public int BggId { get; set; }
    public SwipeAction Action { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Game Game { get; set; } = null!;
}

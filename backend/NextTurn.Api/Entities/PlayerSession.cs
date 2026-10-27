namespace NextTurn.Api.Entities;

public class PlayerSession
{
    public string Id { get; set; } = "";
    public string? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class GameInteraction
{
    public string SessionId { get; set; } = "";
    public int BggId { get; set; }
    public SwipeAction Action { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Game Game { get; set; } = null!;
}

public class InteractionEvent
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    public int BggId { get; set; }
    public SwipeAction Action { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

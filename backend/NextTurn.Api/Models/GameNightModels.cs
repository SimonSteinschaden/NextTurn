using System.ComponentModel.DataAnnotations;

namespace NextTurn.Api.Models;

public class GameNightRequest
{
    [Required, StringLength(120)]
    public string Name { get; set; } = "Spieleabend";
    public DateOnly? Date { get; set; }

    [Range(1, 100)]
    public int Players { get; set; } = 3;
    public bool IncludeUnowned { get; set; }
}

public class GameNightChoiceRequest
{
    [Required, EnumDataType(typeof(SwipeAction))]
    public SwipeAction? Action { get; set; }
}

public record GameNightDto(Guid Id, string Name, DateOnly? Date, int Players, bool IncludeUnowned);

public record GameNightSelection(BoardGameDto Game, SwipeAction Action);

public record GameNightBoard(
    GameNightDto Plan,
    IReadOnlyList<BoardGameDto> Candidates,
    PagedResult<GameNightSelection> Selections,
    int RemainingCount
);

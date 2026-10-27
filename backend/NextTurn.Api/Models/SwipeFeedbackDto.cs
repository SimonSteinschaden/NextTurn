using System.ComponentModel.DataAnnotations;

namespace NextTurn.Api.Models;

public class SwipeFeedbackDto
{
    [Required, StringLength(100)]
    public string SessionId { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int GameId { get; set; }

    [EnumDataType(typeof(SwipeAction))]
    public SwipeAction Action { get; set; }
}

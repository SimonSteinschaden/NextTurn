namespace NextTurn.Api.Models
{
    public class SwipeFeedbackDto
    {
        public string SessionId { get; set; } = "";

        public int GameId { get; set; }

        public SwipeAction Action { get; set; }
    }
}
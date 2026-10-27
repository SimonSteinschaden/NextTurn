namespace NextTurn.Api.Models
{
    public class BoardGameDto
    {
        public int Id { get; set; }
        public int? BggRank { get; set; }

        public string Name { get; set; } = "";

        public string Thumbnail { get; set; } = "";
        public string Image { get; set; } = "";

        public int? YearPublished { get; set; }

        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public List<PlayerCountPollDto> PlayerCountPoll { get; set; } = new();

        public int? PlayingTime { get; set; }

        public double? AverageRating { get; set; }
        public double? Complexity { get; set; }
        
        public double RecommendationScore { get; set; }

        public List<string> Categories { get; set; } = new();
        public List<string> Mechanics { get; set; } = new();

    }

    public class PlayerCountPollDto
    {
        public string PlayerCount { get; set; } = "";

        public int Best { get; set; }

        public int Recommended { get; set; }

        public int NotRecommended { get; set; }

        public bool IsRecommended => Best + Recommended > NotRecommended;
    }

}
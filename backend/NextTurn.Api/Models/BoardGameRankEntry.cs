namespace NextTurn.Api.Models
{
    public class BoardGameRankEntry
    {
        public int BggId { get; set; }

        public int? BggRank { get; set; }

        public string Name { get; set; } = "";
    }
}
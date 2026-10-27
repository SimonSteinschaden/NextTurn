namespace NextTurn.Api.Models;

public class BoardGameRankEntry
{
    public int BggId { get; set; }
    public int? BggRank { get; set; }
    public string Name { get; set; } = "";
    public int? YearPublished { get; set; }
    public double? AverageRating { get; set; }
    public double? BayesAverage { get; set; }
    public int? UsersRated { get; set; }
}

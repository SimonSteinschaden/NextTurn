namespace NextTurn.Api.Models;

public class UserPreferenceProfile
{
    public string SessionId { get; set; } = "";
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public Dictionary<string, double> MechanicScores { get; set; } = new();
    public double PreferredComplexity { get; set; }
    public double? PreferredPlayTime { get; set; }
    public double? PreferredPlayerCount { get; set; }
    public int TotalSwipes { get; set; }
    public int[] RatedGameIds { get; set; } = [];
}

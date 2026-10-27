using NextTurn.Api.Entities;
using NextTurn.Api.Models;

namespace NextTurn.Api.Services;

public static class GameMapping
{
    public static BoardGameDto ToDto(Game g, SwipeAction? action = null) =>
        new()
        {
            Id = g.BggId,
            Name = g.Name,
            BggRank = g.BggRank,
            YearPublished = g.YearPublished == 0 ? null : g.YearPublished,
            Description = g.Description,
            Image = g.ImageUrl ?? "",
            Thumbnail = g.ThumbnailUrl ?? "",
            AverageRating = g.AverageRating,
            BayesAverage = g.BayesAverage,
            UsersRated = g.UsersRated,
            MinPlayers = g.MinPlayers > 0 ? g.MinPlayers : null,
            MaxPlayers = g.MaxPlayers > 0 ? g.MaxPlayers : null,
            BestPlayerCounts = g.BestPlayerCounts,
            RecommendedPlayerCounts = g.RecommendedPlayerCounts,
            PlayingTime = g.PlayingTime > 0 ? g.PlayingTime : null,
            MinPlayTime = g.MinPlayTime > 0 ? g.MinPlayTime : null,
            MaxPlayTime = g.MaxPlayTime > 0 ? g.MaxPlayTime : null,
            MinAge = g.MinAge > 0 ? g.MinAge : null,
            Complexity = g.ComplexityWeight,
            Categories = g.Categories.ToList(),
            Mechanics = g.Mechanics.ToList(),
            GameTypes = g.GameTypes,
            DetailsAvailable = g.DetailsUpdatedAt.HasValue,
            UserAction = action,
        };
}

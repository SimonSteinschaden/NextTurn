using System.Net.Http.Headers;
using System.Globalization;
using System.Xml.Linq;
using NextTurn.Api.Models;
using Microsoft.Extensions.Caching.Memory;


namespace NextTurn.Api.Services
{
    public class BggService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public BggService(IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = new HttpClient();
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<BoardGameDto?> GetBoardGameAsync(int id)
        {
            var cacheKey = $"bgg-game-{id}";

            if (_cache.TryGetValue(cacheKey, out BoardGameDto? cachedGame))
            {
                return cachedGame;
            }

            var baseUrl =    _configuration["BggApi:BaseUrl"] ?? "https://boardgamegeek.com/xmlapi2";
            var userAgent =  _configuration["BggApi:UserAgent"];
            var token =      _configuration["BggApi:BearerToken"];
            
            if(string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("BGG API token or BaseUrl is missing.");
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? "NextTurn/1.0");

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/xml");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"{baseUrl}/thing?id={id}&stats=1";
            var response = await _httpClient.GetAsync(url);

            if(!response.IsSuccessStatusCode)
            {
             var errorBody = await response.Content.ReadAsStringAsync();
             throw new Exception($"BGG API request failed with status code {response.StatusCode}. Response: {errorBody}");
            }
            
            var xmlText = await response.Content.ReadAsStringAsync();

            var game = ParseBoardGameXml(xmlText);

            if (game != null)
            {
                _cache.Set(
                    cacheKey,
                    game,
                    TimeSpan.FromHours(24)
                );
            }

            return game;        
        }

        private BoardGameDto? ParseBoardGameXml(string xmlText){
            var xml=XDocument.Parse(xmlText);
            var item = xml.Descendants("item").FirstOrDefault();
            if(item == null) return null;
            var game = new BoardGameDto();
            game.Id = int.Parse(item.Attribute("id")?.Value ?? "0");
            game.Name = item.Elements("name").FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value ?? "";
            game.Thumbnail = item.Element("thumbnail")?.Value ?? "";
            game.Image = item.Element("image")?.Value ?? "";
            game.YearPublished = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out var year) ? year : null;
            game.MinPlayers = int.TryParse(item.Element("minplayers")?.Attribute("value")?.Value, out var minPlayers) ? minPlayers : null;
            game.MaxPlayers = int.TryParse(item.Element("maxplayers")?.Attribute("value")?.Value, out var maxPlayers) ? maxPlayers : null;
            game.PlayingTime = int.TryParse(item.Element("playingtime")?.Attribute("value")?.Value, out var playingTime) ? playingTime : null;
            game.AverageRating = double.TryParse(item.Element("statistics")?.Element("ratings")?.Element("average")?.Attribute("value")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var avgRating) ? avgRating : null;
            game.Complexity = double.TryParse(item.Element("statistics")?.Element("ratings")?.Element("averageweight")?.Attribute("value")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var complexity) ? complexity : null;
            
            game.Categories = item.Elements("link")
                .Where(x => x.Attribute("type")?.Value == "boardgamecategory")
                .Select(x => x.Attribute("value")?.Value ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            game.Mechanics = item.Elements("link")
                .Where(x => x.Attribute("type")?.Value == "boardgamemechanic")
                .Select(x => x.Attribute("value")?.Value ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            game.PlayerCountPoll = item.Elements("poll")
            .Where(p => p.Attribute("name")?.Value == "suggested_numplayers")
            .Elements("results")
            .Select(results =>
            {
                var playerCount =
                    results.Attribute("numplayers")?.Value ?? "";

                var resultValues = results.Elements("result")
                    .ToDictionary(
                        r => r.Attribute("value")?.Value ?? "",
                        r => int.TryParse(r.Attribute("numvotes")?.Value, out var votes)
                            ? votes
                            : 0
                    );

                return new PlayerCountPollDto
                {
                    PlayerCount = playerCount,
                    Best = resultValues.GetValueOrDefault("Best"),
                    Recommended = resultValues.GetValueOrDefault("Recommended"),
                    NotRecommended = resultValues.GetValueOrDefault("Not Recommended")
                };
            })
            .ToList();
            return game;
        }

        private int? ParseNullableInt(string? value)
        {
            if (int.TryParse(value, out var result))
            {
                return result;
            }
            return null;
        }

        private double? ParseNullableDouble(string? value)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return null;
        }
    }
}
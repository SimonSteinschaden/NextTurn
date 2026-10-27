using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using NextTurn.Api.Models;

namespace NextTurn.Api.Services;

public class BggService(
    IConfiguration configuration,
    IMemoryCache cache,
    IHttpClientFactory clients
)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequest = DateTimeOffset.MinValue;

    public async Task<BoardGameDto?> GetBoardGameAsync(int id, CancellationToken ct = default)
    {
        if (cache.TryGetValue("bgg-game-" + id, out BoardGameDto? cached))
            return cached;
        return (await GetBoardGamesAsync([id], ct)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<BoardGameDto>> GetBoardGamesAsync(
        IReadOnlyList<int> ids,
        CancellationToken ct
    )
    {
        if (ids.Count is < 1 or > 20)
            throw new ArgumentOutOfRangeException(
                nameof(ids),
                "BGG supports 1–20 IDs per request."
            );
        var token = configuration["BggApi:BearerToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "BGG-Token fehlt in der lokalen Backend-Konfiguration."
            );
        var baseUri = new Uri(
            configuration["BggApi:BaseUrl"] ?? "https://boardgamegeek.com/xmlapi2"
        );
        if (baseUri.Scheme != "https" || baseUri.Host != "boardgamegeek.com")
            throw new InvalidOperationException(
                "Der BGG-Token darf nur an https://boardgamegeek.com gesendet werden."
            );
        await gate.WaitAsync(ct);
        try
        {
            var client = clients.CreateClient("bgg");
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var delay = nextRequest - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct);
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    baseUri.ToString().TrimEnd('/')
                        + "/thing?id="
                        + string.Join(",", ids)
                        + "&stats=1"
                );
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.UserAgent.ParseAdd(
                    configuration["BggApi:UserAgent"] ?? "NextTurn/1.0"
                );
                request.Headers.Accept.ParseAdd("application/xml");
                nextRequest = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(5, configuration.GetValue("BggApi:RequestIntervalSeconds", 5))
                );
                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, ct);
                }
                catch (HttpRequestException) when (attempt < 4)
                {
                    nextRequest = DateTimeOffset.UtcNow.AddSeconds(5 * Math.Pow(2, attempt));
                    continue;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < 4)
                {
                    nextRequest = DateTimeOffset.UtcNow.AddSeconds(5 * Math.Pow(2, attempt));
                    continue;
                }
                using (response)
                {
                    if (
                        response.StatusCode
                        is HttpStatusCode.Unauthorized
                            or HttpStatusCode.Forbidden
                    )
                        throw new BggAuthorizationException(
                            "BGG hat den Zugriff abgelehnt (HTTP "
                                + (int)response.StatusCode
                                + "). Token/Lizenz lokal prüfen."
                        );
                    var retryable =
                        response.StatusCode
                            is HttpStatusCode.Accepted
                                or HttpStatusCode.TooManyRequests
                        || (int)response.StatusCode >= 500;
                    if (retryable && attempt < 4)
                    {
                        var retry =
                            response.Headers.RetryAfter?.Delta
                            ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                            ?? TimeSpan.FromSeconds(5 * Math.Pow(2, attempt));
                        nextRequest = DateTimeOffset.UtcNow.Add(
                            retry > TimeSpan.FromSeconds(5) ? retry : TimeSpan.FromSeconds(5)
                        );
                        continue;
                    }
                    if (
                        !response.IsSuccessStatusCode
                        || response.StatusCode == HttpStatusCode.Accepted
                    )
                        throw new HttpRequestException(
                            "BGG-Abfrage fehlgeschlagen (HTTP " + (int)response.StatusCode + ").",
                            null,
                            response.StatusCode
                        );
                    List<BoardGameDto> games;
                    try
                    {
                        games = ParseBoardGamesXml(await response.Content.ReadAsStringAsync(ct));
                    }
                    catch (System.Xml.XmlException) when (attempt < 4)
                    {
                        nextRequest = DateTimeOffset.UtcNow.AddSeconds(5 * Math.Pow(2, attempt));
                        continue;
                    }
                    foreach (var game in games)
                        cache.Set("bgg-game-" + game.Id, game, TimeSpan.FromHours(24));
                    return games;
                }
            }
            throw new HttpRequestException("BGG hat nach mehreren Versuchen nicht geantwortet.");
        }
        finally
        {
            gate.Release();
        }
    }

    public static List<BoardGameDto> ParseBoardGamesXml(string text)
    {
        var xml = XDocument.Parse(text);
        if (xml.Root?.Name.LocalName != "items")
            throw new System.Xml.XmlException("Unerwartetes Format der BGG-Metadaten.");
        return xml.Descendants("item")
            .Select(item =>
            {
                var ratings = item.Element("statistics")?.Element("ratings");
                var polls = item.Elements("poll")
                    .Where(p => (string?)p.Attribute("name") == "suggested_numplayers")
                    .Elements("results")
                    .Select(r => new PlayerCountPollDto
                    {
                        PlayerCount = (string?)r.Attribute("numplayers") ?? "",
                        Best = Votes(r, "Best"),
                        Recommended = Votes(r, "Recommended"),
                        NotRecommended = Votes(r, "Not Recommended"),
                    })
                    .ToList();
                return new BoardGameDto
                {
                    Id = Int((string?)item.Attribute("id")) ?? 0,
                    Name =
                        item.Elements("name")
                            .FirstOrDefault(n => (string?)n.Attribute("type") == "primary")
                            ?.Attribute("value")
                            ?.Value
                        ?? "",
                    Description = item.Element("description")?.Value,
                    Image = item.Element("image")?.Value ?? "",
                    Thumbnail = item.Element("thumbnail")?.Value ?? "",
                    YearPublished = KnownYear(item),
                    MinPlayers = PositiveInt(item, "minplayers"),
                    MaxPlayers = PositiveInt(item, "maxplayers"),
                    PlayingTime = PositiveInt(item, "playingtime"),
                    MinPlayTime = PositiveInt(item, "minplaytime"),
                    MaxPlayTime = PositiveInt(item, "maxplaytime"),
                    MinAge = PositiveInt(item, "minage"),
                    AverageRating = ValueDouble(ratings, "average"),
                    BayesAverage = ValueDouble(ratings, "bayesaverage"),
                    UsersRated = ValueInt(ratings, "usersrated"),
                    Complexity = ValueDouble(ratings, "averageweight"),
                    BggRank = Int(
                        ratings
                            ?.Element("ranks")
                            ?.Elements("rank")
                            .FirstOrDefault(r => (string?)r.Attribute("name") == "boardgame")
                            ?.Attribute("value")
                            ?.Value
                    ),
                    Designers = DesignerPreference
                        .Names(Links(item, "boardgamedesigner"))
                        .ToArray(),
                    Categories = Links(item, "boardgamecategory"),
                    Mechanics = Links(item, "boardgamemechanic"),
                    GameTypes = item.Attribute("type") is { } type ? [type.Value] : [],
                    PlayerCountPoll = polls,
                    BestPlayerCounts = polls
                        .Where(p =>
                            p.Best > 0 && p.Best >= p.Recommended && p.Best > p.NotRecommended
                        )
                        .Select(p => Int(p.PlayerCount))
                        .OfType<int>()
                        .Distinct()
                        .ToArray(),
                    RecommendedPlayerCounts = polls
                        .Where(p => p.Best + p.Recommended > p.NotRecommended)
                        .Select(p => Int(p.PlayerCount))
                        .OfType<int>()
                        .Distinct()
                        .ToArray(),
                    DetailsAvailable = true,
                };
            })
            .Where(g => g.Id > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .ToList();
    }

    private static int? PositiveInt(XElement? element, string name)
    {
        var value = ValueInt(element, name);
        return value > 0 ? value : null;
    }

    private static int? KnownYear(XElement? element)
    {
        var value = ValueInt(element, "yearpublished");
        return value == 0 ? null : value;
    }

    private static int Votes(XElement r, string name) =>
        Int(
            r.Elements("result")
                .FirstOrDefault(x => (string?)x.Attribute("value") == name)
                ?.Attribute("numvotes")
                ?.Value
        ) ?? 0;

    private static int? Int(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static int? ValueInt(XElement? e, string name) =>
        Int(e?.Element(name)?.Attribute("value")?.Value);

    private static double? ValueDouble(XElement? e, string name) =>
        double.TryParse(
            e?.Element(name)?.Attribute("value")?.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var n
        )
        && n > 0
            ? n
            : null;

    private static List<string> Links(XElement e, string type) =>
        e.Elements("link")
            .Where(l => (string?)l.Attribute("type") == type)
            .Select(l => (string?)l.Attribute("value"))
            .OfType<string>()
            .Distinct()
            .ToList();
}

public class BggAuthorizationException(string message) : Exception(message);

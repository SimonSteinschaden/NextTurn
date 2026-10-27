using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class BggClientTests
{
    private class Responses(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? RequestHost { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            RequestHost = request.RequestUri?.Host;
            var status = statuses[Math.Min(Calls++, statuses.Length - 1)];
            return Task.FromResult(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent(
                        status == HttpStatusCode.OK
                            ? "<items><item type=\"boardgame\" id=\"42\"><name type=\"primary\" value=\"Test\"/></item></items>"
                            : "Upstream body must not be forwarded."
                    ),
                }
            );
        }
    }

    private class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private static BggService Client(Responses responses, string? host = null) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["BggApi:BearerToken"] = "unit-test-placeholder-not-a-real-token",
                        ["BggApi:BaseUrl"] = host ?? "https://boardgamegeek.com/xmlapi2",
                    }
                )
                .Build(),
            new MemoryCache(new MemoryCacheOptions()),
            new Factory(responses)
        );

    [Fact]
    public async Task RefusesForeignHostsWithoutSendingCredentials()
    {
        var responses = new Responses(HttpStatusCode.OK);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Client(responses, "https://example.com/xmlapi2").GetBoardGamesAsync([42], default)
        );
        Assert.Equal(0, responses.Calls);
    }

    [Fact]
    public async Task AuthorizationFailureDoesNotForwardResponseBodyOrToken()
    {
        var responses = new Responses(HttpStatusCode.Unauthorized);
        var error = await Assert.ThrowsAsync<BggAuthorizationException>(() =>
            Client(responses).GetBoardGamesAsync([42], default)
        );
        Assert.DoesNotContain("unit-test-placeholder", error.Message);
        Assert.DoesNotContain("Upstream body", error.Message);
        Assert.Equal("Bearer", responses.AuthorizationScheme);
        Assert.Equal("boardgamegeek.com", responses.RequestHost);
        Assert.Equal(1, responses.Calls);
    }

    [Fact]
    public async Task QueuedResponseIsRetriedAndReturnsParsedGames()
    {
        var responses = new Responses(HttpStatusCode.Accepted, HttpStatusCode.OK);
        var games = await Client(responses).GetBoardGamesAsync([42], default);
        Assert.Equal(2, responses.Calls);
        Assert.Equal(42, Assert.Single(games).Id);
    }
}

using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class BggDesignerRetryTests
{
    private sealed class Responses : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            Calls++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        Calls == 1
                            ? "<html>Temporary upstream response</html>"
                            : "<items><item type='boardgame' id='42'><name type='primary' value='Fixture'/><link type='boardgamedesigner' value='Ada'/></item></items>"
                    ),
                }
            );
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    [Fact]
    public async Task UnexpectedMetadataIsRetriedAndOnlySuccessfulDesignerDataIsCached()
    {
        var responses = new Responses();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BggApi:BearerToken"] = "unit-test-placeholder-not-a-real-token",
                }
            )
            .Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new BggService(config, cache, new Factory(responses));
        var games = await service.GetBoardGamesAsync([42], default);
        Assert.Equal(["Ada"], Assert.Single(games).Designers);
        Assert.Equal(2, responses.Calls);
        Assert.Equal(["Ada"], (await service.GetBoardGameAsync(42))!.Designers);
        Assert.Equal(2, responses.Calls);
    }
}

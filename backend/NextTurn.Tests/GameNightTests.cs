using Microsoft.EntityFrameworkCore;
using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

[Trait("Category", "Integration")]
public class GameNightTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Session() => "night-test-" + Guid.NewGuid().ToString("N");

    private async Task SeedAsync(params int[] ids)
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        await games.SeedAsync(
            ids.Select(
                    (id, index) =>
                        new BoardGameRankEntry
                        {
                            BggId = id,
                            BggRank = index + 1,
                            Name = "Night " + id,
                        }
                )
                .ToArray(),
            default
        );
        await games.SaveDetailsAsync(
            ids.Select(id => new BoardGameDto
                {
                    Id = id,
                    Name = "Night " + id,
                    MinPlayers = 2,
                    MaxPlayers = 4,
                    Categories = ["Fantasy"],
                    Mechanics = ["Deck Building"],
                    Complexity = 3,
                    PlayingTime = 60,
                })
                .ToArray(),
            ids,
            null,
            default
        );
    }

    private static GameNightService Service(NextTurn.Api.Data.NextTurnDbContext db) =>
        new(
            new EfGameNightRepository(db),
            new MatchQualityService(new EfInteractionRepository(db))
        );

    [Fact]
    public async Task OwnershipAloneIsDurableButDoesNotRateLearnOrExcludeDiscovery()
    {
        await SeedAsync(81001);
        var session = Session();
        await using (var write = fixture.CreateDb())
        {
            await new OwnershipService(
                new EfGameRepository(write),
                new EfInteractionRepository(write)
            ).SetAsync(session, 81001, true, default);
        }
        await using var db = fixture.CreateDb();
        var interactions = new EfInteractionRepository(db);
        var current = await interactions.CurrentAsync(session, default);
        var row = Assert.Single(current);
        Assert.True(row.IsOwned);
        Assert.Null(row.Action);
        Assert.Equal(0, await db.InteractionEvents.CountAsync(i => i.SessionId == session));
        var profile = UserPreferenceService.BuildProfile(session, current);
        Assert.Empty(profile.RatedGameIds);
        Assert.Empty(profile.CategoryScores);
        Assert.Equal(0, profile.TotalSwipes);
        Assert.Equal(0, StatisticsService.Calculate(current).Favorites);
        var games = new EfGameRepository(db);
        Assert.Contains(
            await games.CandidatesAsync(session, 3, [], [], false, 300, default),
            g => g.BggId == 81001
        );
        var owned = await games.SearchAsync(new() { OwnedOnly = true }, session, false, default);
        await new MatchQualityService(interactions).ApplyAsync(owned.Items, session, default);
        Assert.True(Assert.Single(owned.Items).IsOwned);
        Assert.Null(owned.Items[0].UserAction);
        Assert.Empty((await games.SearchAsync(new(), session, true, default)).Items);
        Assert.Empty(
            (await games.SearchAsync(new() { OwnedOnly = true }, Session(), false, default)).Items
        );
        await interactions.SetOwnershipAsync(session, 81001, false, default);
        Assert.Null((await interactions.CurrentAsync(session, default)).Single().Action);
    }

    [Fact]
    public async Task ConcurrentOwnershipAndVotePreserveBothAndRemovalIsIndependent()
    {
        await SeedAsync(82001);
        var session = Session();
        await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var db = fixture.CreateDb();
                await new EfInteractionRepository(db).SetOwnershipAsync(
                    session,
                    82001,
                    true,
                    default
                );
            }),
            Task.Run(async () =>
            {
                await using var db = fixture.CreateDb();
                await new EfInteractionRepository(db).SaveAsync(
                    session,
                    82001,
                    SwipeAction.SuperLike,
                    default
                );
            })
        );
        await using var read = fixture.CreateDb();
        var repo = new EfInteractionRepository(read);
        var row = Assert.Single(await repo.CurrentAsync(session, default));
        Assert.True(row.IsOwned);
        Assert.Equal(SwipeAction.SuperLike, row.Action);
        Assert.Equal(1, await read.InteractionEvents.CountAsync(i => i.SessionId == session));
        await repo.SaveAsync(session, 82001, SwipeAction.Skip, default);
        Assert.True((await repo.CurrentAsync(session, default)).Single().IsOwned);
        await repo.SaveAsync(session, 82001, SwipeAction.Like, default);
        await repo.SetOwnershipAsync(session, 82001, false, default);
        row = Assert.Single(await repo.CurrentAsync(session, default));
        Assert.False(row.IsOwned);
        Assert.Equal(SwipeAction.Like, row.Action);
    }

    [Fact]
    public async Task NightDefaultsToOwnedAndOptionAddsOnlyLikedUnownedWithoutChangingTaste()
    {
        await SeedAsync(83001, 83002, 83003, 83004, 83005, 83006);
        var session = Session();
        await using var db = fixture.CreateDb();
        var interactions = new EfInteractionRepository(db);
        foreach (var id in new[] { 83001, 83002, 83005, 83006 })
            await interactions.SetOwnershipAsync(session, id, true, default);
        await interactions.SaveAsync(session, 83001, SwipeAction.Like, default);
        await interactions.SaveAsync(session, 83002, SwipeAction.Dislike, default);
        await interactions.SaveAsync(session, 83003, SwipeAction.SuperLike, default);
        await db
            .Games.Where(g => g.BggId == 83005)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.MaxPlayers, 2));
        await db
            .Games.Where(g => g.BggId == 83006)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.MinPlayers, (int?)null));
        var service = Service(db);
        var plan = await service.CreateAsync(
            session,
            new() { Name = "Freitag", Date = new(2026, 10, 30) },
            default
        );
        Assert.False(plan.IncludeUnowned);
        var board = await service.BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(new[] { 83001, 83002 }, board.Candidates.Select(g => g.Id).Order().ToArray());
        Assert.All(board.Candidates, g => Assert.True(g.IsOwned));
        board = await service.UpdateAsync(
            plan.Id,
            session,
            new()
            {
                Name = plan.Name,
                Players = 3,
                IncludeUnowned = true,
            },
            default
        );
        Assert.Equal(
            new[] { 83001, 83002, 83003 },
            board.Candidates.Select(g => g.Id).Order().ToArray()
        );
        Assert.False(board.Candidates.Single(g => g.Id == 83003).IsOwned);
        Assert.All(board.Candidates, g => Assert.NotNull(g.MatchQuality));
        var before = await db.InteractionEvents.CountAsync(i => i.SessionId == session);
        board = await service.ChooseAsync(plan.Id, session, 83002, SwipeAction.SuperLike, default);
        Assert.DoesNotContain(board.Candidates, g => g.Id == 83002);
        Assert.Equal(83002, Assert.Single(board.Selections.Items).Game.Id);
        Assert.Equal(SwipeAction.SuperLike, board.Selections.Items[0].Action);
        await service.ChooseAsync(plan.Id, session, 83001, SwipeAction.Skip, default);
        Assert.Equal(before, await db.InteractionEvents.CountAsync(i => i.SessionId == session));
        Assert.Equal(
            SwipeAction.Dislike,
            (await interactions.CurrentAsync(session, default)).Single(i => i.BggId == 83002).Action
        );
        await using var restarted = fixture.CreateDb();
        var saved = await Service(restarted).BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(83002, Assert.Single(saved.Selections.Items).Game.Id);
        Assert.DoesNotContain(saved.Candidates, g => g.Id is 83001 or 83002);
        var second = await service.CreateAsync(session, new(), default);
        Assert.Contains(
            (await service.BoardAsync(second.Id, session, 1, default)).Candidates,
            g => g.Id == 83002
        );
    }

    [Fact]
    public async Task NightWritesAreSessionScopedPoolValidatedAndCanRemoveUnavailableSelection()
    {
        await SeedAsync(84001, 84002);
        var session = Session();
        await using var db = fixture.CreateDb();
        var interactions = new EfInteractionRepository(db);
        await interactions.SetOwnershipAsync(session, 84001, true, default);
        var service = Service(db);
        var plan = await service.CreateAsync(session, new(), default);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChooseAsync(plan.Id, session, 84002, SwipeAction.Like, default)
        );
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.BoardAsync(plan.Id, Session(), 1, default)
        );
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ChooseAsync(plan.Id, Session(), 84001, SwipeAction.Like, default)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("invalid session", new(), default)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(plan.Id, session, new() { Players = 0 }, default)
        );
        await service.ChooseAsync(plan.Id, session, 84001, SwipeAction.Like, default);
        await service.ChooseAsync(plan.Id, session, 84001, SwipeAction.Like, default);
        Assert.Equal(1, await db.GameNightChoices.CountAsync(c => c.GameNightId == plan.Id));
        await interactions.SetOwnershipAsync(session, 84001, false, default);
        var board = await service.BoardAsync(plan.Id, session, 1, default);
        Assert.False(Assert.Single(board.Selections.Items).Game.IsOwned);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChooseAsync(plan.Id, session, 84001, SwipeAction.SuperLike, default)
        );
        board = await service.ChooseAsync(plan.Id, session, 84001, SwipeAction.Skip, default);
        Assert.Empty(board.Selections.Items);
        Assert.Empty(board.Candidates);
        Assert.Equal(0, await db.InteractionEvents.CountAsync(i => i.SessionId == session));
    }

    [Fact]
    public async Task PlansAndSelectionsArePagedAndSurviveAContextRestart()
    {
        var ids = Enumerable.Range(85001, 25).ToArray();
        await SeedAsync(ids);
        var session = Session();
        await using var db = fixture.CreateDb();
        var service = Service(db);
        for (var i = 0; i < 21; i++)
            await service.CreateAsync(session, new() { Name = "Abend " + i }, default);
        var first = await service.ListAsync(session, 1, default);
        var second = await service.ListAsync(session, 2, default);
        Assert.Equal(21, first.Total);
        Assert.Equal(20, first.Items.Count);
        Assert.Single(second.Items);
        var plan = first.Items[0];
        foreach (var id in ids)
        {
            await new EfInteractionRepository(db).SetOwnershipAsync(session, id, true, default);
            await new EfGameNightRepository(db).SaveChoiceAsync(
                plan.Id,
                session,
                id,
                SwipeAction.Like,
                default
            );
        }
        await using var restarted = fixture.CreateDb();
        var board = await Service(restarted).BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(25, board.Selections.Total);
        Assert.Equal(24, board.Selections.Items.Count);
        Assert.Single(
            (await Service(restarted).BoardAsync(plan.Id, session, 2, default)).Selections.Items
        );
        Assert.Equal(0, board.RemainingCount);
    }
}

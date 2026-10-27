import { test, expect } from "@playwright/test";
import type {
  GameMatchQuality,
  GameNight,
  GameNightSettings,
} from "../src/api/nextTurnApi";
const games = [1, 2, 3].map((id) => ({
  id,
  name: "Fixture Game " + id,
  designers:
    id === 1
      ? ["Fixture Designer A", "Fixture Designer B"]
      : id === 2
        ? ["Fixture Designer A"]
        : [],
  bggRank: id,
  thumbnail: "",
  image: "",
  categories: ["Fantasy"],
  mechanics: ["Deck Building"],
  gameTypes: ["boardgame"],
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  averageRating: 8,
  complexity: 3,
  bestPlayerCounts: [3],
  recommendedPlayerCounts: [3],
  detailsAvailable: true,
}));
test.beforeEach(async ({ page }) => {
  const ratings = new Map<number, number>();
  const owned = new Set<number>();
  const nights = new Map<
    string,
    { plan: GameNight; choices: Map<number, number> }
  >();
  function enrich(game: (typeof games)[number]) {
    return {
      ...game,
      isOwned: owned.has(game.id),
      userAction: ratings.get(game.id),
    };
  }
  function board(id: string, maxRank?: number) {
    const night = nights.get(id)!;
    const candidates = games
      .filter(
        (g) =>
          (maxRank === undefined || g.bggRank <= maxRank) &&
          !night.choices.has(g.id) &&
          g.minPlayers <= night.plan.players &&
          g.maxPlayers >= night.plan.players &&
          (owned.has(g.id) ||
            (night.plan.includeUnowned &&
              [1, 2].includes(ratings.get(g.id) ?? -1))),
      )
      .map(enrich);
    const selections = games
      .filter(
        (g) =>
          (maxRank === undefined || g.bggRank <= maxRank) &&
          [1, 2].includes(night.choices.get(g.id) ?? -1),
      )
      .map((g) => ({ game: enrich(g), action: night.choices.get(g.id) }));
    return {
      plan: night.plan,
      candidates,
      selections: {
        items: selections,
        total: selections.length,
        page: 1,
        pageSize: 24,
      },
      remainingCount: candidates.length,
    };
  }
  await page.route(
    (url) => url.pathname.startsWith("/api/"),
    async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname;
      const maxRank = url.searchParams.has("maxRank")
        ? Number(url.searchParams.get("maxRank"))
        : undefined;
      let data: unknown;
      if (path === "/api/game-nights") {
        if (route.request().method() === "POST") {
          const settings = route.request().postDataJSON() as GameNightSettings;
          const id =
            "00000000-0000-0000-0000-" +
            String(nights.size + 1).padStart(12, "0");
          const plan = { ...settings, id };
          nights.set(id, { plan, choices: new Map() });
          data = plan;
        } else {
          const items = [...nights.values()].map((n) => n.plan);
          data = { items, total: items.length, page: 1, pageSize: 20 };
        }
      } else if (path.startsWith("/api/game-nights/")) {
        const parts = path.split("/");
        const id = parts[3];
        if (parts[4] === "choices")
          nights
            .get(id)!
            .choices.set(
              Number(parts[5]),
              route.request().postDataJSON().action,
            );
        else if (route.request().method() === "PUT")
          nights.get(id)!.plan = { ...route.request().postDataJSON(), id };
        data = board(id, maxRank);
      } else if (path.startsWith("/api/player/owned/")) {
        const id = Number(path.split("/").at(-1));
        if (route.request().postDataJSON().isOwned) owned.add(id);
        else owned.delete(id);
        await route.fulfill({ status: 204 });
        return;
      } else if (path === "/api/health") data = { status: "Test API" };
      else if (path === "/api/games/status")
        data = {
          catalog: {
            total: 10000,
            detailed: 10000,
            pending: 0,
            failed: 0,
            importRunning: false,
          },
        };
      else if (path === "/api/games/facets")
        data = { categories: ["Fantasy"], mechanics: ["Deck Building"] };
      else if (path === "/api/games/recommendations")
        data = games.filter(
          (g) =>
            !ratings.has(g.id) &&
            (maxRank === undefined || g.bggRank <= maxRank),
        );
      else if (path === "/api/swipes") {
        const body = route.request().postDataJSON();
        ratings.set(body.gameId, body.action);
        data = {
          sessionId: body.sessionId,
          totalSwipes: ratings.size,
          ratedGameIds: [...ratings.keys()],
          categoryScores: {},
          mechanicScores: {},
          preferredComplexity: 3,
        };
      } else if (path.startsWith("/api/player/favorites/")) {
        ratings.set(Number(path.split("/").at(-1)), 3);
        await route.fulfill({ status: 204 });
        return;
      } else if (path === "/api/player/statistics") {
        const favorites = games.filter((g) =>
          [1, 2].includes(ratings.get(g.id) ?? -1),
        );
        data = {
          favorites: favorites.length,
          likes: favorites.length,
          superLikes: 0,
          averageRating: 8,
          averageComplexity: 3,
          missingData: {},
          categories: [
            { name: "Fantasy", count: favorites.length, percentage: 100 },
          ],
          mechanics: [],
          designers: ["Fixture Designer A", "Fixture Designer B"]
            .map((name) => {
              const count = favorites.filter((g) =>
                g.designers.includes(name),
              ).length;
              return {
                name,
                count,
                percentage: favorites.length
                  ? (100 * count) / favorites.length
                  : 0,
              };
            })
            .filter((d) => d.count > 0),
          complexity: [],
          bestPlayerCounts: favorites.length
            ? [{ name: "3", count: favorites.length, percentage: 100 }]
            : [],
          preferredPlayers: {
            counts: favorites.length ? [3] : [],
            knownGames: favorites.length,
            matchingGames: favorites.length,
            percentage: favorites.length ? 100 : null,
            isProvisional: true,
          },
          playTimePreference: {
            tendency: favorites.length ? "short" : "unknown",
            knownGames: favorites.length,
            missingGames: 0,
            shortGames: favorites.length,
            longGames: 0,
            variableGames: 0,
            shortPercentage: favorites.length ? 100 : null,
            longPercentage: favorites.length ? 0 : null,
            variablePercentage: favorites.length ? 0 : null,
            isProvisional: true,
          },
          recommendedPlayerCounts: [],
          playTimes: [],
          profile: "Testprofil aus aktuellen Favoriten.",
          percentageDenominator:
            "Alle aktuellen Favoriten, jedes Spiel einmal.",
        };
      } else if (
        path === "/api/games" ||
        path === "/api/player/favorites" ||
        path === "/api/player/owned"
      ) {
        const q = (url.searchParams.get("q") ?? "").toLowerCase();
        const items = games
          .filter(
            (g) =>
              g.name.toLowerCase().includes(q) &&
              (maxRank === undefined || g.bggRank <= maxRank) &&
              (path === "/api/games" ||
                (path === "/api/player/owned"
                  ? owned.has(g.id)
                  : [1, 2].includes(ratings.get(g.id) ?? -1))),
          )
          .map(enrich);
        data = { items, total: items.length, page: 1, pageSize: 24 };
      } else {
        const game = games.find((g) => g.id === Number(path.split("/").at(-1)));
        data = {
          ...game,
          userAction: ratings.get(game?.id ?? 0),
          isOwned: owned.has(game?.id ?? 0),
          description: "A local test game.",
        };
      }
      await route.fulfill({ json: data });
    },
  );
});
test("rated game disappears and catalog likes populate favorites and dashboard", async ({
  page,
}) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Fixture Game 1", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Fixture Game 2", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Fixture Game 1", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await page
    .getByRole("searchbox", { name: "Spielname suchen" })
    .fill("Game 3");
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await page
    .locator(".catalog-card")
    .getByRole("button", { name: "Like", exact: true })
    .click();
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.reload();
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Dein Spielerprofil" }),
  ).toBeVisible();
  await expect(
    page
      .locator(".metric")
      .filter({ has: page.getByText("Favoriten", { exact: true }) }),
  ).toContainText("2");
});
test("details open and close accessibly and mobile view does not overflow", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await page
    .getByRole("button", { name: "Fixture Game 1 – Details öffnen" })
    .click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(
    page
      .getByRole("dialog")
      .getByRole("heading", { name: "Fixture Game 1", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth,
  );
  expect(overflow).toBe(false);
});

test("a failed swipe keeps the game and displays a persistent error", async ({
  page,
}) => {
  await page.route(
    (url) => url.pathname === "/api/swipes",
    (route) =>
      route.fulfill({
        status: 503,
        json: { message: "Speichern derzeit nicht möglich." },
      }),
  );
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Fixture Game 1", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "Speichern derzeit nicht möglich.",
  );
  await expect(
    page.getByRole("heading", { name: "Fixture Game 1", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Like", exact: true }),
  ).toBeEnabled();
});

test("match indicator displays the API index and its data limitations", async ({
  page,
}) => {
  let quality: GameMatchQuality = {
    percentage: 78,
    matchedGenre: "Fantasy",
    comparedFavorites: 2,
    availableFavorites: 2,
    featureCoveragePercentage: 100,
    isProvisional: true,
  };
  await page.route(
    (url) => url.pathname === "/api/games/recommendations",
    (route) =>
      route.fulfill({
        json: games.map((game) => ({ ...game, matchQuality: quality })),
      }),
  );
  await page.goto("/");
  await expect(
    page.getByRole("img", {
      name: "Matchqualität 78 Prozent, vorläufig",
      exact: true,
    }),
  ).toBeVisible();
  await expect(page.locator(".swipe-cover .match-quality")).toContainText(
    "vorläufig",
  );
  await expect(page.locator(".swipe-cover .match-quality")).toHaveAttribute(
    "title",
    /Genreprofil „Fantasy“/,
  );
  quality = {
    percentage: null,
    comparedFavorites: 0,
    availableFavorites: 0,
    featureCoveragePercentage: 0,
    isProvisional: true,
  };
  await page
    .getByRole("button", { name: "Empfehlungen aktualisieren" })
    .click();
  await expect(
    page.getByRole("img", { name: "Matchqualität noch offen", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".swipe-cover .match-quality")).not.toContainText(
    "0 %",
  );
  quality = {
    percentage: 92,
    matchedGenre: "Economic",
    comparedFavorites: 8,
    availableFavorites: 8,
    featureCoveragePercentage: 100,
    isProvisional: false,
  };
  await page
    .getByRole("button", { name: "Empfehlungen aktualisieren" })
    .click();
  await expect(
    page.getByRole("img", { name: "Matchqualität 92 Prozent", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".swipe-cover .match-quality")).not.toContainText(
    "vorläufig",
  );
  await expect(page.locator(".swipe-cover .match-quality")).toHaveAttribute(
    "title",
    /Genreprofil „Economic“/,
  );
  await page.route(
    (url) => url.pathname === "/api/games/1",
    (route) => route.fulfill({ json: { ...games[0], matchQuality: quality } }),
  );
  await page.getByRole("button", { name: "Spieldetails ansehen" }).click();
  await expect(page.getByRole("dialog")).toContainText(
    "Passendes Genreprofil: Economic",
  );
  await expect(
    page.getByRole("dialog").locator(".match-quality"),
  ).toHaveAttribute("title", /Andere Genreprofile senken diesen Wert nicht/);
});

test("ownership and favorites stay independent and saved nights default to owned games", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  const first = page
    .locator(".catalog-card")
    .filter({ hasText: "Fixture Game 1" });
  const second = page
    .locator(".catalog-card")
    .filter({ hasText: "Fixture Game 2" });
  await first.getByRole("button", { name: "Besitze ich", exact: true }).click();
  await expect(
    first.getByRole("button", { name: "✓ Im Besitz", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await second.getByRole("button", { name: "Like", exact: true }).click();
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await expect(page.locator(".catalog-card")).toContainText("Fixture Game 2");
  await page.getByRole("button", { name: "In Besitz", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await expect(page.locator(".catalog-card")).toContainText("Fixture Game 1");
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Name", exact: true })
    .fill("Freitag");
  await page
    .getByRole("button", { name: "Spieleabend anlegen", exact: true })
    .click();
  const borrowedToggle = page.getByRole("button", {
    name: "Auch Spiele nicht im Besitz anzeigen",
    exact: true,
  });
  await expect(borrowedToggle).toHaveAttribute("aria-pressed", "false");
  await expect(page.locator(".night-view .swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await page.getByRole("button", { name: "Heute nicht", exact: true }).click();
  await expect(page.locator(".night-view .swipe-game-card")).toHaveCount(0);
  await borrowedToggle.click();
  await expect(borrowedToggle).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".night-view .swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  await expect(page.locator(".night-source")).toContainText("Zum Ausleihen");
  await page.getByRole("button", { name: "Top-Wahl", exact: true }).click();
  await expect(page.locator(".shortlist-card")).toHaveCount(1);
  await expect(page.locator(".shortlist-card")).toContainText("Top-Wahl");
  await page.reload();
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  await expect(page.locator(".shortlist-card")).toContainText("Fixture Game 2");
  await expect(page.locator(".night-view .swipe-game-card")).toHaveCount(0);
  await expect(borrowedToggle).toHaveAttribute("aria-pressed", "true");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth > innerWidth,
    ),
  ).toBe(false);
  await page
    .getByRole("button", {
      name: "Fixture Game 2 vom Spieleabend entfernen",
      exact: true,
    })
    .click();
  await expect(page.locator(".shortlist-card")).toHaveCount(0);
  await page
    .getByRole("button", { name: "Neuer Spieleabend", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Spieleabend anlegen", exact: true })
    .click();
  await expect(borrowedToggle).toHaveAttribute("aria-pressed", "false");
  await expect(page.locator(".night-view .swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await page.getByRole("button", { name: "Favoriten", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await expect(page.locator(".catalog-card")).toContainText("Fixture Game 2");
});

test("failed night choice preserves its card and shortlist", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await page
    .locator(".catalog-card")
    .first()
    .getByRole("button", { name: "Besitze ich", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Spieleabend anlegen", exact: true })
    .click();
  await expect(page.locator(".night-view .swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await page.route(
    (url) => url.pathname.includes("/choices/"),
    (route) =>
      route.fulfill({
        status: 503,
        json: { message: "Auswahl derzeit nicht möglich." },
      }),
  );
  await page.getByRole("button", { name: "Vormerken", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "Auswahl derzeit nicht möglich.",
  );
  await expect(page.locator(".night-view .swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await expect(page.locator(".shortlist-card")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Vormerken", exact: true }),
  ).toBeEnabled();
});

test("rank limit is shared and persistent without removing favorites or ownership", async ({
  page,
}) => {
  let ratings = 0;
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/swipes") ratings++;
  });
  await page.goto("/");
  const rank = page.getByRole("spinbutton", { name: "BGG-Rang bis" });
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await rank.fill("1");
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(page.locator(".swipe-game-card")).toHaveCount(0);
  await rank.fill("2");
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await expect(rank).toHaveValue("2");
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await rank.clear();
  await expect(page.locator(".catalog-card")).toHaveCount(3);
  const third = page
    .locator(".catalog-card")
    .filter({ hasText: "Fixture Game 3" });
  await third.getByRole("button", { name: "Besitze ich", exact: true }).click();
  await expect(
    third.getByRole("button", { name: "✓ Im Besitz", exact: true }),
  ).toBeVisible();
  await third.getByRole("button", { name: "Like", exact: true }).click();
  await rank.fill("2");
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await expect(page.locator(".catalog-card")).toContainText("Fixture Game 1");
  await rank.clear();
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.getByRole("button", { name: "In Besitz", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await rank.fill("2");
  await expect(page.locator(".result-status")).toContainText(
    "Keine Spiele für diese Filter gefunden.",
  );
  await expect(page.locator(".catalog-card")).toHaveCount(0);
  await page.reload();
  await expect(rank).toHaveValue("2");
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await page.getByRole("button", { name: "In Besitz", exact: true }).click();
  await rank.clear();
  await expect(page.locator(".catalog-card")).toHaveCount(1);
  await expect(page.locator(".catalog-card")).toContainText("Fixture Game 3");
  expect(ratings).toBe(2);
});

test("rank limit filters night responses and preserves hidden saved choices", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  for (const id of [1, 2, 3]) {
    const card = page
      .locator(".catalog-card")
      .filter({ hasText: "Fixture Game " + id });
    await card
      .getByRole("button", { name: "Besitze ich", exact: true })
      .click();
    await expect(
      card.getByRole("button", { name: "✓ Im Besitz", exact: true }),
    ).toBeVisible();
  }
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  const rank = page.getByRole("spinbutton", { name: "BGG-Rang bis" });
  await rank.fill("2");
  await expect(
    page.getByRole("button", { name: "Spieleabend anlegen", exact: true }),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Spieleabend anlegen", exact: true })
    .click();
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 1",
  );
  await page.getByRole("button", { name: "Vormerken", exact: true }).click();
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  await page.getByRole("button", { name: "Top-Wahl", exact: true }).click();
  await expect(page.locator(".swipe-game-card")).toHaveCount(0);
  await expect(page.locator(".shortlist-card")).toHaveCount(2);
  await page
    .getByRole("button", {
      name: "Auch Spiele nicht im Besitz anzeigen",
      exact: true,
    })
    .click();
  await expect(page.locator(".swipe-game-card")).toHaveCount(0);
  await page.locator(".night-settings-panel > summary").click();
  await page
    .getByRole("textbox", { name: "Name", exact: true })
    .fill("Entwurf bleibt erhalten");
  await rank.fill("1");
  await expect(page.locator(".shortlist-card")).toHaveCount(1);
  await expect(
    page.getByRole("textbox", { name: "Name", exact: true }),
  ).toHaveValue("Entwurf bleibt erhalten");
  await expect(page.locator(".shortlist-card")).toContainText("Fixture Game 1");
  await page.reload();
  await expect(rank).toHaveValue("1");
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  await expect(page.locator(".shortlist-card")).toHaveCount(1);
  await expect(page.locator(".swipe-game-card")).toHaveCount(0);
  await rank.clear();
  await expect(page.locator(".shortlist-card")).toHaveCount(2);
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 3",
  );
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth > innerWidth,
    ),
  ).toBe(false);
});

test("typing a rank never reuses a previous swipe or sends a rating", async ({
  page,
}) => {
  const votes: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/swipes")
      votes.push(request.postData() ?? "");
  });
  await page.goto("/");
  await expect(page.locator(".swipe-game-card")).toHaveCSS("opacity", "1");
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  await expect(page.locator(".swipe-game-card")).toHaveCSS("opacity", "1");
  const rank = page.getByRole("spinbutton", { name: "BGG-Rang bis" });
  const [maximumHorizontalOffset] = await Promise.all([
    page.evaluate(
      () =>
        new Promise<number>((resolve) => {
          const until = performance.now() + 1200;
          let maximum = 0;
          function sample() {
            document.querySelectorAll(".swipe-game-card").forEach((card) => {
              const transform = getComputedStyle(card).transform;
              if (transform !== "none")
                maximum = Math.max(
                  maximum,
                  Math.abs(new DOMMatrixReadOnly(transform).m41),
                );
            });
            if (performance.now() < until) requestAnimationFrame(sample);
            else resolve(maximum);
          }
          sample();
        }),
    ),
    rank.pressSequentially("500", { delay: 40 }),
  ]);
  await expect(rank).toHaveValue("500");
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 2",
  );
  expect(maximumHorizontalOffset).toBeLessThan(1);
  expect(votes).toHaveLength(1);
  await page.getByRole("button", { name: "Dislike", exact: true }).click();
  await expect(page.locator(".swipe-game-card")).toContainText(
    "Fixture Game 3",
  );
  expect(votes).toHaveLength(2);
});

test("profile shows community best counts and duration tendencies instead of ranking or time averages", async ({
  page,
}) => {
  let statistics = {
    favorites: 5,
    likes: 4,
    superLikes: 1,
    averageRating: 8,
    averageComplexity: 3,
    categories: [],
    mechanics: [],
    complexity: [],
    bestPlayerCounts: [
      { name: "2", count: 3, percentage: 60 },
      { name: "4", count: 3, percentage: 60 },
    ],
    recommendedPlayerCounts: [{ name: "3", count: 5, percentage: 100 }],
    preferredPlayers: {
      counts: [2, 4],
      knownGames: 5,
      matchingGames: 3,
      percentage: 60 as number | null,
      isProvisional: false,
    },
    playTimePreference: {
      tendency: "short",
      knownGames: 5,
      missingGames: 0,
      shortGames: 4,
      longGames: 1,
      variableGames: 0,
      shortPercentage: 80 as number | null,
      longPercentage: 20 as number | null,
      variablePercentage: 0 as number | null,
      isProvisional: false,
    },
    playTimes: [
      { name: "Schnell (bis 60 Min.)", count: 4, percentage: 80 },
      { name: "Lang (über 60 Min.)", count: 1, percentage: 20 },
    ],
    missingData: {} as Record<string, number>,
    profile: "Tendenzen aus aktuellen Favoriten.",
    percentageDenominator:
      "Alle aktuellen Favoriten, jedes Spiel einmal; Mehrfachzuordnung möglich.",
  };
  await page.route(
    (url) => url.pathname === "/api/player/statistics",
    (route) => route.fulfill({ json: statistics }),
  );
  await page.goto("/");
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  const players = page
    .locator(".metric")
    .filter({ hasText: "Bevorzugte Spieleranzahl" });
  const duration = page
    .locator(".metric")
    .filter({ hasText: "Spieldauer-Tendenz" });
  await expect(players.locator("strong")).toHaveText("2 / 4");
  await expect(players).toContainText(
    "Je 60 % von 5 Favoriten mit Best-Angaben",
  );
  await expect(duration.locator("strong")).toHaveText("Eher schnell");
  await expect(duration).toContainText("80 % bis 60 Min.");
  await expect(page.getByText("Ø BGG-Ranking", { exact: true })).toHaveCount(0);
  await expect(page.getByText("Ø Spieldauer", { exact: true })).toHaveCount(0);
  await expect(
    page.getByRole("heading", {
      name: "Unterstützte Spieleranzahlen",
      exact: true,
    }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", {
      name: "Community: beste Spieleranzahlen",
      exact: true,
    }),
  ).toBeVisible();
  statistics = {
    ...statistics,
    playTimePreference: {
      ...statistics.playTimePreference,
      tendency: "mixed",
      shortGames: 2,
      longGames: 2,
      variableGames: 1,
      shortPercentage: 40,
      longPercentage: 40,
      variablePercentage: 20,
    },
  };
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  await expect(duration.locator("strong")).toHaveText("Gemischt");
  await expect(duration).toContainText(
    "40 % schnell · 40 % lang · 20 % variabel",
  );
  statistics = {
    ...statistics,
    bestPlayerCounts: [],
    preferredPlayers: {
      counts: [],
      knownGames: 0,
      matchingGames: 0,
      percentage: null,
      isProvisional: true,
    },
    playTimePreference: {
      tendency: "unknown",
      knownGames: 0,
      missingGames: 5,
      shortGames: 0,
      longGames: 0,
      variableGames: 0,
      shortPercentage: null,
      longPercentage: null,
      variablePercentage: null,
      isProvisional: true,
    },
    playTimes: [],
    missingData: { "Community-Bestbesetzung": 5, Spieldauer: 5 },
  };
  await page.getByRole("button", { name: "Suche", exact: true }).click();
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  await expect(players.locator("strong")).toHaveText("Noch offen");
  await expect(duration.locator("strong")).toHaveText("Noch offen");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth > innerWidth,
    ),
  ).toBe(false);
});

test("designers appear below titles across cards and details and have their own statistics panel", async ({
  page,
}) => {
  await page.goto("/");
  const swipeCard = page.locator(".swipe-game-card");
  await expect(swipeCard.locator(".designer-line")).toHaveText(
    "von Fixture Designer A, Fixture Designer B",
  );
  const layout = await swipeCard.evaluate((card) => {
    const title = card.querySelector("h2")!;
    const credit = card.querySelector(".designer-line")!;
    return {
      below:
        credit.getBoundingClientRect().top >=
        title.getBoundingClientRect().bottom,
      smaller:
        parseFloat(getComputedStyle(credit).fontSize) <
        parseFloat(getComputedStyle(title).fontSize),
    };
  });
  expect(layout).toEqual({ below: true, smaller: true });
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(swipeCard.locator(".designer-line")).toHaveText(
    "von Fixture Designer A",
  );
  await page.getByRole("button", { name: "Like", exact: true }).click();
  await expect(swipeCard.locator(".designer-line")).toHaveText(
    "Spieldesign unbekannt",
  );
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  const first = page
    .locator(".catalog-card")
    .filter({
      has: page.getByRole("heading", { name: "Fixture Game 1", exact: true }),
    });
  await expect(first.locator(".designer-line")).toHaveText(
    "von Fixture Designer A, Fixture Designer B",
  );
  await first
    .getByRole("button", {
      name: "Fixture Game 1 – Details öffnen",
      exact: true,
    })
    .click();
  await expect(page.getByRole("dialog").locator(".designer-line")).toHaveText(
    "von Fixture Designer A, Fixture Designer B",
  );
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  const panel = page
    .locator(".chart-panel")
    .filter({
      has: page.getByRole("heading", {
        name: "Beliebte Spieldesigner",
        exact: true,
      }),
    });
  await expect(
    panel.locator(".stat-bar").filter({ hasText: "Fixture Designer A" }),
  ).toContainText("100%");
  await expect(
    panel.locator(".stat-bar").filter({ hasText: "Fixture Designer B" }),
  ).toContainText("50%");
  await page.getByRole("button", { name: "Sammlung", exact: true }).click();
  await first.getByRole("button", { name: "Besitze ich", exact: true }).click();
  await expect(
    first.getByRole("button", { name: "✓ Im Besitz", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Spieleabend planen", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Spieleabend anlegen", exact: true })
    .click();
  await expect(swipeCard.locator(".designer-line")).toHaveText(
    "von Fixture Designer A, Fixture Designer B",
  );
  await page.getByRole("button", { name: "Vormerken", exact: true }).click();
  await expect(page.locator(".shortlist-card .designer-line")).toHaveText(
    "von Fixture Designer A, Fixture Designer B",
  );
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth > innerWidth,
    ),
  ).toBe(false);
});

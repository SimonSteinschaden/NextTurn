import { test, expect } from "@playwright/test";
const games = [1, 2, 3].map((id) => ({
  id,
  name: "Fixture Game " + id,
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
  await page.route(
    (url) => url.pathname.startsWith("/api/"),
    async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname;
      let data: unknown;
      if (path === "/api/health") data = { status: "Test API" };
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
        data = games.filter((g) => !ratings.has(g.id));
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
          averageRank: 1,
          averageRating: 8,
          averagePlayTime: 60,
          averageComplexity: 3,
          missingData: {},
          categories: [
            { name: "Fantasy", count: favorites.length, percentage: 100 },
          ],
          mechanics: [],
          complexity: [],
          playerCounts: [],
          recommendedPlayerCounts: [],
          playTimes: [],
          profile: "Testprofil aus aktuellen Favoriten.",
          percentageDenominator:
            "Alle aktuellen Favoriten, jedes Spiel einmal.",
        };
      } else if (path === "/api/games" || path === "/api/player/favorites") {
        const q = (url.searchParams.get("q") ?? "").toLowerCase();
        const items = games
          .filter(
            (g) =>
              g.name.toLowerCase().includes(q) &&
              (path === "/api/games" ||
                [1, 2].includes(ratings.get(g.id) ?? -1)),
          )
          .map((g) => ({ ...g, userAction: ratings.get(g.id) }));
        data = { items, total: items.length, page: 1, pageSize: 24 };
      } else {
        const game = games.find((g) => g.id === Number(path.split("/").at(-1)));
        data = {
          ...game,
          userAction: ratings.get(game?.id ?? 0),
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
  await page.getByRole("button", { name: "Meine Spiele", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.reload();
  await page.getByRole("button", { name: "Meine Spiele", exact: true }).click();
  await expect(page.locator(".catalog-card")).toHaveCount(2);
  await page.getByRole("button", { name: "Profil", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Dein Spielerprofil" }),
  ).toBeVisible();
  await expect(
    page.locator(".metric").filter({ hasText: "Favoriten" }),
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

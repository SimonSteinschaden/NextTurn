const API_BASE_URL = (
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080"
).replace(/\/+$/, "");
export type SwipeAction = 0 | 1 | 2 | 3;
export type BoardGame = {
  id: number;
  bggRank?: number | null;
  name: string;
  description?: string | null;
  thumbnail: string;
  image: string;
  yearPublished?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  playingTime?: number | null;
  minPlayTime?: number | null;
  maxPlayTime?: number | null;
  minAge?: number | null;
  averageRating?: number | null;
  bayesAverage?: number | null;
  usersRated?: number | null;
  complexity?: number | null;
  categories: string[];
  mechanics: string[];
  gameTypes: string[];
  bestPlayerCounts: number[];
  recommendedPlayerCounts: number[];
  detailsAvailable: boolean;
  userAction?: SwipeAction | null;
  recommendationReason?: string | null;
};
export type UserPreferenceProfile = {
  sessionId: string;
  categoryScores: Record<string, number>;
  mechanicScores: Record<string, number>;
  preferredComplexity: number;
  totalSwipes: number;
  ratedGameIds: number[];
};
export type CatalogQuery = {
  q?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
  players?: number;
  maxMinutes?: number;
  minComplexity?: number;
  maxComplexity?: number;
  category?: string;
  mechanic?: string;
  superOnly?: boolean;
};
export type PagedResult = {
  items: BoardGame[];
  total: number;
  page: number;
  pageSize: number;
};
export type CatalogStatus = {
  catalog: {
    total: number;
    detailed: number;
    failed: number;
    pending: number;
    importRunning: boolean;
  };
  lastError?: string | null;
};
export type DistributionItem = {
  name: string;
  count: number;
  percentage: number;
};
export type PlayerStatistics = {
  favorites: number;
  likes: number;
  superLikes: number;
  averageRank?: number | null;
  averageRating?: number | null;
  averagePlayTime?: number | null;
  averageComplexity?: number | null;
  missingData: Record<string, number>;
  categories: DistributionItem[];
  mechanics: DistributionItem[];
  complexity: DistributionItem[];
  playerCounts: DistributionItem[];
  recommendedPlayerCounts: DistributionItem[];
  playTimes: DistributionItem[];
  profile: string;
  percentageDenominator: string;
};
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(API_BASE_URL + path, init);
  if (!response.ok) {
    let message = "Anfrage fehlgeschlagen (HTTP " + response.status + ").";
    try {
      const error = await response.json();
      message = error.message ?? error.title ?? message;
    } catch {
      /* No JSON response. */
    }
    throw new Error(message);
  }
  return response.status === 204
    ? (undefined as T)
    : ((await response.json()) as T);
}
function params(query: Record<string, unknown>): string {
  const values = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== "") values.set(key, String(value));
  });
  return "?" + values.toString();
}
export const getHealthStatus = () =>
  request<{ status: string }>("/api/health").then((r) => r.status);
export const getBoardGame = (id: number, sessionId?: string) =>
  request<BoardGame>("/api/games/" + id + params({ sessionId }));
export const getRecommendations = (
  players?: number,
  sessionId?: string,
  signal?: AbortSignal,
) =>
  request<BoardGame[]>(
    "/api/games/recommendations" + params({ players, sessionId }),
    { signal },
  );
export const searchGames = (
  query: CatalogQuery,
  sessionId: string,
  favorites = false,
  signal?: AbortSignal,
) =>
  request<PagedResult>(
    (favorites ? "/api/player/favorites" : "/api/games") +
      params({ ...query, sessionId }),
    { signal },
  );
export const getFacets = (signal?: AbortSignal) =>
  request<{ categories: string[]; mechanics: string[] }>("/api/games/facets", {
    signal,
  });
export const getCatalogStatus = () =>
  request<CatalogStatus>("/api/games/status");
export const getStatistics = (sessionId: string, signal?: AbortSignal) =>
  request<PlayerStatistics>("/api/player/statistics" + params({ sessionId }), {
    signal,
  });
export const removeFavorite = (id: number, sessionId: string) =>
  request<void>("/api/player/favorites/" + id + params({ sessionId }), {
    method: "DELETE",
  });
export const submitSwipe = (
  sessionId: string,
  gameId: number,
  action: SwipeAction,
) =>
  request<UserPreferenceProfile>("/api/swipes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, gameId, action }),
  });

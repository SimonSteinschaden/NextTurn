const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080").replace(/\/+$/, "");

export type BoardGame = {
        id: number;
        bggRank?: number | null;
        name: string;
        thumbnail: string;
        image: string;
        yearPublished?: number | null;
        minPlayers?: number | null;
        maxPlayers?: number | null;
        playingTime?: number | null;
        averageRating?: number | null;
        complexity?: number | null;
        categories: string[];
        mechanics: string[];
};

export async function getHealthStatus(): Promise<string> {
    const response = await fetch(`${API_BASE_URL}/api/health`);
    if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
    }

    const data = await response.json();

    return data.status;
}

export async function getBoardGame(id: number): Promise<BoardGame> {
    const response = await fetch(`${API_BASE_URL}/api/bgg/${id}`);

    if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
    }
    return await response.json();
}

export async function getRecommendations(
    players?: number,
    sessionId?: string
): Promise<BoardGame[]> {
    const params = new URLSearchParams();

    if (players !== undefined) {
        params.append("players", players.toString());
    }

    if (sessionId) {
        params.append("sessionId", sessionId);
    }

    const url =
        `${API_BASE_URL}/api/games/recommendations` +
        (params.toString() ? `?${params.toString()}` : "");

    const response = await fetch(url);

    if (!response.ok) {
        throw new Error(`API error: ${response.status} for ${url}`);
    }

    return await response.json();
}

export type SwipeAction = 0 | 1 | 2 | 3;

export type UserPreferenceProfile = {
    sessionId: string;
    categoryScores: Record<string, number>;
    mechanicScores: Record<string, number>;
    preferredComplexity: number;
    totalSwipes: number;
};

export async function submitSwipe(
    sessionId: string,
    gameId: number,
    action: SwipeAction
): Promise<UserPreferenceProfile> {
    const response = await fetch(`${API_BASE_URL}/api/swipes`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            sessionId,
            gameId,
            action
        })
    });

    if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
    }

    return await response.json();
}
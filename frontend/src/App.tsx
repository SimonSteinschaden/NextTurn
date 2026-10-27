import { useEffect, useState } from "react";
import { AnimatePresence } from "framer-motion";

import GameCard from "./components/GameCard";
import type { BoardGame } from "./api/nextTurnApi";
import { getRecommendations, submitSwipe } from "./api/nextTurnApi";

function App() {
    const [players, setPlayers] = useState(3);
    const [games, setGames] = useState<BoardGame[]>([]);
    const [error, setError] = useState("");
    const [isLoading, setIsLoading] = useState(false);
    const [swipedGameIds, setSwipedGameIds] = useState<number[]>([]);
    const [exitDirection, setExitDirection] =
        useState<"left" | "right" | "up" | "down">("left");

    const [sessionId] = useState(() => {
        const existingSessionId = localStorage.getItem("nextturn-session-id");

        if (existingSessionId) {
            return existingSessionId;
        }

        const newSessionId = crypto.randomUUID();
        localStorage.setItem("nextturn-session-id", newSessionId);

        return newSessionId;
    });

    useEffect(() => {
        loadRecommendations(true);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [sessionId]);

    async function loadRecommendations(replaceExisting = false) {
        try {
            setIsLoading(true);
            setError("");

            const result = await getRecommendations(players, sessionId);
            const filteredResult = result.filter(
                (game) => !swipedGameIds.includes(game.id)
            );

            setGames((previousGames) => {
                if (replaceExisting) {
                    return filteredResult;
                }

                const existingIds = new Set(previousGames.map((game) => game.id));
                const nextGames = filteredResult.filter(
                    (game) => !existingIds.has(game.id)
                );

                return [...previousGames, ...nextGames];
            });
        } catch (error) {
            console.error(error);
            setError(String(error));
        } finally {
            setIsLoading(false);
        }
    }

    async function handleSwipe(action: 0 | 1 | 2 | 3) {
        const directionMap = {
            0: "left",
            1: "right",
            2: "up",
            3: "down"
        } as const;

        if (!games[0]) {
            return;
        }

        setExitDirection(directionMap[action]);

        try {
            const swipedId = games[0].id;
            const newSwipedGameIds = [...swipedGameIds, swipedId];

            setSwipedGameIds(newSwipedGameIds);
            setGames((previousGames) =>
                previousGames.filter((game) => game.id !== swipedId)
            );

            await submitSwipe(sessionId, swipedId, action);

            const nextQueueSize = games.length - 1;
            if (nextQueueSize <= 5) {
                await loadRecommendations();
            }
        } catch (error) {
            console.error(error);
            setError(String(error));
        }
    }

    function resetSession() {
        const newSessionId = crypto.randomUUID();

        localStorage.setItem("nextturn-session-id", newSessionId);

        window.location.reload();
    }

    const currentGame = games[0];
    const queueLength = games.length;
    const swipeCount = swipedGameIds.length;

    return (
        <div className="min-h-screen bg-zinc-100 flex flex-col items-center p-4">
            <div className="w-full max-w-3xl flex flex-col gap-6">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <label className="flex items-center gap-2 text-sm text-zinc-700">
                        Spieleranzahl
                        <input
                            type="number"
                            min={1}
                            max={12}
                            value={players}
                            onChange={(e) => setPlayers(Number(e.target.value))}
                            className="border rounded-lg px-2 py-1 w-20 text-sm"
                        />
                    </label>

                    <div className="flex flex-wrap gap-3 justify-center">
                        <button
                            type="button"
                            onClick={() => loadRecommendations(true)}
                            className="bg-blue-600 text-white px-4 py-2 rounded-xl shadow-sm hover:bg-blue-500 transition"
                        >
                            Empfehlungen laden
                        </button>
                        <button
                            type="button"
                            onClick={resetSession}
                            className="bg-zinc-700 text-white px-4 py-2 rounded-xl shadow-sm hover:bg-zinc-600 transition"
                        >
                            Session zurücksetzen
                        </button>
                    </div>
                </div>

                <div className="flex flex-col items-center gap-2 text-sm text-zinc-600">
                    {queueLength > 0 && (
                        <p>
                            Spiel {swipeCount + 1} von {swipeCount + queueLength}
                        </p>
                    )}
                    {isLoading && <p>Lade Empfehlungen...</p>}
                    {error && <p className="text-red-600">{error}</p>}
                    {!isLoading && !error && queueLength === 0 && (
                        <p className="text-zinc-500">Keine Empfehlungen verfügbar.</p>
                    )}
                </div>

                <section className="w-full flex justify-center">
                    <div className="w-full max-w-md min-h-[760px]">
                        <AnimatePresence mode="wait">
                            {currentGame ? (
                                <GameCard
                                    key={currentGame.id}
                                    game={currentGame}
                                    exitDirection={exitDirection}
                                    onSwipe={(direction) => {
                                        if (direction === "left") {
                                            handleSwipe(0);
                                        }
                                        if (direction === "right") {
                                            handleSwipe(1);
                                        }
                                    }}
                                />
                            ) : null}
                        </AnimatePresence>
                    </div>
                </section>

                <div className="w-full max-w-md flex flex-wrap justify-center gap-3">
                    <button
                        type="button"
                        onClick={() => handleSwipe(0)}
                        className="flex-1 min-w-[120px] bg-red-600 text-white px-4 py-3 rounded-2xl shadow-sm hover:bg-red-500 transition"
                    >
                        Dislike
                    </button>
                    <button
                        type="button"
                        onClick={() => handleSwipe(3)}
                        className="flex-1 min-w-[120px] bg-zinc-800 text-white px-4 py-3 rounded-2xl shadow-sm hover:bg-zinc-700 transition"
                    >
                        Skip
                    </button>
                    <button
                        type="button"
                        onClick={() => handleSwipe(1)}
                        className="flex-1 min-w-[120px] bg-emerald-600 text-white px-4 py-3 rounded-2xl shadow-sm hover:bg-emerald-500 transition"
                    >
                        Like
                    </button>
                    <button
                        type="button"
                        onClick={() => handleSwipe(2)}
                        className="flex-1 min-w-[120px] bg-sky-600 text-white px-4 py-3 rounded-2xl shadow-sm hover:bg-sky-500 transition"
                    >
                        SuperLike
                    </button>
                </div>
            </div>
        </div>
    );
}

export default App;

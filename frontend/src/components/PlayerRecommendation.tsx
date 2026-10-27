import type { BoardGame } from "../api/nextTurnApi";

export default function PlayerRecommendation({
  game,
  selectedPlayers,
}: {
  game: Pick<BoardGame, "recommendedPlayerCounts" | "bestPlayerCounts">;
  selectedPlayers?: number;
}) {
  const counts = [
    ...new Set([
      ...(game.recommendedPlayerCounts ?? []),
      ...(game.bestPlayerCounts ?? []),
    ]),
  ]
    .filter((n) => Number.isInteger(n) && n >= 1 && n <= 100)
    .sort((a, b) => a - b);
  const matches =
    selectedPlayers !== undefined && counts.includes(selectedPlayers);
  const listed =
    counts.length > 1
      ? counts.slice(0, -1).join(", ") + " oder " + counts[counts.length - 1]
      : String(counts[0]);
  const text = counts.length
    ? "Empfohlen für " +
      listed +
      (counts.length === 1 && counts[0] === 1 ? " Person" : " Personen")
    : "Spielerempfehlung unbekannt";
  return (
    <span
      className={"player-recommendation" + (matches ? " is-match" : "")}
      data-matches-selected={matches}
      title={
        counts.length
          ? "BGG-Community-Empfehlung." +
            (matches ? " Passt zur ausgewählten Spieleranzahl." : "")
          : "Keine Angaben zur empfohlenen Spieleranzahl."
      }
    >
      {matches && <span aria-hidden="true">✓ </span>}
      {text}
    </span>
  );
}

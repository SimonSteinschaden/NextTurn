import type { GameMatchQuality } from "../api/nextTurnApi";

export default function MatchQualityBadge({
  quality,
  compact = false,
}: {
  quality?: GameMatchQuality | null;
  compact?: boolean;
}) {
  const percentage = quality?.percentage;
  const known = percentage != null;
  const provisional = known && quality?.isProvisional;
  const explanation = known
    ? "Merkmalsähnlichkeit zu " +
      (quality?.comparedFavorites ?? 0) +
      " vergleichbaren Favoriten; SuperLikes zählen stärker. " +
      (provisional
        ? "Vorläufig wegen weniger Vergleiche oder fehlender Merkmale. "
        : "") +
      "Ein Ähnlichkeitsindex, keine gemessene Gefallenswahrscheinlichkeit."
    : "Weitere Favoriten oder vollständigere Spieldaten ermöglichen diesen Vergleich. Ohne ausreichende Daten bleibt der Matchwert offen.";
  return (
    <span
      className={
        "match-quality" +
        (compact ? " compact" : "") +
        (!known ? " unknown" : provisional ? " provisional" : "")
      }
      role="img"
      aria-label={
        known
          ? "Matchqualität " +
            percentage +
            " Prozent" +
            (provisional ? ", vorläufig" : "")
          : "Matchqualität noch offen"
      }
      title={explanation}
      aria-description={explanation}
    >
      {known ? (
        <>
          <strong>{percentage.toLocaleString("de-DE")} %</strong>
          <span>Match</span>
          {provisional && <small>vorläufig</small>}
        </>
      ) : (
        <span>Match offen</span>
      )}
    </span>
  );
}

import { useEffect, useRef, useState } from "react";
import { getBoardGame } from "../api/nextTurnApi";
import type { BoardGame, SwipeAction } from "../api/nextTurnApi";
export default function GameDetails({
  game,
  sessionId,
  onClose,
  onRate,
}: {
  game: BoardGame;
  sessionId: string;
  onClose: () => void;
  onRate: (g: BoardGame, a: SwipeAction) => Promise<void>;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [details, setDetails] = useState(game);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    dialog.current?.showModal();
    let active = true;
    getBoardGame(game.id, sessionId)
      .then((g) => {
        if (active) setDetails(g);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [game.id, sessionId]);
  async function rate(action: SwipeAction) {
    setBusy(true);
    setError("");
    try {
      await onRate(details, action);
      setDetails((g) => ({ ...g, userAction: action }));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Speichern fehlgeschlagen.");
    } finally {
      setBusy(false);
    }
  }
  const description = details.description
    ? new DOMParser().parseFromString(details.description, "text/html").body
        .textContent
    : null;
  return (
    <dialog
      ref={dialog}
      className="game-dialog"
      onCancel={onClose}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      aria-labelledby="detail-name"
    >
      <div className="dialog-body">
        <button
          className="dialog-close"
          onClick={onClose}
          aria-label="Spieldetails schließen"
        >
          ✕
        </button>
        {details.image && (
          <img
            className="detail-cover"
            src={details.image}
            alt={details.name}
          />
        )}
        <p className="eyebrow">
          {details.gameTypes.join(" · ") || "Spieltyp noch unbekannt"}
        </p>
        <h1 id="detail-name">{details.name}</h1>
        <div className="detail-facts">
          <span>BGG #{details.bggRank ?? "–"}</span>
          <span>★ {details.averageRating?.toFixed(2) ?? "–"}</span>
          <span>{details.yearPublished ?? "Jahr unbekannt"}</span>
          <span>
            {details.minPlayers ?? "?"}–{details.maxPlayers ?? "?"} Personen
          </span>
          <span>
            {details.minPlayTime ?? details.playingTime ?? "?"}–
            {details.maxPlayTime ?? details.playingTime ?? "?"} Min.
          </span>
          <span>Ab {details.minAge ?? "?"} Jahren</span>
          <span>Komplexität {details.complexity?.toFixed(1) ?? "–"}/5</span>
        </div>
        <p className="muted">
          Bayes-Bewertung: {details.bayesAverage?.toFixed(2) ?? "–"} ·{" "}
          {details.usersRated?.toLocaleString("de-DE") ?? "–"} Bewertungen
        </p>
        <p className="muted">
          BGG empfiehlt:{" "}
          {details.recommendedPlayerCounts.join(", ") || "Keine Angaben"}{" "}
          Personen · Beste Besetzung:{" "}
          {details.bestPlayerCounts.join(", ") || "Keine Angaben"}
        </p>
        {!details.detailsAvailable && (
          <p className="notice">
            Die BGG-Details wurden noch nicht importiert. Fehlende Angaben
            werden nicht geschätzt.
          </p>
        )}
        <div className="detail-tags">
          {details.categories.map((c) => (
            <span key={c}>{c}</span>
          ))}
        </div>
        <h2>Spielmechaniken</h2>
        <div className="detail-tags secondary">
          {details.mechanics.map((m) => (
            <span key={m}>{m}</span>
          ))}
        </div>
        <h2>Über das Spiel</h2>
        <p className="description">
          {description || "Noch keine Beschreibung vorhanden."}
        </p>
        {error && (
          <p className="notice error" role="alert">
            {error}
          </p>
        )}
        <div className="detail-actions">
          <button
            disabled={busy}
            className={details.userAction === 1 ? "primary liked" : "primary"}
            onClick={() => rate(1)}
          >
            ♥ {details.userAction === 1 ? "Gelikt" : "Like"}
          </button>
          <button
            disabled={busy}
            className="superliked"
            onClick={() => rate(2)}
          >
            ★ {details.userAction === 2 ? "SuperLike gespeichert" : "SuperLike"}
          </button>
          <button disabled={busy} onClick={() => rate(0)}>
            Dislike
          </button>
          <button disabled={busy} onClick={() => rate(3)}>
            Skip
          </button>
        </div>
        <a
          href={"https://boardgamegeek.com/boardgame/" + details.id}
          target="_blank"
          rel="noreferrer"
        >
          Spiel auf BoardGameGeek ansehen ↗
        </a>
      </div>
    </dialog>
  );
}

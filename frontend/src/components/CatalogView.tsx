import { useEffect, useState } from "react";
import { getFacets, searchGames } from "../api/nextTurnApi";
import type {
  BoardGame,
  CatalogQuery,
  PagedResult,
  SwipeAction,
} from "../api/nextTurnApi";

type Props = {
  sessionId: string;
  favorites?: boolean;
  revision: number;
  onOpen: (game: BoardGame) => void;
  onRate: (game: BoardGame, action: SwipeAction) => Promise<void>;
  onRemove: (game: BoardGame) => Promise<void>;
};
export default function CatalogView({
  sessionId,
  favorites = false,
  revision,
  onOpen,
  onRate,
  onRemove,
}: Props) {
  const [query, setQuery] = useState<CatalogQuery>({
    page: 1,
    pageSize: 24,
    sort: "rank",
  });
  const [results, setResults] = useState<PagedResult | null>(null);
  const [facets, setFacets] = useState<{
    categories: string[];
    mechanics: string[];
  }>({ categories: [], mechanics: [] });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [pending, setPending] = useState<number | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    getFacets(controller.signal)
      .then(setFacets)
      .catch(() => {});
    return () => controller.abort();
  }, [revision]);
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setLoading(true);
      setError("");
      searchGames(query, sessionId, favorites, controller.signal)
        .then((result) => {
          setResults(result);
          const last = Math.max(1, Math.ceil(result.total / result.pageSize));
          if (result.page > last) setQuery((q) => ({ ...q, page: last }));
        })
        .catch((e) => {
          if (!controller.signal.aborted)
            setError(e instanceof Error ? e.message : "Suche fehlgeschlagen.");
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
    }, 250);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, sessionId, favorites, revision]);
  function change(values: Partial<CatalogQuery>) {
    setQuery((q) => ({ ...q, ...values, page: 1 }));
  }
  async function action(
    game: BoardGame,
    remove: boolean,
    swipe: SwipeAction = 1,
  ) {
    setPending(game.id);
    setError("");
    try {
      if (remove) await onRemove(game);
      else await onRate(game, swipe);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Bewertung konnte nicht gespeichert werden.",
      );
    } finally {
      setPending(null);
    }
  }
  return (
    <section className="catalog-view" aria-labelledby="catalog-title">
      <div className="section-heading">
        <div>
          <p className="eyebrow">
            {favorites ? "Deine Sammlung" : "Der lokale Spielekatalog"}
          </p>
          <h1 id="catalog-title">
            {favorites ? "Meine Spiele" : "Finde dein nächstes Spiel"}
          </h1>
        </div>
        <span className="count-pill">{results?.total ?? "…"} Spiele</span>
      </div>
      <div className="filter-panel">
        <label className="search-label">
          <span>Spielname</span>
          <input
            type="search"
            placeholder="z. B. Brass, Catan, Ark Nova …"
            value={query.q ?? ""}
            onChange={(e) => change({ q: e.target.value })}
            aria-label="Spielname suchen"
            autoComplete="off"
          />
        </label>
        <div className="filter-grid">
          <label>
            Spieler
            <select
              value={query.players ?? ""}
              onChange={(e) =>
                change({
                  players: e.target.value ? Number(e.target.value) : undefined,
                })
              }
            >
              <option value="">Alle</option>
              {Array.from({ length: 12 }, (_, i) => (
                <option key={i + 1}>{i + 1}</option>
              ))}
            </select>
          </label>
          <label>
            Spieldauer
            <select
              value={query.maxMinutes ?? ""}
              onChange={(e) =>
                change({
                  maxMinutes: e.target.value
                    ? Number(e.target.value)
                    : undefined,
                })
              }
            >
              <option value="">Beliebig</option>
              {[30, 60, 90, 120, 180].map((n) => (
                <option key={n} value={n}>
                  Bis {n} Min.
                </option>
              ))}
            </select>
          </label>
          <label>
            Komplexität
            <select
              value={query.maxComplexity ?? ""}
              onChange={(e) =>
                change({
                  maxComplexity: e.target.value
                    ? Number(e.target.value)
                    : undefined,
                })
              }
            >
              <option value="">Beliebig</option>
              {[2, 3, 4, 5].map((n) => (
                <option key={n} value={n}>
                  Bis {n}/5
                </option>
              ))}
            </select>
          </label>
          <label>
            Genre
            <select
              value={query.category ?? ""}
              onChange={(e) =>
                change({ category: e.target.value || undefined })
              }
            >
              <option value="">Alle Kategorien</option>
              {facets.categories.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </label>
          <label>
            Mechanik
            <select
              value={query.mechanic ?? ""}
              onChange={(e) =>
                change({ mechanic: e.target.value || undefined })
              }
            >
              <option value="">Alle Mechaniken</option>
              {facets.mechanics.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </label>
          <label>
            Sortierung
            <select
              value={query.sort}
              onChange={(e) => change({ sort: e.target.value })}
            >
              <option value="rank">BGG-Ranking</option>
              <option value="rating">Bewertung</option>
              <option value="name">Name A–Z</option>
              <option value="year">Neueste zuerst</option>
            </select>
          </label>
        </div>
        {favorites && (
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={query.superOnly ?? false}
              onChange={(e) => change({ superOnly: e.target.checked })}
            />{" "}
            Nur SuperLikes
          </label>
        )}
      </div>
      {error && (
        <p className="notice error" role="alert">
          {error}
        </p>
      )}
      <p className="result-status" aria-live="polite">
        {loading
          ? "Suche läuft …"
          : results?.total
            ? results.total + " Treffer im lokalen Katalog"
            : favorites
              ? "Noch keine Favoriten. Like ein Spiel, um es hier zu sammeln."
              : "Keine Spiele für diese Filter gefunden."}
      </p>
      <div className="catalog-grid" aria-busy={loading}>
        {results?.items.map((game) => (
          <article className="catalog-card" key={game.id}>
            <button
              className="card-open"
              onClick={() => onOpen(game)}
              aria-label={game.name + " – Details öffnen"}
            >
              <div className="catalog-cover">
                {game.thumbnail || game.image ? (
                  <img
                    src={game.thumbnail || game.image}
                    alt=""
                    loading="lazy"
                  />
                ) : (
                  <span>♟</span>
                )}
                <span className="rank-badge">
                  {game.bggRank ? "#" + game.bggRank : "Ohne Rang"}
                </span>
              </div>
              <div className="card-info">
                <h2>{game.name}</h2>
                <p>
                  {game.yearPublished ?? "Jahr unbekannt"} · ★{" "}
                  {game.averageRating?.toFixed(1) ?? "–"}
                </p>
                <p>
                  {game.minPlayers ?? "?"}–{game.maxPlayers ?? "?"} Personen ·{" "}
                  {game.maxPlayTime ?? game.playingTime ?? "?"} Min.
                </p>
                <p>Komplexität {game.complexity?.toFixed(1) ?? "–"}/5</p>
                {!game.detailsAvailable && (
                  <small>Details werden noch importiert</small>
                )}
              </div>
            </button>
            <div className="card-actions">
              <button
                aria-label={game.userAction === 1 ? "Gelikt" : "Like"}
                className={game.userAction === 1 ? "liked" : ""}
                disabled={pending !== null}
                onClick={() => action(game, false, 1)}
              >
                {game.userAction === 1 ? "♥ Gelikt" : "♡ Like"}
              </button>
              <button
                className={game.userAction === 2 ? "superliked" : ""}
                disabled={pending !== null}
                onClick={() => action(game, false, 2)}
                aria-label={game.name + " superliken"}
              >
                ★
              </button>
              {favorites && (
                <button
                  disabled={pending !== null}
                  onClick={() => action(game, true)}
                  aria-label={game.name + " aus Favoriten entfernen"}
                >
                  Entfernen
                </button>
              )}
            </div>
          </article>
        ))}
      </div>
      {results && results.total > results.pageSize && (
        <nav className="pagination" aria-label="Ergebnisseiten">
          <button
            disabled={loading || results.page <= 1}
            onClick={() => setQuery((q) => ({ ...q, page: (q.page ?? 1) - 1 }))}
          >
            ← Zurück
          </button>
          <span>
            Seite {results.page} von{" "}
            {Math.ceil(results.total / results.pageSize)}
          </span>
          <button
            disabled={
              loading || results.page * results.pageSize >= results.total
            }
            onClick={() => setQuery((q) => ({ ...q, page: (q.page ?? 1) + 1 }))}
          >
            Weiter →
          </button>
        </nav>
      )}
    </section>
  );
}

import { useCallback, useEffect, useRef, useState } from "react";
import { AnimatePresence } from "framer-motion";
import GameCard from "./components/GameCard";
import CatalogView from "./components/CatalogView";
import NightPlanner from "./components/NightPlanner";
import Dashboard from "./components/Dashboard";
import GameDetails from "./components/GameDetails";
import {
  getRecommendations,
  removeFavorite,
  submitSwipe,
  setOwnership,
} from "./api/nextTurnApi";
import type { BoardGame, SwipeAction } from "./api/nextTurnApi";
import diceLogo from "./assets/Würfel-Icon mit eingeprägtem N.png";
import "./App.css";

type Tab = "discover" | "search" | "collection" | "night" | "profile";
const tabs: { id: Tab; label: string; icon: string }[] = [
  { id: "discover", label: "Entdecken", icon: "✦" },
  { id: "search", label: "Suche", icon: "⌕" },
  { id: "collection", label: "Sammlung", icon: "♡" },
  { id: "night", label: "Spieleabend planen", icon: "▦" },
  { id: "profile", label: "Profil", icon: "◉" },
];
function App() {
  const [tab, setTab] = useState<Tab>("discover");
  const [players, setPlayers] = useState(3);
  const [games, setGames] = useState<BoardGame[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [revision, setRevision] = useState(0);
  const [discoveryRevision, setDiscoveryRevision] = useState(0);
  const [collectionMode, setCollectionMode] = useState<"favorites" | "owned">(
    "favorites",
  );
  const [reload, setReload] = useState(0);
  const [selected, setSelected] = useState<BoardGame | null>(null);
  const [exitDirection, setExitDirection] = useState<
    "left" | "right" | "up" | "down"
  >("left");
  const [sessionId] = useState(() => {
    const existing = localStorage.getItem("nextturn-session-id");
    if (existing && /^[a-zA-Z0-9_-]{1,100}$/.test(existing)) return existing;
    const next = crypto.randomUUID();
    localStorage.setItem("nextturn-session-id", next);
    return next;
  });
  const rated = useRef(new Set<number>());
  const savingRef = useRef(false);
  const requestSequence = useRef(0);
  useEffect(() => {
    const controller = new AbortController();
    const sequence = ++requestSequence.current;
    const load = async () => {
      setLoading(true);
      try {
        const result = await getRecommendations(
          players,
          sessionId,
          controller.signal,
        );
        if (sequence === requestSequence.current)
          setGames(result.filter((g) => !rated.current.has(g.id)));
      } catch (e) {
        if (!controller.signal.aborted && sequence === requestSequence.current)
          setError(
            e instanceof Error
              ? e.message
              : "Empfehlungen konnten nicht geladen werden.",
          );
      } finally {
        if (sequence === requestSequence.current) setLoading(false);
      }
    };
    void load();
    return () => controller.abort();
  }, [players, sessionId, discoveryRevision, reload]);
  const rate = useCallback(
    async (game: BoardGame, action: SwipeAction) => {
      if (savingRef.current)
        throw new Error("Die letzte Bewertung wird noch gespeichert.");
      savingRef.current = true;
      setSaving(true);
      // Immediately invalidate an in-flight queue response, even before React rerenders.
      requestSequence.current++;
      rated.current.add(game.id);
      try {
        await submitSwipe(sessionId, game.id, action);
        setGames((old) => old.filter((g) => g.id !== game.id));
        setRevision((v) => v + 1);
        setDiscoveryRevision((v) => v + 1);
      } catch (e) {
        rated.current.delete(game.id);
        setReload((v) => v + 1);
        throw e;
      } finally {
        savingRef.current = false;
        setSaving(false);
      }
    },
    [sessionId],
  );
  async function swipe(action: SwipeAction) {
    const game = games[0];
    if (!game || savingRef.current) return;
    setExitDirection(
      ({ 0: "left", 1: "right", 2: "up", 3: "down" } as const)[action],
    );
    setError("");
    try {
      await rate(game, action);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Bewertung fehlgeschlagen.");
    }
  }
  async function remove(game: BoardGame) {
    await removeFavorite(game.id, sessionId);
    rated.current.add(game.id);
    setRevision((v) => v + 1);
    setDiscoveryRevision((v) => v + 1);
  }
  async function own(game: BoardGame, isOwned: boolean) {
    await setOwnership(game.id, sessionId, isOwned);
    setGames((old) =>
      old.map((g) => (g.id === game.id ? { ...g, isOwned } : g)),
    );
    setRevision((v) => v + 1);
  }
  function newSession() {
    if (
      window.confirm(
        "Eine neue Session startet ein neues Profil. Deine bisherigen Favoriten, Besitzlisten und Spieleabende bleiben unter der alten Session-ID gespeichert, sind hier danach aber nicht mehr sichtbar. Fortfahren?",
      )
    ) {
      localStorage.setItem("nextturn-session-id", crypto.randomUUID());
      window.location.reload();
    }
  }
  return (
    <div className="app-shell">
      <header className="app-header">
        <a
          className="brand"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            setTab("discover");
          }}
        >
          <span className="brand-mark" aria-hidden="true">
            <img src={diceLogo} alt="" width={56} height={56} />
          </span>
          <span>
            next<span>turn</span>
            <small>DEIN NÄCHSTER SPIELEABEND</small>
          </span>
        </a>
        <nav className="main-nav" aria-label="Hauptnavigation">
          {tabs.map((t) => (
            <button
              key={t.id}
              aria-current={tab === t.id ? "page" : undefined}
              onClick={() => setTab(t.id)}
            >
              <span aria-hidden="true">{t.icon}</span>
              {t.label}
            </button>
          ))}
        </nav>
      </header>
      <main className="app-main">
        {tab === "discover" && (
          <section className="discover-view" aria-labelledby="discover-title">
            <div className="section-heading">
              <div>
                <h1 id="discover-title">Was kommt auf den Tisch?</h1>
              </div>
              <label className="players-control">
                Personen
                <select
                  value={players}
                  disabled={saving}
                  onChange={(e) => {
                    setGames([]);
                    setPlayers(Number(e.target.value));
                  }}
                >
                  {Array.from({ length: 12 }, (_, i) => (
                    <option key={i + 1}>{i + 1}</option>
                  ))}
                </select>
              </label>
            </div>
            <div className="discovery-layout">
              <div className="swipe-stage">
                {error && (
                  <p className="notice error" role="alert">
                    {error}
                  </p>
                )}
                <div className="queue-meta" aria-live="polite">
                  <span>
                    {saving
                      ? "Bewertung wird gespeichert …"
                      : loading
                        ? "Empfehlungen werden sortiert …"
                        : ""}
                  </span>
                  <button
                    disabled={saving || loading}
                    onClick={() => {
                      setError("");
                      setReload((v) => v + 1);
                    }}
                    aria-label="Empfehlungen aktualisieren"
                  >
                    ↻
                  </button>
                </div>
                <div className="swipe-card-wrap">
                  <AnimatePresence mode="wait" custom={exitDirection}>
                    {games[0] ? (
                      <GameCard
                        key={games[0].id}
                        game={games[0]}
                        exitDirection={exitDirection}
                        disabled={saving || loading}
                        onSwipe={(direction) => {
                          void swipe(direction === "left" ? 0 : 1);
                        }}
                        onDetails={() => setSelected(games[0])}
                      />
                    ) : (
                      <div className="empty-discovery" key="empty">
                        <span>♟</span>
                        <h2>
                          {loading
                            ? "Wir suchen dein nächstes Spiel …"
                            : "Hier ist gerade alles entdeckt."}
                        </h2>
                        <p>
                          Versuche eine andere Spieleranzahl oder lade die
                          Empfehlungen neu.
                        </p>
                        <button onClick={() => setTab("search")}>
                          Im Katalog stöbern →
                        </button>
                      </div>
                    )}
                  </AnimatePresence>
                </div>
                <div className="swipe-actions">
                  {(
                    [
                      { action: 0, label: "Dislike", icon: "✕" },
                      { action: 3, label: "Skip", icon: "→" },
                      { action: 1, label: "Like", icon: "♥" },
                      { action: 2, label: "SuperLike", icon: "★" },
                    ] as const
                  ).map((a) => (
                    <button
                      key={a.action}
                      className={"swipe-action action-" + a.action}
                      disabled={!games[0] || saving || loading}
                      onClick={() => {
                        void swipe(a.action);
                      }}
                    >
                      <span aria-hidden="true">{a.icon}</span>
                      {a.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </section>
        )}
        {tab === "search" && (
          <CatalogView
            sessionId={sessionId}
            revision={revision}
            onOpen={setSelected}
            onRate={rate}
            onRemove={remove}
            onOwnership={own}
          />
        )}
        {tab === "collection" && (
          <CatalogView
            key={collectionMode}
            sessionId={sessionId}
            collectionMode={collectionMode}
            onCollectionModeChange={setCollectionMode}
            revision={revision}
            onOpen={setSelected}
            onRate={rate}
            onRemove={remove}
            onOwnership={own}
          />
        )}
        {tab === "night" && (
          <NightPlanner
            sessionId={sessionId}
            revision={revision}
            onOwnership={own}
            onOpenCollection={() => {
              setCollectionMode("owned");
              setTab("collection");
            }}
          />
        )}
        {tab === "profile" && (
          <>
            <Dashboard sessionId={sessionId} revision={revision} />
            <div className="session-note">
              <p>
                Dein Profil bleibt über die Session in diesem Browser erhalten.
                Es ist noch kein Benutzerkonto.
              </p>
              <button className="text-button" onClick={newSession}>
                Neue Session beginnen
              </button>
            </div>
          </>
        )}
      </main>
      <footer className="app-footer">
        <a
          href="https://boardgamegeek.com"
          target="_blank"
          rel="noreferrer"
          className="bgg-credit"
        >
          <img src="/powered-by-bgg.png" alt="Powered by BGG" />
        </a>
      </footer>
      {selected && (
        <GameDetails
          key={selected.id}
          game={selected}
          sessionId={sessionId}
          onClose={() => setSelected(null)}
          onRate={rate}
          onOwnership={own}
        />
      )}
    </div>
  );
}
export default App;

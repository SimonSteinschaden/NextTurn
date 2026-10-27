import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { AnimatePresence } from "framer-motion";
import PlayerRecommendation from "./PlayerRecommendation";
import DesignerLine from "./DesignerLine";
import RankFilter from "./RankFilter";
import GameCard from "./GameCard";
import GameDetails from "./GameDetails";
import MatchQualityBadge from "./MatchQualityBadge";
import {
  chooseNightGame,
  createNight,
  getNight,
  listNights,
  updateNight,
} from "../api/nextTurnApi";
import type {
  BoardGame,
  GameNight,
  GameNightSettings,
  NightBoard,
  SwipeAction,
} from "../api/nextTurnApi";

type Props = {
  sessionId: string;
  revision: number;
  maxRank?: number;
  onMaxRankChange: (rank: number | undefined) => void;
  onOwnership: (game: BoardGame, owned: boolean) => Promise<void>;
  onOpenCollection: () => void;
};
const choices = [
  { action: 0, label: "Heute nicht", icon: "✕" },
  { action: 3, label: "Skip", icon: "→" },
  { action: 1, label: "Vormerken", icon: "♥" },
  { action: 2, label: "Top-Wahl", icon: "★" },
] as const;
function message(e: unknown) {
  return e instanceof Error ? e.message : "Speichern fehlgeschlagen.";
}

export default function NightPlanner({
  sessionId,
  revision,
  maxRank,
  onMaxRankChange,
  onOwnership,
  onOpenCollection,
}: Props) {
  const storageKey = "nextturn-night-" + sessionId;
  const [nightId, setNightId] = useState(
    () => localStorage.getItem(storageKey) ?? "",
  );
  const [plans, setPlans] = useState<GameNight[]>([]);
  const [totalPlans, setTotalPlans] = useState(0);
  const [listPage, setListPage] = useState(1);
  const [listLoading, setListLoading] = useState(true);
  const [board, setBoard] = useState<NightBoard | null>(null);
  const [boardRank, setBoardRank] = useState<number | undefined>();
  const [creating, setCreating] = useState(false);
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<BoardGame | null>(null);
  const [exitDirection, setExitDirection] = useState<
    "left" | "right" | "up" | "down" | "none"
  >("none");
  const sequence = useRef(0);
  const mutating = useRef(false);

  useEffect(() => {
    const controller = new AbortController();
    listNights(sessionId, listPage, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setPlans((old) =>
          listPage === 1
            ? result.items
            : [
                ...old.filter((n) => !result.items.some((r) => r.id === n.id)),
                ...result.items,
              ],
        );
        setTotalPlans(result.total);
        if (listPage === 1) {
          setNightId((old) => old || result.items[0]?.id || "");
          if (!result.total) setCreating(true);
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(message(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setListLoading(false);
      });
    return () => controller.abort();
  }, [sessionId, listPage, reload]);

  useEffect(() => {
    if (!nightId || creating) return;
    localStorage.setItem(storageKey, nightId);
    const controller = new AbortController();
    const current = ++sequence.current;
    getNight(nightId, sessionId, page, controller.signal, maxRank)
      .then((result) => {
        if (current === sequence.current && !controller.signal.aborted) {
          setBoard(result);
          setBoardRank(maxRank);
          setLoading(false);
          const last = Math.max(
            1,
            Math.ceil(result.selections.total / result.selections.pageSize),
          );
          if (result.selections.page > last) setPage(last);
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted && current === sequence.current) {
          setError(message(e));
          setLoading(false);
        }
      });
    return () => controller.abort();
  }, [
    nightId,
    creating,
    sessionId,
    storageKey,
    revision,
    page,
    reload,
    maxRank,
  ]);

  function accept(result: NightBoard) {
    setBoard(result);
    setBoardRank(maxRank);
    setPage(1);
    setPlans((old) =>
      old.map((n) => (n.id === result.plan.id ? result.plan : n)),
    );
  }
  async function mutate(
    work: () => Promise<void>,
    direction: "left" | "right" | "up" | "down" | "none" = "none",
  ) {
    if (mutating.current)
      throw new Error("Die letzte Auswahl wird noch gespeichert.");
    mutating.current = true;
    setExitDirection(direction);
    sequence.current++;
    setBusy(true);
    setError("");
    try {
      await work();
    } finally {
      mutating.current = false;
      setBusy(false);
      setLoading(false);
    }
  }
  async function saveSettings(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const settings: GameNightSettings = {
      name: String(form.get("name") ?? "").trim(),
      date: String(form.get("date") ?? "") || null,
      players: Number(form.get("players")),
      includeUnowned: creating ? false : (board?.plan.includeUnowned ?? false),
    };
    try {
      await mutate(async () => {
        if (creating || !nightId) {
          const plan = await createNight(sessionId, settings);
          setPlans((old) => [plan, ...old]);
          setTotalPlans((n) => n + 1);
          setBoard(null);
          setPage(1);
          setNightId(plan.id);
          setCreating(false);
          setLoading(true);
        } else accept(await updateNight(nightId, sessionId, settings, maxRank));
      });
    } catch (e) {
      setError(message(e));
    }
  }
  async function choose(game: BoardGame, action: SwipeAction) {
    if (!board) return;
    await mutate(
      async () =>
        accept(
          await chooseNightGame(
            board.plan.id,
            sessionId,
            game.id,
            action,
            maxRank,
          ),
        ),
      ({ 0: "left", 1: "right", 2: "up", 3: "down" } as const)[action],
    );
  }
  function chooseSafely(game: BoardGame, action: SwipeAction) {
    void choose(game, action).catch((e) => setError(message(e)));
  }
  const current = !creating && board?.plan.id === nightId ? board : null;
  const active = current && boardRank === maxRank ? current : null;
  const candidate = active?.candidates[0];
  const disabled = busy || loading;
  return (
    <section className="night-view" aria-labelledby="night-title">
      <div className="section-heading">
        <div>
          <h1 id="night-title">Spieleabend planen</h1>
        </div>
        {!creating && (
          <button
            disabled={disabled}
            onClick={() => {
              sequence.current++;
              setCreating(true);
              setError("");
            }}
          >
            Neuer Spieleabend
          </button>
        )}
      </div>
      <div className="night-toolbar">
        <RankFilter
          value={maxRank}
          disabled={busy}
          onChange={(rank) => {
            sequence.current++;
            setExitDirection("none");
            setPage(1);
            setLoading(!creating && !!nightId);
            setError("");
            onMaxRankChange(rank);
          }}
        />
        {plans.length > 0 && (
          <label>
            Gespeicherte Spieleabende
            <select
              aria-label="Spieleabend auswählen"
              value={creating ? "" : nightId}
              disabled={busy}
              onChange={(e) => {
                sequence.current++;
                setBoard(null);
                setPage(1);
                setNightId(e.target.value);
                setCreating(false);
                setLoading(true);
                setError("");
              }}
            >
              {creating && (
                <option value="" disabled>
                  Neuen Spieleabend anlegen
                </option>
              )}
              {!creating && nightId && !plans.some((n) => n.id === nightId) && (
                <option value={nightId}>
                  {board?.plan.name ?? "Gespeicherter Spieleabend"}
                </option>
              )}
              {plans.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.name}
                  {n.date
                    ? " · " +
                      new Date(n.date + "T12:00:00").toLocaleDateString("de-DE")
                    : ""}
                </option>
              ))}
            </select>
          </label>
        )}
        {plans.length < totalPlans && (
          <button
            disabled={listLoading || busy}
            onClick={() => {
              setListLoading(true);
              setListPage((p) => p + 1);
            }}
          >
            Weitere Spieleabende laden
          </button>
        )}
      </div>
      {(creating || current) && (
        <details
          className="night-settings-panel"
          key={creating ? "new-settings" : current?.plan.id}
          open={creating ? true : undefined}
        >
          <summary>
            {creating
              ? "Neuen Spieleabend anlegen"
              : current?.plan.players +
                " Personen" +
                (current?.plan.date
                  ? " · " +
                    new Date(
                      current.plan.date + "T12:00:00",
                    ).toLocaleDateString("de-DE")
                  : "") +
                " · Abend bearbeiten"}
          </summary>
          <form
            className="night-settings filter-panel"
            key={creating ? "new" : current?.plan.id}
            onSubmit={(e) => void saveSettings(e)}
          >
            <label>
              Name
              <input
                name="name"
                defaultValue={creating ? "Spieleabend" : current?.plan.name}
                required
                maxLength={120}
                disabled={disabled}
              />
            </label>
            <label>
              Datum <span className="sr-only">(optional)</span>
              <input
                type="date"
                name="date"
                defaultValue={creating ? "" : (current?.plan.date ?? "")}
                disabled={disabled}
              />
            </label>
            <label>
              Personen
              <input
                type="number"
                name="players"
                min={1}
                max={100}
                defaultValue={creating ? 3 : current?.plan.players}
                required
                disabled={disabled}
              />
            </label>
            <button className="primary" disabled={disabled}>
              {creating ? "Spieleabend anlegen" : "Änderungen speichern"}
            </button>
            {creating && nightId && (
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setCreating(false);
                  setLoading(true);
                }}
              >
                Abbrechen
              </button>
            )}
          </form>
        </details>
      )}
      {error && (
        <p className="notice error" role="alert">
          {error}{" "}
          <button
            onClick={() => {
              setError("");
              setReload((r) => r + 1);
            }}
          >
            Erneut laden
          </button>
        </p>
      )}
      {!active && !creating && (
        <p role="status">
          {listLoading || loading || nightId
            ? "Spieleabend wird geladen …"
            : "Wähle einen Spieleabend oder lege einen neuen an."}
        </p>
      )}
      {active && (
        <>
          <div className="night-pool">
            <span className="pool-badge">
              {active.plan.includeUnowned
                ? "Besitz + Favoriten"
                : "Nur Spiele im Besitz"}
            </span>
            <button
              aria-pressed={active.plan.includeUnowned}
              disabled={disabled}
              onClick={() => {
                void mutate(async () =>
                  accept(
                    await updateNight(
                      nightId,
                      sessionId,
                      {
                        ...active.plan,
                        includeUnowned: !active.plan.includeUnowned,
                      },
                      maxRank,
                    ),
                  ),
                ).catch((e) => setError(message(e)));
              }}
            >
              Auch Spiele nicht im Besitz anzeigen
            </button>
          </div>
          <div className="night-layout">
            <div className="swipe-stage">
              <p className="night-source" aria-live="polite">
                {busy
                  ? "Auswahl wird gespeichert …"
                  : candidate
                    ? candidate.isOwned
                      ? "In deinem Besitz"
                      : "Favorit · Zum Ausleihen"
                    : "Alle passenden Spiele durchgesehen"}
              </p>
              <div className="swipe-card-wrap">
                <AnimatePresence mode="wait" custom={exitDirection}>
                  {candidate ? (
                    <GameCard
                      key={candidate.id}
                      game={candidate}
                      selectedPlayers={active.plan.players}
                      exitDirection={exitDirection}
                      disabled={disabled}
                      planning
                      onSwipe={(direction) =>
                        chooseSafely(candidate, direction === "left" ? 0 : 1)
                      }
                      onDetails={() => setSelected(candidate)}
                    />
                  ) : (
                    <div className="empty-discovery" key="empty">
                      <span>♟</span>
                      <h2>Platz für deinen Spieleabend</h2>
                      <p>
                        {active.selections.total
                          ? "Deine vorgemerkten Spiele stehen auf der Abend-Liste."
                          : "Füge Spiele zu deinem Besitz hinzu, passe Ranggrenze oder Personenzahl an oder beziehe deine Favoriten zum Ausleihen ein."}
                      </p>
                      <button onClick={onOpenCollection}>
                        Spiele im Besitz ansehen
                      </button>
                    </div>
                  )}
                </AnimatePresence>
              </div>
              <div className="swipe-actions">
                {choices.map((c) => (
                  <button
                    className={"swipe-action action-" + c.action}
                    key={c.action}
                    disabled={!candidate || disabled}
                    onClick={() =>
                      candidate && chooseSafely(candidate, c.action)
                    }
                  >
                    <span aria-hidden="true">{c.icon}</span>
                    {c.label}
                  </button>
                ))}
              </div>
            </div>
            <aside
              className="night-shortlist"
              aria-labelledby="shortlist-title"
            >
              <div className="shortlist-heading">
                <h2 id="shortlist-title">Für diesen Abend</h2>
                <span className="count-pill">{active.selections.total}</span>
              </div>
              {!active.selections.total && (
                <p className="muted">
                  Wische nach rechts, um ein Spiel vorzumerken.
                </p>
              )}
              {active.selections.items.map(({ game, action }) => (
                <article className="shortlist-card" key={game.id}>
                  <button
                    className="shortlist-open"
                    onClick={() => setSelected(game)}
                  >
                    {game.thumbnail && (
                      <img src={game.thumbnail} alt="" loading="lazy" />
                    )}
                    <span>
                      <strong>{game.name}</strong>
                      <DesignerLine designers={game.designers} />
                      <PlayerRecommendation
                        game={game}
                        selectedPlayers={active.plan.players}
                      />
                      <small>
                        {action === 2 ? "★ Top-Wahl · " : ""}
                        {game.isOwned ? "Im Besitz" : "Zum Ausleihen"}
                      </small>
                    </span>
                  </button>
                  <MatchQualityBadge quality={game.matchQuality} compact />
                  {((game.minPlayers ?? 0) > active.plan.players ||
                    (game.maxPlayers ?? Infinity) < active.plan.players) && (
                    <p className="muted">Personenzahl passt nicht mehr.</p>
                  )}
                  <button
                    className="text-button"
                    disabled={disabled}
                    aria-label={game.name + " vom Spieleabend entfernen"}
                    onClick={() => chooseSafely(game, 3)}
                  >
                    Von der Liste nehmen
                  </button>
                </article>
              ))}
              {active.selections.total > active.selections.pageSize && (
                <nav className="pagination" aria-label="Abend-Liste">
                  <button
                    disabled={disabled || page <= 1}
                    onClick={() => {
                      setLoading(true);
                      setPage((p) => p - 1);
                    }}
                  >
                    Zurück
                  </button>
                  <span>Seite {page}</span>
                  <button
                    disabled={
                      disabled ||
                      page * active.selections.pageSize >=
                        active.selections.total
                    }
                    onClick={() => {
                      setLoading(true);
                      setPage((p) => p + 1);
                    }}
                  >
                    Weiter
                  </button>
                </nav>
              )}
            </aside>
          </div>
        </>
      )}
      {selected && (
        <GameDetails
          key={selected.id}
          game={selected}
          selectedPlayers={current?.plan.players}
          sessionId={sessionId}
          planning
          onClose={() => setSelected(null)}
          onRate={choose}
          onOwnership={onOwnership}
        />
      )}
    </section>
  );
}

import { useEffect, useState } from "react";
import { getStatistics } from "../api/nextTurnApi";
import type { DistributionItem, PlayerStatistics } from "../api/nextTurnApi";

function Bars({
  title,
  items,
  limit = 10,
}: {
  title: string;
  items: DistributionItem[];
  limit?: number;
}) {
  return (
    <section className="chart-panel">
      <h2>{title}</h2>
      {items.length === 0 ? (
        <p className="muted">Noch keine Daten vorhanden.</p>
      ) : (
        items.slice(0, limit).map((item) => (
          <div className="stat-bar" key={item.name}>
            <div>
              <span>{item.name}</span>
              <strong>
                {item.percentage.toLocaleString("de-DE")}%{" "}
                <small>({item.count})</small>
              </strong>
            </div>
            <div className="bar-track">
              <div style={{ width: item.percentage + "%" }} />
            </div>
          </div>
        ))
      )}
    </section>
  );
}
export default function Dashboard({
  sessionId,
  revision,
}: {
  sessionId: string;
  revision: number;
}) {
  const [data, setData] = useState<PlayerStatistics | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    getStatistics(sessionId, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setData(result);
        setError("");
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(e.message);
      });
    return () => controller.abort();
  }, [sessionId, revision]);
  if (error)
    return (
      <p className="notice error" role="alert">
        {error}
      </p>
    );
  if (!data) return <p role="status">Dein Profil wird geladen …</p>;
  const number = (value: number | null | undefined, digits = 1) =>
    value == null
      ? "–"
      : value.toLocaleString("de-DE", { maximumFractionDigits: digits });
  const players = data.preferredPlayers;
  const duration = data.playTimePreference;
  const counts = players?.counts ?? [];
  const playerValue =
    counts.length === 0
      ? "Noch offen"
      : counts.length <= 3
        ? counts.join(" / ")
        : "Mehrere";
  const playerDetail = players?.knownGames
    ? (counts.length > 1 ? "Je " : "") +
      number(players.percentage) +
      " % von " +
      players.knownGames +
      " Favoriten mit Best-Angaben" +
      (players.isProvisional ? " · vorläufig" : "")
    : "Community-Bestangaben fehlen";
  const durationLabels = {
    short: "Eher schnell",
    long: "Eher lang",
    variable: "Variabel",
    mixed: "Gemischt",
    unknown: "Noch offen",
  };
  const durationValue = durationLabels[duration?.tendency ?? "unknown"];
  const durationDetail = !duration?.knownGames
    ? "Zeitangaben fehlen"
    : (duration.tendency === "short"
        ? number(duration.shortPercentage) + " % bis 60 Min."
        : duration.tendency === "long"
          ? number(duration.longPercentage) + " % über 60 Min."
          : duration.tendency === "variable"
            ? number(duration.variablePercentage) +
              " % mit variabler Zeitspanne"
            : number(duration.shortPercentage) +
              " % schnell · " +
              number(duration.longPercentage) +
              " % lang" +
              (duration.variableGames
                ? " · " + number(duration.variablePercentage) + " % variabel"
                : "")) + (duration.isProvisional ? " · vorläufig" : "");
  return (
    <section aria-labelledby="profile-title">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Dein Geschmack in Zahlen</p>
          <h1 id="profile-title">Dein Spielerprofil</h1>
        </div>
      </div>
      <div className="profile-summary">
        <span className="profile-symbol">✦</span>
        <div>
          <h2>So spielst du gerne</h2>
          <p>{data.profile}</p>
        </div>
      </div>
      <div className="metric-grid">
        {[
          { label: "Favoriten", value: data.favorites },
          { label: "Likes", value: data.likes },
          { label: "SuperLikes", value: data.superLikes },
          {
            label: "Bevorzugte Spieleranzahl",
            value: playerValue,
            detail: playerDetail,
          },
          { label: "Ø Bewertung", value: number(data.averageRating) },
          {
            label: "Spieldauer-Tendenz",
            value: durationValue,
            detail: durationDetail,
          },
          {
            label: "Ø Komplexität",
            value: number(data.averageComplexity) + "/5",
          },
        ].map(({ label, value, detail }) => (
          <div className="metric" key={label}>
            <span>{label}</span>
            <strong>{value}</strong>
            {detail && <small>{detail}</small>}
          </div>
        ))}
      </div>
      <p className="chart-note">
        {data.percentageDenominator} Beste Besetzungen stammen aus
        Community-Abstimmungen. Dauertendenzen berücksichtigen bekannte
        Zeitspannen.
      </p>
      <div className="chart-grid">
        <Bars title="Deine Genres & Themen" items={data.categories} />
        <Bars title="Beliebte Mechaniken" items={data.mechanics} />
        <Bars title="Beliebte Spieldesigner" items={data.designers ?? []} />
        <Bars title="Komplexitätsprofil" items={data.complexity} />
        <Bars title="Spieldauer deiner Favoriten" items={data.playTimes} />
        <Bars
          title="Community: beste Spieleranzahlen"
          items={data.bestPlayerCounts ?? []}
        />
        <Bars
          title="Von BGG empfohlene Spieleranzahlen"
          items={data.recommendedPlayerCounts}
        />
      </div>
      {Object.values(data.missingData).some((n) => n > 0) && (
        <section className="notice">
          <h2>Hier fehlen noch Angaben</h2>
          <p>
            {Object.entries(data.missingData)
              .filter(([, n]) => n > 0)
              .map(([name, count]) => name + ": " + count + " Spiel(e)")
              .join(" · ")}
          </p>
        </section>
      )}
    </section>
  );
}

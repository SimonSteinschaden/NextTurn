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
          ["Favoriten", data.favorites],
          ["Likes", data.likes],
          ["SuperLikes", data.superLikes],
          [
            "Ø BGG-Ranking",
            data.averageRank == null ? "–" : "#" + number(data.averageRank, 0),
          ],
          ["Ø Bewertung", number(data.averageRating)],
          ["Ø Spieldauer", number(data.averagePlayTime, 0) + " Min."],
          ["Ø Komplexität", number(data.averageComplexity) + "/5"],
        ].map(([label, value]) => (
          <div className="metric" key={label}>
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
      <p className="chart-note">
        {data.percentageDenominator} Durchschnitte nutzen nur bekannte Werte.
        Spielerzahlen zeigen unterstützte Bereiche bis 100 Personen.
      </p>
      <div className="chart-grid">
        <Bars title="Deine Genres & Themen" items={data.categories} />
        <Bars title="Beliebte Mechaniken" items={data.mechanics} />
        <Bars title="Komplexitätsprofil" items={data.complexity} />
        <Bars title="Typische Spieldauer" items={data.playTimes} />
        <Bars title="Unterstützte Spieleranzahlen" items={data.playerCounts} />
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

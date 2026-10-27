# NextTurn

Brettspiele entdecken, im lokalen Katalog suchen, favorisieren und den eigenen Spielgeschmack kennenlernen.

React / TypeScript / Vite / Tailwind / Framer Motion · ASP.NET Core 10 · EF Core / PostgreSQL 16

## Lokal starten

Voraussetzungen: .NET SDK 10, Node.js ab 22.12, Docker Desktop und ein genehmigter BGG-Anwendungstoken.

1. Die lokale Datenbank konfigurieren und starten:

```powershell
Copy-Item .env.example .env
# In .env ein eigenes lokales PostgreSQL-Passwort setzen.
docker compose up -d
```

Bei einem bereits vorhandenen alten Volume bleibt die bisherige Datenbank erhalten. Falls die neue Datenbank noch fehlt, einmal anlegen:

```powershell
docker exec nextturn_postgres createdb -U nextturn nextturn
```

Die neuen Container nutzen das bestehende Volume weiter. Ein geändertes POSTGRES_PASSWORD setzt bei einem bereits initialisierten Volume nicht automatisch das Datenbankpasswort zurück. Verwende die bestehenden Zugangsdaten oder ändere sie ausdrücklich in PostgreSQL.

2. Zugangsdaten ausschließlich lokal hinterlegen:

```powershell
dotnet user-secrets set "ConnectionStrings:NextTurn" "Host=localhost;Port=5432;Database=nextturn;Username=nextturn;Password=DEIN_LOKALES_PASSWORT" --project backend/NextTurn.Api
dotnet user-secrets set "BggApi:BearerToken" "DEIN_BGG_TOKEN" --project backend/NextTurn.Api
dotnet run --project backend/NextTurn.Api --launch-profile http
```

Die .NET User Secrets werden im Development-Profil gelesen. Für andere Umgebungen stehen ConnectionStrings__NextTurn und BggApi__BearerToken als Umgebungsvariablen zur Verfügung. Innerhalb des Docker-Netzes ist der Datenbankhost nextturn_db; beim lokalen dotnet run ist es localhost. Datenbankname und Benutzer: nextturn.

API: http://localhost:5080 · Swagger: http://localhost:5080/swagger

3. Frontend in einem zweiten Terminal:

```powershell
cd frontend
npm ci
npm run dev
```

App: http://localhost:5173. Eine andere API-Adresse kann über eine lokale frontend/.env.local mit VITE_API_BASE_URL konfiguriert werden. Das Beispiel liegt in frontend/.env.example. Niemals den BGG-Token als VITE-Variable hinterlegen.

## Katalog und Import

Beim API-Start werden versionierte EF-Migrationen angewendet und die allgemeinen Ränge 1–10.000 aus data/boardgames_ranks.csv per BggId abgeglichen. Die CSV wird beim Build/Publish mitkopiert; der Pfad wird relativ zum API-Ausgabeordner aufgelöst.

Danach ergänzt ein Hintergrundimport die BGG-Details: maximal 20 IDs pro Anfrage, mindestens fünf Sekunden Abstand, Retry-After und exponentieller Backoff. Erfolgreiche Batches werden sofort gespeichert. Ein Neustart setzt bei fehlenden oder älter als 30 Tage gewordenen Details fort. Fehlgeschlagene/fehlende IDs werden protokolliert und bleiben für den nächsten Lauf offen. Es werden keine Angaben erfunden.

Der erste vollständige Detailimport benötigt selbst ohne Wartezeiten bei BGG mindestens rund 42 Minuten. Suche und Favoriten funktionieren bereits mit den CSV-Grunddaten. Die Entdecken-Queue verwendet nur Spiele mit importierten Details und bekannter passender Spieleranzahl.

Der aktuelle Importstand ist unter /api/games/status sichtbar. Import:Enabled=false deaktiviert den Hintergrundimport. Import:RefreshDays steuert die Aktualisierungsfrist. Ein aktualisierter CSV-Snapshot wird beim nächsten Start abgeglichen; alte Favoriten bleiben erhalten, auch wenn ein Spiel nicht mehr in den aktuellen Top 10.000 liegt.

## Bewertungen und Empfehlungen

- Like und SuperLike erscheinen in Meine Spiele.
- Dislike und Skip sind keine Favoriten.
- Jede Session besitzt genau einen aktuellen Zustand je Spiel. Identische Wiederholungen sind idempotent; Änderungen erzeugen ein neues Historienereignis und ersetzen den Lernzustand.
- Favorit entfernen setzt den aktuellen Zustand auf Skip. Das Spiel bleibt damit aus der Entdecken-Queue ausgeschlossen.
- Alle bereits bewerteten oder übersprungenen Spiele werden serverseitig ausgeschlossen, auch nach Browser-, API- und Datenbankneustart.
- Lernen berücksichtigt Kategorien, Mechaniken, Komplexität, Spieldauer und Spielerzahl. SuperLikes gewichten stärker, Dislikes negativ; positive Komplexitäts-/Zeitpräferenzen werden nur aus Favoriten gebildet.
- Standardmäßig sind 80 % der Queue personalisiert und 20 % Entdeckungen. Für den Entdeckungsanteil werden möglichst unbekannte Genres/Mechaniken gewählt. Das Verhältnis ist über Recommendations:ExplorationRatio (0,1–0,5) konfigurierbar, sofern genügend unbewertete passende Spiele verfügbar sind.
- Pro Kandidatenabfrage werden standardmäßig höchstens 300 Spiele geladen. Lernabfragen projizieren nur benötigte Merkmale statt kompletter Beschreibungen/Cover. Die Recommendation Engine verwendet Repository-Interfaces und ist unabhängig von EF Core.
- Nach jeder bestätigten Bewertung wird die Queue neu gewichtet; überholte Antworten werden verworfen.

## Statistik

Das Dashboard verwendet ausschließlich aktuelle Likes und SuperLikes, jedes Spiel einmal. Der Nenner für Kategorien/Mechaniken/Verteilungen sind alle aktuellen Favoriten, daher dürfen sich Kategorienprozente auf mehr als 100 % summieren. Durchschnittswerte verwenden bekannte Werte; fehlende Angaben werden ausgewiesen. Die mittlere Spieldauer ist die Mitte zwischen MinPlayTime und MaxPlayTime, ersatzweise PlayingTime. Unterstützte Spieleranzahlen und von BGG empfohlene Spieleranzahlen werden getrennt gezeigt. Bei weniger als fünf Favoriten gibt es nur einen Hinweis auf erste Tendenzen.

## Tests

```powershell
dotnet test NextTurn.slnx
cd frontend
npm run build
npm run lint
npx playwright install chromium
npm run test:e2e
```

Die PostgreSQL-Tests erzeugen und entfernen ausschließlich eine eigene zufällig benannte nextturn_test_*-Datenbank. Sie lesen lokal ConnectionStrings:NextTurn oder alternativ NEXTTURN_TEST_CONNECTION; der Testbenutzer muss Testdatenbanken anlegen dürfen. Ohne PostgreSQL:

```powershell
dotnet test NextTurn.slnx --filter "Category!=Integration"
```

Die Browser-Regressionstests verwenden ausdrücklich synthetische API-Fixtures. Zusätzlich wird die echte lokale API mit PostgreSQL und dem fortlaufenden Import geprüft. Eine Testbewertung verändert keine echte Browsersession.

## API-Überblick

- GET /api/games — lokale Suche; q, page, pageSize, sort (rank/rating/name/year), players, maxMinutes, minComplexity, maxComplexity, category, mechanic
- GET /api/games/facets — lokale Kategorien/Mechaniken
- GET /api/games/{id}?sessionId=... — lokale Details und aktueller Bewertungszustand
- GET /api/games/recommendations?sessionId=...&players=... — personalisierte Queue
- GET /api/games/status — Importfortschritt und Fehlerhinweis
- POST /api/swipes — zentrale Bewertungslogik für Swipe, Suche und Details
- GET /api/player/favorites?sessionId=... — gefilterte/paginierte Favoriten
- DELETE /api/player/favorites/{id}?sessionId=... — Favorit zu Skip ändern
- GET /api/player/statistics?sessionId=... — Kennzahlen und Verteilungen

Die alte /api/bgg/{id}-Route bleibt als explizite BGG-Abfrage erhalten. Suche, Details, Favoriten und Empfehlungen benötigen sie nicht.

## Architektur und Grenzen

Controller → Service → Repository-Interface → EF-Repository → PostgreSQL. Entities liegen separat unter Entities, DTOs weiter unter Models. Migrationen liegen unter Data/Migrations. Redis ist vorbereitet und wird derzeit nicht benötigt; personalisierte Ergebnisse werden nicht über einen potenziell veralteten Cache ausgeliefert. Der bestehende BGG-MemoryCache bleibt für explizite Einzelabfragen erhalten.

Session-IDs bleiben in localStorage. Das ist noch kein Benutzerkonto; ein anderes Browserprofil hat andere Favoriten. Eine neue Session löscht keine Daten der alten Session, wechselt aber das sichtbare Profil. Die Entity PlayerSession besitzt bereits ein optionales UserId-Feld für eine spätere Zuordnung.

BGG-Daten und Cover gehören ihren jeweiligen Rechteinhabern. Attribution und Verlinkung erfolgen im globalen Footer. Das unveränderte Powered-by-BGG-Logo liegt unter frontend/public/powered-by-bgg.png (Bildquelle: https://dm1i7q1ruvbhg.cloudfront.net/assets/powered_by_bgg-9f2993c7fad826d5407c35b5c8117b08d2a6cdf5beb1f0c02d06a7dc7f5cdd7f.png). Lizenz/Registrierung: https://boardgamegeek.com/using_the_xml_api und https://boardgamegeek.com/xmlapi/termsofuse.

Bestandsaufnahme, Risiken und tatsächliche Phasenergebnisse: docs/Entwicklungsauftrag.md.

## Ranking-Gewichtung der nächsten Vorschläge

Die globale Ranking-Regel und der Entdeckungsanteil sind zwei unabhängige Entscheidungen:

- Pro Vorschlag wird mit 80 % Wahrscheinlichkeit die Gruppe BGG-Rang 1–500 und mit 20 % die Gruppe 501–10.000 gewählt.
- Innerhalb der gewählten Gruppe wird ohne Wiederholung gewichtet gezogen. Bei ansonsten gleichen Merkmalen sinkt die Chance mit schlechterem Rang; Standardgewicht für den Rang: 1 / Rang^0,65.
- Der bestehende Profilscore erhöht bzw. senkt dieses Gewicht. Bevorzugte Kategorien und Mechaniken bleiben dadurch wahrscheinlicher; innerhalb derselben Vorlieben haben besser gerankte Spiele höhere Chancen. Ranking und Profilsignal sind getrennt: Auch eine sehr starke Genrevorliebe kann die globale Top-500-Wahrscheinlichkeit nicht aushebeln.
- Weiterhin sind standardmäßig 20 % der Queue-Plätze zum Entdecken weniger vertrauter Genres/Mechaniken reserviert. Das bedeutet weder 100 % bekannte Genres noch 100 % Top-500-Spiele.
- Die Kandidatenabfragen berücksichtigen beide Ranggruppen und bevorzugen bekannte Merkmale für personalisierte Plätze bzw. weniger bekannte Merkmale für Entdeckungen. Sie laden weiterhin höchstens das konfigurierte Kandidatenlimit je Pool.
- Wenn eine Ranggruppe durch Spielerzahlfilter, fehlende Details oder bereits bewertete Spiele nicht mehr verfügbar ist, wird auf die verbleibende Gruppe ausgewichen. Bereits bewertete Spiele werden dabei nie wieder aufgenommen.

Die 80 % sind eine Wahrscheinlichkeit pro Ziehung und ein langfristiger Erwartungswert, keine starre Quote von 16 Spielen je 20er-Queue. Separate offizielle BGG-Genre-/Mechanikranglisten sind nicht gespeichert; innerhalb dieser Gruppen wird der vorhandene allgemeine BGG-Rang verwendet, ohne neue Rangdaten zu erfinden.

Konfiguration unter Recommendations: TopRankCutoff=500, TopRankProbability=0.8, RankDecayExponent=0.65. ExplorationRatio=0.2 steuert unabhängig davon den Entdeckungsanteil. RecommendationScore bleibt der Profilscore; die Ranggruppenwahl und der Rangfaktor wirken zusätzlich bei der Zufallsauswahl.

## Persönliche Matchqualität

Die Karten zeigen einen auf 0–100 normierten Ähnlichkeitsindex zu den aktuellen Favoriten. Es ist keine kalibrierte Wahrscheinlichkeit für einen zukünftigen Like. BGG-Rang, Community-Bewertung und die zufällige Auswahlchance erhöhen diesen Wert nicht.

Für jedes andere aktuelle Lieblingsspiel werden verglichen:

| Merkmal        | Festgelegtes Gewicht | Ähnlichkeitsmaß                                                                  |
| -------------- | -------------------: | -------------------------------------------------------------------------------- |
| Kategorien     |                 35 % | Cosinusähnlichkeit der eindeutigen Kategorienmengen                              |
| Mechaniken     |                 35 % | Cosinusähnlichkeit der eindeutigen Mechanikenmengen                              |
| Komplexität    |                 15 % | 1 minus absoluter Weight-Abstand geteilt durch 4 (Skala 1–5)                     |
| Spieldauer     |                 10 % | Verhältnis der kleineren zur größeren mittleren Dauer                            |
| Spielerbereich |                  5 % | Überschneidung der unterstützten Spieleranzahlen geteilt durch deren Vereinigung |

Je Vergleich werden ausschließlich gemeinsam bekannte Merkmale gewertet und deren Gewichte neu normiert. Die paarweisen Ähnlichkeiten werden gemittelt: Likes zählen einfach, SuperLikes dreifach. Kategorien/Mechaniken sind eindeutige Mengen; ein Spiel wird nur einmal gezählt. Dislikes, Skips und ersetzte Bewertungen sind keine positiven Referenzen. Ein Favorit wird beim eigenen Matchwert aus der Referenzmenge entfernt.

Ein Vergleich braucht mindestens 30 % der möglichen Merkmalsgewichte. Fehlen geeignete Referenzen, bleibt der Prozentwert null und die Oberfläche zeigt Match offen. Ein Wert ist als vorläufig markiert, wenn weniger als fünf andere Favoriten verglichen wurden, die mittlere Merkmalsabdeckung unter 70 % liegt oder Favoriten mangels Daten nicht vergleichbar waren. Das sind transparente Mindestregeln für die Anzeige, keine statistischen Konfidenzgrenzen. Die Prozentwerte werden ohne Nachkommastellen gezeigt.

Die Matchqualität wird pro Anfrage frisch berechnet und in Empfehlungen, Suche, Favoriten und Details gleich ausgegeben. Empfehlungen verwenden die bereits für das Profil geladenen Interaktionen; es gibt keine zusätzliche BGG-Abfrage oder Datenbankabfrage je Karte. API-Feld: matchQuality mit percentage, comparedFavorites, availableFavorites, featureCoveragePercentage und isProvisional.

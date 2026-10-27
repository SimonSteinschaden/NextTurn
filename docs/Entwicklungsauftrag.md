# Bestandsaufnahme und Umsetzung

Stand: 27.10.2026. Anforderung: lokaler Top-10.000-Katalog, Suche, persistente Favoriten und Interaktionen, Spielerstatistik, personalisierte Empfehlungen mit Entdeckungsanteil.

## Phase 1: Bestandsaufnahme

| Kategorie   | Dateien                                              | Befund / notwendige Änderung                                                                                                                    |
| ----------- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Vorhanden   | Services/BggService.cs                               | Bearer-Authentifizierung, XML-Parsing, 24h MemoryCache; Metadaten unvollständig.                                                                |
| Vorhanden   | Repositories/CsvGameRepository.cs                    | Allgemeines Ranking aus CSV, portabler Dateipfad. CSV: 176.894 Zeilen, exakt 10.000 eindeutige IDs auf Rang 1–10.000, keine Erweiterungen dort. |
| Vorhanden   | frontend/src/components/GameCard.tsx                 | Framer-Motion-Swipe mit Links/Rechts, Like/Dislike/SuperLike/Skip über Buttons.                                                                 |
| Teilweise   | Services/UserPreferenceService.cs                    | Lerngewichte +1/+3/-1/0; Dictionary verliert Profile beim Neustart, wiederholte Bewertungen zählen mehrfach.                                    |
| Teilweise   | Services/RecommendationService.cs                    | Spielerzahl-, Kategorie-, Mechanik- und Komplexitätsscoring, nur erste 20 CSV-Spiele, externe API pro Kandidat, verschluckte Fehler.            |
| Teilweise   | frontend/src/App.tsx                                 | Session in localStorage, Queue; veraltete State-Werte beim Nachladen und überlappende Requests möglich.                                         |
| Fehlt       | Data/, Entities/, Migrations/, Repository-Interfaces | PostgreSQL läuft, öffentliche Anwendungstabellen fehlen.                                                                                        |
| Fehlt       | Katalog, Favoriten, Statistik                        | Keine lokale Suche, Persistenz, Favoritenansicht oder Dashboard.                                                                                |
| Refactoring | Program, Services, Controllers, App                  | Scoped Services/Repositories, einheitliche Interaktionen, lokale Empfehlungen, Navigation und Ladezustände.                                     |

BGG-Daten bisher: ID, Name, Jahr, Cover/Thumbnail, Min/Max-Spieler, Spielzeit, Rating, Weight, Kategorien, Mechaniken, Spielerzahlumfrage. Ranking kommt aus CSV. Beschreibung, BayesAverage, UsersRated, Spielzeitbereich, Alter, Spieltyp und bestmögliche Spielerzahl ergänzen. Ranking, Rating, Kategorien, Mechaniken und Typ getrennt halten.

## Implementierungsplan / Risiken

1. EF Core + Npgsql, Entities separat von DTOs, Migrationen, PostgreSQL-Repositories. Bestehendes Docker-Volume und alte nextturn_db erhalten; neue Anwendungsdatenbank nextturn.
2. CSV-Grunddaten in 10.000 eindeutigen Datensätzen; Detailimport in 20er-Batches, mindestens 5s Abstand, Retry-After/Backoff, persistente Fortschrittsmarkierungen, erneute Ausführung ergänzt fehlende/veraltete Details.
3. Lokale Suche mit ILIKE, Trigramm- und Array-GIN-Indizes, Pagination/Filter/Sortierung; Likes aus Suche/Swipe über denselben Service, aktueller Zustand plus Ereignishistorie.
4. Favoriten und regelbasiertes Statistik-Dashboard: nur aktuelle Likes/SuperLikes, ein Spiel einmal, Prozentnenner alle Favoriten, fehlende Daten separat.
5. Kandidaten im Repository begrenzen, positive Favoritenstatistik und negative Bewertungen berücksichtigen. 80/20 personalisiert/Entdeckung als konfigurierbarer Ausgangswert; bewertete Spiele serverseitig ausschließen. Skip ebenfalls nicht wieder vorschlagen. Ein entfernter Favorit wird zu Skip, Historie bleibt erhalten.
6. Unit-/PostgreSQL-Integrationstests, Migration, Neustarttest, Frontend-Build/Lint; Ergebnisse unten dokumentieren.

Risiken: BGG-Ausfälle/fehlende Metadaten bleiben sichtbar; 10.000 Detailimporte benötigen wegen Rate-Limits Zeit. Kein erfundener Ersatz. Alte InMemory-Swipes sind mangels Speicherung nicht rekonstruierbar. Sessions sind an dieses Browserprofil gebunden und ersetzen keine Benutzeranmeldung. Geänderte Bewertungen ersetzen den Lernzustand, Historie bleibt erhalten. Redis zunächst nicht einführen: PostgreSQL-Indizes und begrenzte Kandidaten reichen; keine veralteten personalisierten Cache-Einträge.

## Geprüfte Primärquellen

- https://boardgamegeek.com/using_the_xml_api — Registrierung, Backend-Bearer-Token, Cache, Attribution; Prüfung 27.10.2026.
- https://boardgamegeek.com/wiki/page/BGG_XML_API2 — CSV-Allgemeinranking, maximal 20 IDs je thing-Abfrage, 5s Abstand als dokumentierte Empfehlung, keine garantierte feste Lizenzquote.
- https://boardgamegeek.com/xmlapi/termsofuse — Attribution, verlinktes Powered-by-BGG-Logo für öffentliche Apps; keine externe KI zum Profil nötig.
- https://www.npgsql.org/efcore/mapping/array.html — text[]/integer[], filterbar und GIN-indizierbar.
- https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing — versionierte EF-Migrationen.

Die Anwendungsregistrierung/Lizenz gilt weiterhin für den Betreiber. Zugangsdaten ausschließlich über .NET User Secrets / Umgebungsvariablen, nicht im Frontend und nicht in Git.

## Tatsächlich umgesetzt und geprüft

### Phase 2 – Datenbank und Import

- EF Core/Npgsql, getrennte Entities und Repository-Interfaces, versionierte Migration InitialCatalog.
- Bestehendes Docker-Volume und alte nextturn_db erhalten; neue Anwendungsdatenbank nextturn.
- CSV-Abdeckung geprüft: 176.894 Zeilen, exakt 10.000 eindeutige BggIds mit allgemeinen Rängen 1–10.000. Seed/Upsert zweimal gegen PostgreSQL getestet, keine Duplikate.
- Hintergrundimport: 20er-Batches, mindestens 5 Sekunden Abstand, Retry-After/Backoff, persistente Fehler- und Erfolgsmarkierungen; Abbruch und Wiederaufnahme tatsächlich getestet.
- BGG-Zugriff ausschließlich über Backend und den festen HTTPS-Host boardgamegeek.com; Redirects deaktiviert. Autorisierungsfehler geben weder Token noch Upstream-Antwortkörper weiter.
- Nullwert-Platzhalter bei Spielerzahl, Zeit und Alter werden als fehlend behandelt; negative historische Erscheinungsjahre bleiben erhalten. UsersRated=0 bleibt eine gültige Anzahl.

### Phase 3 – Suche und Favoriten

- Lokale PostgreSQL-Suche mit ILIKE, Escaping von Wildcards, Pagination, Sortierung und Filtern; passende Trigramm- und Array-GIN-Indizes angelegt und geprüft.
- Like/SuperLike aus Swipe, Suche und Details laufen durch denselben Interaction-Service. Identische Wiederholungen sowie sechs parallele Likes erzeugen nur einen aktuellen Zustand und ein Ereignis.
- Favorit entfernen setzt Skip. Geänderte Bewertungen ersetzen den aktuellen Zustand und bleiben in einer getrennten Historie nachvollziehbar.
- Favorit, Statistik und Ausschluss bewerteter Spiele haben einen tatsächlichen Docker- und API-Neustart überstanden. Die eigens erzeugte QA-Session wurde danach entfernt; echte Sessions wurden nicht verändert.

### Phase 4 – Spieler-Dashboard

- Kennzahlen, Kategorien/Mechaniken, Weight-Verteilung, Zeitverteilung sowie getrennte unterstützte und BGG-empfohlene Spieleranzahlen.
- Aktuelle Favoriten als eindeutige Grundgesamtheit, bekannte Werte als Grundlage von Durchschnitten, fehlende Daten explizit ausgewiesen.
- Regelbasierte Profilbeschreibung ohne externe KI. Bei weniger als fünf Favoriten nur vorsichtiger Hinweis auf erste Tendenzen.
- Mobile Navigation mit Entdecken, Suche, Meine Spiele und Profil; Detaildialog und gemeinsame Karten. Bestehende Framer-Motion-Swipe-Interaktion erhalten und dynamische Exit-Richtung korrigiert.

### Phase 5 – Empfehlungen und Performance

- Dauerhafter Ausschluss aller bereits bewerteten/übersprungenen Spiele serverseitig; zusätzliche Frontend-Abwehr gegen überholte Antworten.
- 80/20 personalisiert/Entdeckung als Ausgangswert. Unbekannte Kategorien/Mechaniken werden für die reservierten Entdeckungsplätze bevorzugt.
- Lernmerkmale: Kategorien, Mechaniken, Komplexität, Spielerzahl, Spieldauer; Like +1, SuperLike +3, Dislike -1, Skip 0.
- Begrenzte Kandidatenabfragen und Projektionen; lange Beschreibungen nur für explizite Details. Keine 10.000 vollständigen Spiele je Swipe und kein BGG-Aufruf aus Suche/Empfehlungen.
- Reale Empfehlungsabfrage: 20 eindeutige Spiele, davon vier Entdeckungen. Lokale Stichprobe: 238 ms erste Anfrage, danach 23–34 ms; keine allgemeine Lastgarantie.
- Redis nicht zusätzlich eingeführt: bestehende PostgreSQL-Indizes und begrenzte Abfragen reichen für den gemessenen lokalen Betrieb. Kein personalisierter Cache, der nach Likes veralten könnte.

### Phase 6 – Qualität und Abschluss

- 19 Backend-Tests erfolgreich, einschließlich PostgreSQL-Integration, Migrationen, 10.000er-Upserts, Suche, Persistenz, parallelen Likes, Statistik, Nullwerten, BGG-Token-Schutz und Retry.
- Sechs Browser-Tests erfolgreich: Desktop und Mobil, Swipe, Like aus Suche, Favoriten nach Reload, Profil, Detaildialog und fehlgeschlagenes Speichern.
- Frontend-Produktionsbuild und ESLint erfolgreich; reale Desktop-/Mobilansichten mit BGG-Covern visuell geprüft, keine JavaScript-Fehler und kein horizontaler Überlauf auf 390 px Breite.
- EF-Prüfung: keine Modelländerungen ohne Migration. Swagger antwortet mit HTTP 200.
- npm-Audit und NuGet-Vulnerabilitätsprüfung: keine bekannten Schwachstellen zum Prüfzeitpunkt. Microsoft.OpenApi auf korrigierte 2.7.5 gesetzt: https://github.com/advisories/GHSA-v5pm-xwqc-g5wc.
- Exakter Vergleich des lokal gespeicherten BGG-Schlüssels mit sämtlichen für Git vorgesehenen Textdateien: kein Treffer. .env, User Secrets, node_modules, Build-Ausgaben und Testartefakte bleiben ausgeschlossen.

Abschluss des realen Imports: 10.000 von 10.000 Spielen mit BGG-Detailantwort dauerhaft in PostgreSQL gespeichert; 0 fehlgeschlagene und 0 ausstehende IDs. Anschließend den finalen Release-Build gestartet und erneut geprüft: Katalog bleibt vollständig, Empfehlungen liefern 20 eindeutige Spiele mit vier Entdeckungsplätzen, unbekannte Altersangaben werden als fehlend ausgegeben. Einzelne Metadaten können bei BGG fehlen; dies wird weiterhin ausdrücklich angezeigt.

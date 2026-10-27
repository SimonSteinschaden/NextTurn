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

## Erweiterung: Ranggewichtete Zufallsauswahl

- Globale Top-500-Gruppe mit Zielwahrscheinlichkeit 80 % pro Vorschlag; übrige Ränge mit 20 %. Gleichzeitiger Erhalt des separaten 20-%-Entdeckungsanteils.
- Innerhalb der Ranggruppe gewichtete Zufallsauswahl statt deterministischer Sortierung: Ranggewicht 1/Rang^0,65, multipliziert mit einem positiven begrenzten Profilfaktor. Damit bleiben schlechter platzierte Spiele möglich, aber bei gleicher Passung unwahrscheinlicher.
- Genre-/Mechanikvorlieben beeinflussen den Profilfaktor und die Kandidatenpriorität. Die Reihenfolge innerhalb bevorzugter Merkmale beruht auf dem vorhandenen allgemeinen BGG-Rang, nicht auf erfundenen unabhängigen Ranglisten.
- Begrenzte Kandidatenpools enthalten sowohl Top-500- als auch übrige Spiele; kein Abschneiden auf die ersten 300 Ränge. Leere bzw. erschöpfte Ranggruppen haben einen expliziten Fallback, ohne bewertete Spiele zurückzubringen.
- Zusätzliche Tests: 20.000 Ziehungen für die Top-500-Wahrscheinlichkeit trotz dominanter Vorliebe für ein schlechter platziertes Genre; 20.000 Ziehungen für monoton abnehmende Auswahlhäufigkeit innerhalb gleicher Lieblingsmerkmale; Genre- und Mechanikpräferenzen bei gleichem Rang; Entdeckungsanteil, fehlende Ränge, erschöpfte Gruppen und PostgreSQL-Pools über beide Ranggruppen.

Die Entdeckungsplätze werden zufällig über die Queue verteilt. Dadurch bleibt auch die erste Karte mit rund 20 % Wahrscheinlichkeit eine Entdeckung, wenn nach jedem Swipe eine neue Queue angefragt wird; ein fester fünfter Platz würde diesen Anteil beim tatsächlichen nächsten Vorschlag verlieren. Ein zusätzlicher Test simuliert 2.000 solche Queue-Neuberechnungen und prüft gleichzeitig die 20-%-Entdeckungs- und 80-%-Top-500-Chance der jeweils ersten Karte.
Verifikation dieser Erweiterung: 22 Unit-Tests erfolgreich, zusätzlich sechs PostgreSQL-Integrationstests einschließlich der neuen Ranggruppenprüfung. Frontend-Build und ESLint erfolgreich. Die laufende API wurde mit 25 echten Anfragen geprüft: alle Queues enthielten 20 eindeutige Spiele und vier Entdeckungsplätze; der Katalog blieb bei 10.000 vollständigen Detailantworten ohne Importfehler. In dieser kleinen Live-Stichprobe lagen 385 von 500 Karten in den Top 500; bei den jeweils ersten Karten waren es 17 von 25, fünf erste Karten waren Entdeckungen. Solche Stichproben schwanken; die größeren Simulationstests prüfen die Zielwahrscheinlichkeiten mit festen Zufallsseeds und definierten Toleranzen.

## Erweiterung: Persönliche Matchqualität

- Eigener normierter Ähnlichkeitsindex aus aktuellen Likes/SuperLikes; keine Skalierung des vorhandenen, vom Ranking beeinflussten RecommendationScore.
- Vergleich von Kategorien, Mechaniken, Komplexität, Spieldauer und Spielerbereich; paarweiser gewichteter Mittelwert, SuperLikes dreifach, ohne Selbstvergleich und ohne doppelte Historienzählung.
- Fehlende Angaben bleiben fehlend. Weniger als 30 % gemeinsam bekannte Merkmalsgewichte reichen nicht für eine Prozentzahl. Wenige Referenzen oder unvollständige Daten werden als vorläufig gekennzeichnet. Kein behauptetes statistisches Konfidenzniveau und keine kalibrierte Gefallenswahrscheinlichkeit.
- Gleiche Berechnung für Entdecken, Suche, Favoriten und Details; UI zeigt einen kompakten Prozentwert bzw. Match offen. Die Ranggruppenwahrscheinlichkeit und der Entdeckungsanteil bleiben unverändert.
- Neue Unit-Tests prüfen Identität, Rangunabhängigkeit, SuperLike-Gewichtung, aktuelle Zustände, Duplikate, Selbstvergleich, fehlende Daten, echte Nullwerte und partielle Übereinstimmung. PostgreSQL-Test prüft dieselben Werte in allen Spielansichten und die Neuberechnung nach Dislike/Skip. Browser-Test prüft Prozentanzeige, vorläufigen Wert und fehlende Daten auf Desktop und Mobil.

Verifikation der Matchqualität: 41 Backend-Tests und acht Browser-Tests erfolgreich; Frontend-Build und ESLint ohne Fehler. Mit einer getrennten QA-Session und fünf echten Katalogfavoriten wurden die laufende API sowie Empfehlungen, Suche, Favoriten und Details geprüft. Die Ansichten lieferten denselben Index; eigene Favoriten wurden ohne Selbstvergleich ausgewertet. Ohne Favoriten gab es keine erfundenen Prozentwerte. Desktop/Mobil visuell geprüft, ohne JavaScript-Fehler oder horizontales Überlaufen. Danach ausschließlich die erzeugten QA-Datensätze entfernt und geprüft, dass keine veralteten Matchwerte verbleiben. Bestehende Nutzersitzungen und Katalogdaten blieben erhalten.

## Erweiterung: Besitzverwaltung und Spieleabend planen

- Neue Navigation Spieleabend planen; Sammlung mit getrennten Filtern Favoriten und In Besitz. Besitz lässt sich in Suche, Karten und Details unabhängig von Likes verwalten.
- Migration OwnershipAndGameNights erweitert Interactions um IsOwned und erlaubt Action=null für reinen Besitz. Neue Tabellen GameNights und GameNightChoices speichern Abende und deren unabhängige Auswahl. Bestehende Bewertungen und Historie bleiben erhalten; vorhandene Likes werden nicht als Besitz geraten.
- Der Planer startet ausschließlich mit eigenen Spielen. Auch Spiele nicht im Besitz anzeigen erweitert den Pool um aktuelle unbesessene Likes/SuperLikes. Bekannte passende Spieleranzahl und importierte Details bleiben Voraussetzung. Eigene bereits bewertete Spiele sind für den Abend erlaubt.
- Gemeinsame Tinder-Karte mit Drag/Swipe, Heute nicht, Skip, Vormerken und Top-Wahl. Entscheidungen gelten nur für den jeweiligen Abend. Gespeicherte Abende, Einstellungen und Abend-Liste sind wieder aufrufbar; nicht mehr vorhandene Einträge können entfernt werden, ohne einen globalen Like zu ändern.
- Matchqualität kommt aus denselben aktuellen globalen Favoriten wie in allen bisherigen Ansichten. Der Planer priorisiert innerhalb seines begrenzten Besitz-/Favoritenpools diesen Matchwert und den Rang. Die bisherige Zufallsauswahl und Ausschlussregel von Entdecken bleiben unverändert.
- Backendprüfung: Besitz ohne Rating/Statistik/Ausschluss, unabhängige Entfernung, gleichzeitige Besitz- und Bewertungsänderung, Default-Pool und Ausleihoption, Spielerzahl/fehlende Details, voneinander getrennte Abende, idempotente Auswahl, Sessionzuordnung, Poolvalidierung, dauerhafte Listen und Pagination. Ein eigener Upgrade-Test beginnt mit der alten Datenbankstruktur samt bestehendem SuperLike und Historie und prüft deren Erhalt.
- 47 Backendtests und zwölf Desktop-/Mobiltests erfolgreich. Release-Build, TypeScript, ESLint und EF-Modellabgleich ohne Fehler. Die Browsertests nutzen synthetische Fixtures und prüfen zusätzlich getrennte Besitz-/Favoritenlisten, gespeicherte Abende, Default-/Ausleihpool und fehlgeschlagene Auswahl.
- Live-Prüfung mit separater QA-Session und echten Katalogdaten: zwei eigene Spiele und ein unbesessener Favorit; Standardpool zwei, erweiterter Pool drei. Vormerken ändert die globale Statistik nicht; Auswahl bleibt nach Neuladen erhalten. Fremde Session erhält 404; fehlende Besitzangabe oder Abend-Aktion erhält 400.
- Echte Desktop-, Tablet- und Mobilansichten mit BGG-Covern geprüft, ohne JavaScript-Fehler oder horizontalen Überlauf. Bestehende Abende zeigen ihre Einstellungen aufklappbar, damit die Swipe-Karte im Vordergrund steht. Der lokale Datenbankhost verwendet explizit IPv4-Loopback gegen Windows-Verbindungsabbrüche; der BGG-Schlüssel bleibt ausschließlich in privaten User Secrets.

## Korrektur: Unabhängige Genreprofile für die Matchqualität

- Aktuelle Likes/SuperLikes werden nach BGG-Kategorie in unabhängige Interessenprofile aufgeteilt. Ein Vorschlag wird ausschließlich mit den anderen Favoriten des jeweiligen passenden Genres verglichen. Hat er mehrere passende Genres, zählt das stärkste Profil; unterschiedliche Genres werden nicht gegenseitig heruntergemittelt.
- Gewichte bleiben Genre 35 %, Mechanik 35 %, Komplexität 15 %, Dauer 10 %, Spielerbereich 5 %. Innerhalb eines passenden Profils ist die Genrezugehörigkeit eine volle Übereinstimmung. Mechaniken, Zahlenmerkmale, SuperLike-Gewichtung und das Mitteln innerhalb eines Genres bleiben wirksam.
- Referenzzahl, Datenabdeckung und vorläufig beziehen sich auf das gewählte Profil. Viele unpassende Genre-Favoriten erzeugen keine künstliche Sicherheit. Selbstvergleich, doppelte Historie, Dislikes/Skips und fehlende Daten bleiben ausgeschlossen bzw. explizit behandelt.
- Neue oder fehlende Genres erlauben weiterhin eine Passung über andere bekannte Merkmale. Ein unbekanntes Interesse wird nicht als bestehendes Genreprofil ausgegeben.
- API ergänzt matchedGenre; Spieldetails und Match-Kurzinfo erklären das gewählte Genreprofil. Die Matchquote bleibt ein beschreibender Ähnlichkeitsindex. Keine Schemaänderung, kein zusätzlicher BGG-Aufruf; allgemeine Statistik und Zufalls-/Rankingregeln von Entdecken bleiben erhalten.
- Neue Regressionstests prüfen gleichzeitig perfekte Fantasy-/Wirtschaftspassung trotz verschiedener Mechaniken und Zahlenwerte, Unabhängigkeit von 30 fremden SuperLikes, Evidenz pro Genre, Mehrfachgenres, fehlende Kategorien, Teilüberschneidung der Mechaniken, unverändert wirksame Merkmalsgewichte und Selbstvergleich.
- PostgreSQL-Test prüft identische Genrewerte in Suche, Details, Entdecken sowie Spieleabend-Kandidaten und Abend-Liste; das Entfernen eines Fantasy-Favoriten verändert den getrennten Wirtschafts-Match nicht. Browserprüfung ergänzt Genreprofil-Hinweise und die Detailansicht auf Desktop und Mobil.
- 57 Backendtests und zwölf Browsertests erfolgreich; Frontend-Produktionsbuild und ESLint ohne Fehler.

Live-Prüfung mit separater QA-Session: War of the Ring behält seinen Fantasy-Match von 67 % nach zusätzlichem Wirtschafts-SuperLike; Terraforming Mars behält seinen Wirtschafts-Match von 73 % nach Entfernen des Fantasy-Likes. Suche und Details stimmen überein; Entdecken liefert weiter 20 Vorschläge. Echte Desktop-/Mobilansichten ohne JavaScript-Fehler oder Überlauf geprüft. Anschließend nur die eigene QA-Session entfernt; bestehende Nutzersitzungen und Katalogdaten erhalten.

## Erweiterung: Frei einstellbare BGG-Rangobergrenze

- Gemeinsames Feld BGG-Rang bis in Entdecken, Katalog/Sammlung und Spieleabend. Eingabe 1–10.000 einschließlich; leer bedeutet alle Ränge. Browserpersistenz und gleiche Einstellung nach Ansichtswechsel bzw. Reload.
- Serverseitiger maxRank-Queryparameter vor Suche, Pagination und Kandidatenauswahl. Auch die reine Empfehlungsauswahl prüft die Grenze, sodass ein erschöpfter Pool nicht auf schlechtere Spiele ausweicht. Unbekannte/ungültige Ränge erfüllen eine aktive Grenze nicht.
- Favoriten, Besitz und Abend-Auswahlen bleiben unverändert gespeichert. Rangfilter blendet Einträge aus und zählt nur sichtbare Treffer; Aufheben der Grenze stellt sie wieder her. Keine Schemaänderung oder neue BGG-Abfrage.
- Frontend verwirft überholte Queueantworten und zeigt während eines gewechselten Abend-Filters keine alte Queue als aktuelle Auswahl. Katalog und Abend-Liste beginnen nach einer Grenzänderung auf Seite 1. Gefilterte leere Sammlungen behaupten nicht, dass noch keine Spiele gespeichert seien.
- Grenzwerttests bei 1/499/500/501/10.000, Ranggruppen-Fallback, unbekannte Ränge, Pagination, kombinierte Filter, erschöpfte Vorschläge, gespeicherte Spiele und gefilterte Nachtantworten. Browserabläufe prüfen gemeinsame Werte, Reload-Persistenz, Rangfilter schon beim Anlegen eines Abends sowie das Wiederanzeigen ausgeblendeter Einträge.
- 65 Backendtests und 16 Desktop-/Mobiltests erfolgreich. TypeScript-Produktionsbuild und ESLint ohne Fehler; EF-Modellabgleich bestätigt keine ausstehende Migration.
- Echte API-Prüfung mit eigener QA-Session: Grenze 538 liefert 538 Treffer und enthält Rang 538; nach Skip des einzigen Rang-1-Spiels bleibt die auf Rang 1 begrenzte Queue leer. Top-500-Queue enthält 20 passende Spiele. Filter blendet Besitz/Favorit/Abend-Einträge aus, Aufheben stellt sie wieder her; Statistik unverändert. Sechs ungültige Grenzanfragen an verschiedene Endpunkte werden mit HTTP 400 abgelehnt.

Desktop, Tablet sowie 390-/320-Pixel-Ansichten mit echter API geprüft: kein horizontaler Überlauf oder JavaScript-Fehler. Ein ungespeicherter Spieleabend-Entwurf bleibt beim Ändern der Ranggrenze erhalten. Anschließend ausschließlich die benannte QA-Session samt Testabend entfernt; bestehende Nutzerdaten und Katalog bleiben erhalten.

## Korrekturen: Neutrale Filterwechsel und zentrierte Spieldetails

- GameCard kennt einen neutralen Übergang ohne seitliche Bewegung. Rang-/Personenfilter und Queue-Aktualisierung verwenden diesen Übergang. Nur explizite Swipe-/Bewertungsaktionen setzen eine Richtung. Spieleabend-Einstellungsänderungen verwenden ebenfalls den neutralen Übergang.
- Zusätzlicher Browserregressionstest führt zuerst einen echten Like aus und tippt anschließend den Rangfilter Ziffer für Ziffer. Er misst die horizontale Kartentransformation über mehrere Animationsframes und bestätigt, dass keine weitere Bewertung gesendet wird; ein anschließender Dislike-Button funktioniert weiterhin.
- Der native Detaildialog erhält explizit position:fixed, inset:0 und margin:auto, damit der CSS-Reset die Browserzentrierung nicht aufhebt. Inhalt bleibt in der größenbegrenzten Dialogfläche scrollbar.
- 18 Browsertests auf Desktop/Mobil sowie Frontend-Build und Lint erfolgreich. Die zuvor geprüften 65 Backendtests bleiben unverändert. Reale Dialogmessung bei 1440x1000, 390x844 und 1280x600: jeweils exakt zentriert (horizontaler und vertikaler Mittelpunktversatz 0 px), Escape schließt weiterhin.

## Profil: Community-Bestbesetzungen und Spieldauer-Tendenz

- Durchschnittlicher BGG-Rang und mittlere Spieldauer aus Profil und Statistik-DTO entfernt. Stattdessen bevorzugte Community-Spieleranzahl und eine aus Verteilungen berechnete Dauer-Tendenz.
- Beste Spieleranzahl ist der häufigste BestPlayerCounts-Pollwert unter aktuellen Favoriten. Pro Spiel/Zahl einmal; numerische Sortierung und mehrere gleich häufige Werte bleiben erhalten. Weder Hersteller-Min/Max noch allgemeine RecommendedPlayerCounts werden als Bestbesetzung ausgegeben. Ohne Bestdaten bleibt die Kennzahl offen.
- Schnell bis einschließlich 60 Minuten, lang darüber; eine gültige Min/Max-Spanne über beide Gruppen bleibt variabel. Kein Durchschnitt über gemischte schnelle/lange Favoriten und keine Zwangseinstufung einer breiten Zeitspanne anhand ihres Mittelpunkts. Ab 60 % bekannter Angaben dominiert eine Gruppe, sonst Gemischt; fehlende Angaben bleiben fehlend.
- Kleine oder unvollständige Datenbasis wird als vorläufig markiert. Prozentwerte der Kennzahlen beziehen sich auf bekannte Angaben, Diagramme weiterhin auf alle Favoriten. Profiltext, Kennzahl und Diagramme verwenden dieselben Kategorien.
- Tests prüfen die Grenze 60/61 Minuten, Rundung knapp unter 60 %, breite Spannen, gemischte Vorlieben, fehlende Daten, Community-Best statt Herstellerbereich, Gleichstände 2/4 statt Mittelwert 3, doppelte/ungültige Pollwerte und numerische Reihenfolge. PostgreSQL-Prüfung bestätigt dauerhaft gespeicherte Bestangaben und die Neuberechnung nach Entfernen eines Favoriten.
- 77 Backendtests sowie 20 Desktop-/Mobiltests erfolgreich; Frontend-Produktionsbuild, TypeScript und ESLint ohne Fehler. Kein Schemawechsel, EF-Modellabgleich ohne ausstehende Migration.
- Live-QA mit fünf echten Favoriten: Community-Bestbesetzung 4 bei unterschiedlichen Herstellerbereichen 2–4, 1–6 und 1–5. Dauermix: 20 % schnell, 40 % lang, 40 % variabel, korrekt Gemischt. Durchschnittsrang, Durchschnittszeit und Hersteller-Spielerbereich sind nicht mehr in der Statistikantwort enthalten.
- Desktop, Tablet sowie 390-/320-Pixel-Ansichten ohne horizontales Überlaufen oder JavaScript-Fehler geprüft. Anschließend ausschließlich die eigene QA-Session bereinigt; Nutzerfavoriten und Katalog bleiben erhalten.

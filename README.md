# NextTurn

Brettspiel-Empfehlungen mit React, TypeScript und einer ASP.NET Core API. Die App nutzt BoardGameGeek-Ranglisten, Spielerdaten und Swipe-Feedback.

## Voraussetzungen

- .NET SDK 10
- Node.js 22.12 oder neuer und npm
- Ein eigener BoardGameGeek-API-Token fuer die Spielabfragen

## API starten

Im Repository-Hauptordner:

```powershell
dotnet restore backend/NextTurn.Api/NextTurn.Api.csproj
dotnet user-secrets set "BggApi:BearerToken" "DEIN_BGG_TOKEN" --project backend/NextTurn.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project backend/NextTurn.Api --launch-profile http
```

Alternativ kann der Token ueber die Umgebungsvariable `BggApi__BearerToken` gesetzt werden. Zugangsdaten gehoeren nicht ins Repository.

API: http://localhost:5080, Swagger: http://localhost:5080/swagger

Die Rangliste liegt in `data/boardgames_ranks.csv`. Sie wird beim Build und Publish in den API-Ausgabeordner kopiert. `Data:BoardGameRanksCsvPath` wird relativ zum API-Ausgabeordner aufgeloest; ein absoluter Pfad ist ebenfalls moeglich.

## Frontend starten

In einem zweiten Terminal:

```powershell
cd frontend
npm ci
npm run dev
```

Vite zeigt die lokale URL an (standardmaessig http://localhost:5173). Fuer eine andere API-Adresse `frontend/.env.example` nach `frontend/.env.local` kopieren und `VITE_API_BASE_URL` anpassen. Danach Vite neu starten bzw. neu bauen.

## Pruefungen

```powershell
dotnet build backend/NextTurn.Api/NextTurn.Api.csproj
cd frontend
npm run build
npm run lint
```

API-Smoke-Test bei laufender API:

```powershell
Invoke-RestMethod http://localhost:5080/api/health
Invoke-RestMethod http://localhost:5080/api/games/ranked
```

Die Empfehlungen und Swipes benoetigen den BGG-Token und Zugriff auf BoardGameGeek. Profile werden derzeit nur im Arbeitsspeicher gehalten und gehen bei einem API-Neustart verloren. Die optionale `docker-compose.yml` stellt PostgreSQL und Redis fuer die lokale Entwicklung bereit; die aktuelle API verwendet diese Dienste noch nicht. Das enthaltene Datenbankpasswort ist nur fuer lokale Entwicklung vorgesehen.

## Struktur

- `backend/NextTurn.Api`: API, Ranglisten, Empfehlungen und Swipe-Feedback
- `frontend`: React-App mit Spielkarten und Swipe-Bedienung
- `data`: BoardGameGeek-Rangliste
- Die Word-Dateien enthalten die Projektplanung.
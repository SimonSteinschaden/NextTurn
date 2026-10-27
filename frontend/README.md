# NextTurn Frontend

React / TypeScript / Vite / Tailwind / Framer Motion.

```sh
npm ci
npm run dev
```

Die API wird standardmäßig unter http://localhost:5080 erwartet. Eine lokale .env.local kann VITE_API_BASE_URL überschreiben; siehe .env.example. API-Schlüssel bleiben ausschließlich im Backend.

```sh
npm run build
npm run lint
npx playwright install chromium
npm run test:e2e
```

Mobile Navigation: Entdecken, Suche, Meine Spiele, Profil. Die Browser-Tests prüfen beide Bildschirmgrößen mit synthetischen API-Fixtures. Die vollständige Einrichtung für PostgreSQL, EF-Migrationen und BGG-Import steht in der README im Repository-Hauptordner.

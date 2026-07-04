---
name: verify
description: Build, launch and drive the SportTrack watch app (watch-app/) to verify changes end-to-end.
---

# Vérifier watch-app (SportTrack)

## Build & lancement

```bash
cd watch-app
npm install            # première fois seulement
npm run build          # tsc -b && vite build — doit passer sans erreur
npm run dev            # sert sur http://localhost:5174
```

## Piloter l'app (Playwright)

Chromium est préinstallé : `executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome'`
(le suffixe de version peut changer — `ls /opt/pw-browsers`). Installer le paquet
`playwright` dans un répertoire temporaire, pas dans le repo.

Flux à couvrir :

1. **Mode démo** (pas de GPS requis) : cliquer le chip `démo`, puis `Démarrer`,
   attendre ~10 s → `[data-testid=speed]` ≈ 19–20 km/h, `[data-testid=distance]` > 0,
   `[data-testid=duration]` qui défile. Onglet `Carte` → polyline cyan + marqueur.
2. **GPS réel émulé** : contexte Playwright avec `permissions: ['geolocation']` et
   `geolocation: {...}`, puis `ctx.setGeolocation()` en boucle pour simuler le
   déplacement. Le point `.gps-dot` doit passer au vert (`.gps-ok`).
3. **Pause/Reprendre/Terminer** : la durée gèle en pause, tout revient à zéro après
   `Terminer`.
4. **Export GPX** : en pause, bouton `GPX` → `page.waitForEvent('download')`,
   vérifier `<trkpt lat=… lon=…><ele>…<time>` dans le fichier.
5. **Erreur GPS** : stubber `navigator.geolocation.watchPosition` via
   `addInitScript` pour appeler le callback d'erreur avec `code: 1` →
   bandeau `.error` « Accès à la position refusé ».

## Pièges

- Les tuiles `tile.openstreetmap.org` sont bloquées par le proxy du sandbox :
  la carte reste sombre en environnement distant, mais polyline et marqueur
  s'affichent quand même. Ce n'est pas un bug de l'app.
- L'API Geolocation exige un contexte sécurisé (`localhost` ou HTTPS).
- Le service worker ne s'enregistre qu'en build de production (`import.meta.env.PROD`) ;
  préférer `npm run dev` pour éviter les effets de cache pendant les tests.

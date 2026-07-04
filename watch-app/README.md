# SportTrack — application GPS pour montre de sport

Application web progressive (PWA) de suivi d'activité sportive, conçue pour
un écran de montre (cadran rond, thème sombre, gros chiffres). Elle
fonctionne sur toute montre ou téléphone disposant d'un navigateur avec GPS
(Wear OS, Android, iOS) et peut être installée sur l'écran d'accueil.

## Fonctionnalités

- **Cartographie** : fond de carte OpenStreetMap (Leaflet), tracé du parcours
  en temps réel et marqueur de position qui suit le déplacement.
- **Suivi GPS** : enregistrement du parcours via l'API Geolocation
  (`watchPosition`, haute précision), filtrage des points imprécis.
- **Données de séance** :
  - vitesse instantanée (km/h), vitesse moyenne et vitesse max ;
  - distance parcourue et durée (chrono avec pause/reprise) ;
  - allure (min/km), altitude et dénivelé positif cumulé.
- **Export GPX** : téléchargement de la trace au format GPX 1.1 (compatible
  Strava, Garmin Connect, Komoot…).
- **Mode démo** : simule un parcours pour tester sans capteur GPS.
- **Hors connexion** : la coque de l'application est mise en cache par un
  service worker (les tuiles de carte nécessitent le réseau).

## Démarrage

```bash
cd watch-app
npm install
npm run dev        # http://localhost:5174
```

> ⚠️ L'API Geolocation exige un contexte sécurisé : `localhost` en
> développement, HTTPS en production.

## Build de production

```bash
npm run build      # génère dist/
npm run preview    # sert le build localement
```

## Utilisation

1. Ouvrir l'application (ou activer « démo » pour tester sans GPS).
2. **Démarrer** : la montre enregistre le parcours et affiche les données.
3. Basculer entre **Données** et **Carte** avec les onglets.
4. **Pause** puis **Reprendre**, **GPX** pour exporter la trace, ou
   **Terminer** pour clore la séance.

## Architecture

```
src/
  lib/geo.ts        # haversine, formats (vitesse, allure, durée), export GPX
  lib/session.ts    # accumulation des points et calcul des statistiques
  lib/sources.ts    # sources de position : GPS réel et simulateur de démo
  hooks/useTracker.ts  # machine à états démarrer/pause/reprendre/terminer
  components/MapView.tsx   # carte Leaflet (tracé + marqueur + suivi)
  components/StatsView.tsx # écran de données grand format
  App.tsx           # navigation, contrôles, export GPX
public/
  manifest.webmanifest, sw.js, icon.svg  # PWA installable
```

# 🚲 Bikeshop — gestion de magasin de vélo

Application complète de gestion d'un magasin de vélo : API .NET 8 (monolithe
modulaire) + frontend React (Vite + TypeScript).

## Architecture

**Monolithe modulaire** plutôt que micro-services : pour un commerce unique avec
des volumes modestes, découper en services distincts (et orchestrer avec
Temporal) ajouterait une complexité opérationnelle sans bénéfice. Les modules
sont isolés par dossier et communiquent par appels de services en mémoire, dans
la même transaction ; ils pourront être extraits en services indépendants si le
besoin apparaît.

```
src/Bikeshop.Api/
├── Common/               socle (DbContext EF Core + SQLite)
└── Modules/
    ├── Accounting/       comptabilité — EVENT SOURCING
    ├── Inventory/        stocks et mouvements
    ├── Workshop/         planning de l'atelier
    ├── Invoicing/        facturation
    ├── Pos/              caisse (sessions, tickets, Z)
    └── Procurement/      fournisseurs et commandes de composants
tests/Bikeshop.Api.Tests/ tests d'intégration des endpoints (xUnit)
frontend/                 React + Vite + TypeScript
```

### Comptabilité en event sourcing

La seule écriture autorisée dans le module comptabilité est l'**ajout d'un
événement immuable** (`AccountOpened`, `JournalEntryPosted`) au flux `ledger`
de la table `Events`. Comptes, journal, balance et grands livres sont des
**projections** reconstruites en rejouant le flux. Les écritures sont validées
(partie double : total débits = total crédits) avant ajout.

Plan comptable français pré-chargé : 401, 411, 44566, 44571, 512, 530, 607,
706, 707.

### Intégrations entre modules

| Action métier | Effets |
|---|---|
| Vente en caisse | stock −, écriture 530/512 → 707 + 44571 |
| Réception commande fournisseur | stock +, écriture 607 + 44566 → 401 |
| Clôture intervention atelier | pièces sorties du stock |
| Facturation intervention | facture main-d'œuvre + pièces |
| Émission facture | écriture 411 → 706/707 + 44571 |
| Encaissement facture | écriture 512/530 → 411 |

## Démarrer

### API (port 5000)

```bash
dotnet run --project src/Bikeshop.Api
```

Swagger : http://localhost:5000/swagger — base SQLite `bikeshop.db` créée au
premier lancement.

### Frontend (port 5173)

```bash
cd frontend
npm install
npm run dev
```

Le proxy Vite relaie `/api` vers `http://localhost:5000`.

### Tests

```bash
dotnet test
```

28 tests d'intégration couvrent les endpoints de chaque module et les flux
transverses (vente → stock → comptabilité, commande → réception → stock, etc.).

## Endpoints principaux

- `GET/POST /api/accounting/accounts`, `POST /api/accounting/entries`,
  `GET /api/accounting/journal`, `GET /api/accounting/balance`,
  `GET /api/accounting/accounts/{n}/ledger`
- `GET/POST/PUT /api/inventory/products`, `POST /api/inventory/products/{id}/adjust`,
  `GET /api/inventory/movements`, `GET /api/inventory/low-stock`
- `GET/POST /api/workshop/mechanics`, `GET /api/workshop/planning`,
  `GET/POST/PUT /api/workshop/jobs`, `POST /api/workshop/jobs/{id}/{start|complete|invoice|cancel}`
- `GET/POST /api/invoicing/invoices`, `POST /api/invoicing/invoices/{id}/{issue|pay}`
- `POST /api/pos/sessions/open`, `POST /api/pos/sessions/{id}/close`,
  `GET /api/pos/sessions/{id}/summary`, `GET/POST /api/pos/sales`
- `GET/POST /api/procurement/suppliers`, `GET/POST /api/procurement/orders`,
  `POST /api/procurement/orders/{id}/{send|receive|cancel}`,
  `GET /api/procurement/replenishment-suggestions`

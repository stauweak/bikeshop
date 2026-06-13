# Déploiement — Bikeshop API

Containerisation de l'API et déploiement sur Kubernetes via Helm.

## 1. Construire l'image Docker

Depuis la racine du dépôt (le contexte de build doit inclure `src/`) :

```bash
docker build -t bikeshop-api:0.1.0 .
```

Le `Dockerfile` est multi-étapes :
1. **build** (`mcr.microsoft.com/dotnet/sdk:8.0`) : `dotnet restore` puis
   `dotnet publish -c Release`.
2. **runtime** (`mcr.microsoft.com/dotnet/aspnet:8.0`) : image finale, exécutée
   en utilisateur **non-root** (`app`, UID 1654), écoutant sur le port **8080**.

L'image écoute sur `8080` et écrit sa base SQLite dans `/data/bikeshop.db`
(chemin paramétrable via `ConnectionStrings__Default`).

Test local :

```bash
docker run --rm -p 8080:8080 bikeshop-api:0.1.0
curl localhost:8080/healthz      # {"status":"Healthy"}
curl localhost:8080/readyz       # {"status":"Ready"}
```

Pousser vers un registre :

```bash
docker tag bikeshop-api:0.1.0 <registre>/bikeshop-api:0.1.0
docker push <registre>/bikeshop-api:0.1.0
```

## 2. Déployer avec Helm

Le chart se trouve dans `deploy/helm/bikeshop-api`.

```bash
# Validation et aperçu du rendu
helm lint deploy/helm/bikeshop-api
helm template bikeshop deploy/helm/bikeshop-api

# Installation
helm install bikeshop deploy/helm/bikeshop-api \
  --namespace bikeshop --create-namespace \
  --set image.repository=<registre>/bikeshop-api \
  --set image.tag=0.1.0
```

Accéder à l'API (Service ClusterIP par défaut) :

```bash
kubectl -n bikeshop port-forward svc/bikeshop-bikeshop-api 8080:80
curl localhost:8080/api/accounting/accounts
```

Mise à jour / désinstallation :

```bash
helm upgrade bikeshop deploy/helm/bikeshop-api -n bikeshop --set image.tag=0.2.0
helm uninstall bikeshop -n bikeshop
```

## 3. Paramètres principaux (`values.yaml`)

| Clé | Défaut | Description |
|---|---|---|
| `image.repository` / `image.tag` | `bikeshop-api` / `appVersion` | Image à déployer |
| `replicaCount` | `1` | **Doit rester à 1 avec SQLite** (écrivain unique) |
| `config.aspnetcoreEnvironment` | `Production` | `Development` active Swagger |
| `config.connectionString` | `Data Source=/data/bikeshop.db` | Chaîne EF Core |
| `persistence.enabled` / `.size` | `true` / `1Gi` | Volume SQLite (PVC `ReadWriteOnce`) |
| `persistence.existingClaim` | `""` | Réutiliser un PVC existant |
| `service.type` / `.port` | `ClusterIP` / `80` | Service |
| `ingress.enabled` | `false` | Exposition HTTP externe |
| `resources` | 100m/128Mi → 1/512Mi | Requests / limits |

Exemple d'exposition via Ingress + TLS :

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  hosts:
    - host: bikeshop.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: bikeshop-api-tls
      hosts:
        - bikeshop.example.com
```

## Note sur la persistance et la montée en charge

L'API utilise **SQLite**, une base mono-fichier qui n'accepte qu'un seul
processus écrivain. Le déploiement est donc volontairement :

- limité à **1 réplica** ;
- en stratégie de déploiement **`Recreate`** (l'ancien pod libère le volume
  `ReadWriteOnce` avant que le nouveau ne l'attache).

Pour une montée en charge horizontale (plusieurs réplicas, HPA), migrez vers
une base serveur **PostgreSQL** : ajoutez le paquet
`Npgsql.EntityFrameworkCore.PostgreSQL`, remplacez `UseSqlite` par `UseNpgsql`
dans `Program.cs`, puis surchargez `config.connectionString`, désactivez
`persistence` et augmentez `replicaCount`.

# --- Étape 1 : compilation et publication ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration en couche séparée pour profiter du cache tant que le csproj ne change pas.
COPY src/Bikeshop.Api/Bikeshop.Api.csproj src/Bikeshop.Api/
RUN dotnet restore src/Bikeshop.Api/Bikeshop.Api.csproj

COPY src/ src/
RUN dotnet publish src/Bikeshop.Api/Bikeshop.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# --- Étape 2 : image d'exécution ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Répertoire de données SQLite, possédé par l'utilisateur non-root « app » fourni par l'image.
RUN mkdir -p /data && chown -R app:app /data
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/bikeshop.db"

EXPOSE 8080
ENTRYPOINT ["dotnet", "Bikeshop.Api.dll"]

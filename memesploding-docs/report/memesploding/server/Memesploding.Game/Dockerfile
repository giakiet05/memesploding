# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY server/Memesploding.sln server/
COPY server/Memesploding.Api/Memesploding.Api.csproj server/Memesploding.Api/
COPY server/Memesploding.Game/Memesploding.Game.csproj server/Memesploding.Game/
COPY server/Memesploding.Shared/Memesploding.Shared.csproj server/Memesploding.Shared/

RUN dotnet restore server/Memesploding.Game/Memesploding.Game.csproj

COPY server/ server/

WORKDIR /src/server/Memesploding.Game
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 5204

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Memesploding.Game.dll"]

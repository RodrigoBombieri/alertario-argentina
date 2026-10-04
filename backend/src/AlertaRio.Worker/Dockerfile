FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ backend/
RUN dotnet restore backend/src/AlertaRio.Worker/AlertaRio.Worker.csproj --locked-mode
RUN dotnet publish backend/src/AlertaRio.Worker/AlertaRio.Worker.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ ./
USER app
ENTRYPOINT ["dotnet", "AlertaRio.Worker.dll"]

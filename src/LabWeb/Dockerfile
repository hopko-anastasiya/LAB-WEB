FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM node:22-alpine AS spa
WORKDIR /src/spa
COPY src/spa/package.json src/spa/package-lock.json ./
RUN npm ci
COPY src/spa/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/LabWeb/LabWeb.csproj", "src/LabWeb/"]
RUN dotnet restore "src/LabWeb/LabWeb.csproj"
COPY src/LabWeb/ src/LabWeb/
COPY --from=spa /src/LabWeb/wwwroot/app src/LabWeb/wwwroot/app
RUN dotnet build "src/LabWeb/LabWeb.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/LabWeb/LabWeb.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/wwwroot/uploads /app/keys && chown -R app:app /app/wwwroot/uploads /app/keys
USER app
ENTRYPOINT ["dotnet", "LabWeb.dll"]

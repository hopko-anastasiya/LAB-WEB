FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/LabWeb/LabWeb.csproj", "src/LabWeb/"]
RUN dotnet restore "src/LabWeb/LabWeb.csproj"
COPY src/LabWeb/ src/LabWeb/
RUN dotnet build "src/LabWeb/LabWeb.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/LabWeb/LabWeb.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot/uploads
USER app
ENTRYPOINT ["dotnet", "LabWeb.dll"]

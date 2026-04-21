# Build context is usually this folder (Railway "Root Directory" = StudentPortal).
# Also works if context is the parent repo with ./StudentPortal/StudentPortal.csproj.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

# Prefer monorepo path when present; otherwise flat app root (StudentPortal.csproj next to this file).
RUN if [ -f "./StudentPortal/StudentPortal.csproj" ]; then \
      export CSPROJ="./StudentPortal/StudentPortal.csproj"; \
    else \
      export CSPROJ="./StudentPortal.csproj"; \
    fi && \
    echo "Using project: $CSPROJ" && \
    dotnet restore "$CSPROJ" && \
    dotnet publish "$CSPROJ" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StudentPortal.dll"]

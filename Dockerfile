# ConfigForge — Open mode (browser-based) container image.
#
# Open mode runs anywhere .NET runs, so it containerises cleanly. The Locked-mode
# Standalone host is a Windows WPF executable and is NOT containerised here (publish
# it with: dotnet publish src/ConfigForge.Standalone -c Release -r win-x64 --self-contained).
#
# This image builds and runs the sample Open-mode web host
# (samples/ConfigForge.Sample.Web), which renders the ConfigForge UI via Blazor Server.
#
# Build (from the repo root):   docker build -t configforge-web .
# Run:                          docker run --rm -p 8080:8080 configforge-web
# Then open http://localhost:8080
#
# NOTE: intentionally NOT wired into the CI pipeline (ci.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole repo so the sample's relative ProjectReferences and the shared
# Directory.Build.props / global.json resolve.
COPY . .

RUN dotnet restore samples/ConfigForge.Sample.Web/ConfigForge.Sample.Web.csproj
RUN dotnet publish samples/ConfigForge.Sample.Web/ConfigForge.Sample.Web.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConfigForge.Sample.Web.dll"]

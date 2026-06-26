# ConfigForge - Open mode (browser-based) container image.
#
# Open mode runs anywhere .NET runs, so it containerises cleanly. The Locked-mode
# Standalone host is a Windows WPF executable and is NOT containerised here (publish
# it with: dotnet publish src/ConfigForge.Standalone -c Release -r win-x64 --self-contained).
#
# This image runs the configurable Open-mode host (samples/ConfigForge.Sample.OpenHost).
# Point it at schemas via environment variables, e.g. a remote manifest:
#   docker run --rm -p 8080:8080 \
#     -e ConfigForge__SchemaUrl=https://schemas.example.com/schemas.json configforge
# Or a mounted folder:
#   docker run --rm -p 8080:8080 \
#     -e ConfigForge__SchemaDirectory=/schemas -v /my/schemas:/schemas:ro configforge
# See docker-compose.yml for a paired schema-server + ConfigForge setup.
#
# Build (from the repo root):   docker build -t configforge .
#
# NOTE: intentionally NOT wired into the CI pipeline (ci.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the whole repo so the sample's relative ProjectReferences and the shared
# Directory.Build.props / global.json resolve.
COPY . .

RUN dotnet restore samples/ConfigForge.Sample.OpenHost/ConfigForge.Sample.OpenHost.csproj
RUN dotnet publish samples/ConfigForge.Sample.OpenHost/ConfigForge.Sample.OpenHost.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConfigForge.Sample.OpenHost.dll"]

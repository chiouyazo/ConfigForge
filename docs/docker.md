# Run in Docker

Open mode runs anywhere .NET runs, so it containerizes cleanly. (The standalone WPF build is Windows-only and isn't containerized.)

The [`Dockerfile`](../Dockerfile) at the repo root builds and runs the sample Open-mode host.

```
docker build -t configforge-web .
docker run --rm -p 8080:8080 configforge-web
```

Then open <http://localhost:8080>.

It's a normal multi-stage build: the SDK image publishes the web host, the ASP.NET runtime image runs it. To containerize your own host instead of the sample, point the `dotnet publish` line at your project.

The Dockerfile is intentionally not part of the CI pipeline.

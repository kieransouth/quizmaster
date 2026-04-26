# Contributing

Cheers for taking a look. PRs welcome.

## Local setup

You'll want Docker (for Postgres / optional Ollama), the .NET 10 SDK, and Node 22+.

```bash
# macOS / Linux
./dev.sh

# Windows
dev.bat
```

The first run creates the Postgres container, then starts the API on `http://localhost:5101` and Vite on `http://localhost:5173`. The frontend proxies `/api` to the API.

Copy `.env.example` to `.env` and fill in any AI provider keys you want to test with. Local Ollama works without a key.

## Before opening a PR

```bash
dotnet test                        # 68 unit tests
cd web && npm run lint && npm run build
```

CI runs both on every PR.

## Project layout

- `src/Kieran.Quizmaster.{Domain,Application,Infrastructure,Api}/` — clean-architecture-ish layering. Domain has no deps; Application is interfaces + DTOs; Infrastructure is implementations (EF, AI clients); API is controllers.
- `web/` — React + TS + Vite + Tailwind v4.
- `tests/Kieran.Quizmaster.Tests/` — Shouldly + NSubstitute. Service-level tests, no controller integration tests.
- `docker-compose.yml` plus `docker-compose.{standalone,traefik}.yml` overlays — pick one to run.

## Style

- Guid primary keys, `Ardalis.SmartEnum` for enums-stored-as-strings.
- Classic `[ApiController]` controllers, not minimal APIs.
- No repository pattern; controllers depend on services that depend on `DbContext` directly.
- Default to no comments; explain *why*, never *what*.

If you're adding a feature, a quick GitHub issue first to sanity-check direction is appreciated but not required.

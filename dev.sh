#!/usr/bin/env bash
# Quizmaster dev launcher (macOS / Linux).
#   - Ensures Postgres (and Ollama if it exists) containers are running
#   - Reads provider API keys from .env into ASP.NET-style env vars
#   - Runs the API and Vite dev server (Ctrl-C stops both)

set -euo pipefail
cd "$(dirname "$0")"

echo "=== Quizmaster dev launcher ==="
echo

# -------- 1. Postgres --------
if docker start qm-test-pg >/dev/null 2>&1; then
  echo "Postgres : started"
else
  echo "Postgres container not found, creating fresh..."
  docker run -d --name qm-test-pg \
    -e POSTGRES_DB=quizmaster \
    -e POSTGRES_USER=quizmaster \
    -e POSTGRES_PASSWORD=devpassword \
    -p 5432:5432 \
    postgres:16-alpine
fi

# -------- 2. Ollama (optional) --------
if docker start qm-test-ollama >/dev/null 2>&1; then
  echo "Ollama   : started"
fi

# -------- 3. Read auth toggles from .env --------
# Cloud AI keys (OpenAI, Anthropic) are per-user now; save them in the
# Settings page once the dev server is up.
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a

  [[ -n "${REGISTRATION_ENABLED:-}" ]] && export Auth__RegistrationEnabled="$REGISTRATION_ENABLED"
  [[ -n "${OLLAMA_ENABLED:-}"       ]] && export Ai__Providers__Ollama__Enabled="$OLLAMA_ENABLED"
fi

# -------- 4 & 5. API + Vite --------
echo "Starting API     -> http://localhost:5101"
echo "Starting Vite    -> http://localhost:5173"
echo

trap 'kill 0' INT TERM EXIT

dotnet run --project src/Kieran.Quizmaster.Api --launch-profile http &
(cd web && npm run dev) &

wait

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

# -------- 3. Read provider keys + auth toggles from .env --------
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a

  [[ -n "${OPENAI_API_KEY:-}"       ]] && export Ai__Providers__OpenAI__ApiKey="$OPENAI_API_KEY"
  [[ -n "${ANTHROPIC_API_KEY:-}"    ]] && export Ai__Providers__Anthropic__ApiKey="$ANTHROPIC_API_KEY"
  [[ -n "${REGISTRATION_ENABLED:-}" ]] && export Auth__RegistrationEnabled="$REGISTRATION_ENABLED"
else
  echo ".env not found - paid providers won't work without their API keys."
fi

# -------- 4 & 5. API + Vite --------
echo "Starting API     -> http://localhost:5101"
echo "Starting Vite    -> http://localhost:5173"
echo

trap 'kill 0' INT TERM EXIT

dotnet run --project src/Kieran.Quizmaster.Api --launch-profile http &
(cd web && npm run dev) &

wait

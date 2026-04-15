@echo off
REM Quizmaster dev launcher.
REM   - Ensures Postgres (and Ollama if it exists) containers are running
REM   - Reads provider API keys from .env into ASP.NET-style env vars
REM   - Spawns the API and Vite dev server in their own windows
REM
REM Close the spawned windows to stop the servers.

setlocal EnableDelayedExpansion

cd /d "%~dp0"

echo === Quizmaster dev launcher ===
echo.

REM -------- 1. Postgres --------
docker start qm-test-pg >nul 2>&1
if errorlevel 1 (
  echo Postgres container not found, creating fresh...
  docker run -d --name qm-test-pg ^
    -e POSTGRES_DB=quizmaster ^
    -e POSTGRES_USER=quizmaster ^
    -e POSTGRES_PASSWORD=devpassword ^
    -p 5432:5432 ^
    postgres:16-alpine
) else (
  echo Postgres : started
)

REM -------- 2. Ollama (optional, only if container exists) --------
docker start qm-test-ollama >nul 2>&1
if not errorlevel 1 (
  echo Ollama   : started
)

REM -------- 3. Read provider keys from .env --------
if exist .env (
  for /f "usebackq tokens=1* delims==" %%a in (".env") do (
    if /i "%%a"=="OPENAI_API_KEY"       set "OPENAI_API_KEY=%%b"
    if /i "%%a"=="ANTHROPIC_API_KEY"    set "ANTHROPIC_API_KEY=%%b"
    if /i "%%a"=="OLLAMA_CLOUD_API_KEY" set "OLLAMA_CLOUD_API_KEY=%%b"
  )
  REM Map them into the IConfiguration env-var convention.
  if defined OPENAI_API_KEY    set "Ai__Providers__OpenAI__ApiKey=!OPENAI_API_KEY!"
  if defined ANTHROPIC_API_KEY set "Ai__Providers__Anthropic__ApiKey=!ANTHROPIC_API_KEY!"
) else (
  echo .env not found - paid providers won't work without their API keys.
)

REM -------- 4. API --------
echo Starting API     -^> http://localhost:5101
start "Quizmaster API" cmd /k "dotnet run --project src\Kieran.Quizmaster.Api --launch-profile http"

REM -------- 5. Vite --------
echo Starting Vite    -^> http://localhost:5173
start "Quizmaster Web" cmd /k "cd web && npm run dev"

echo.
echo Open http://localhost:5173 in your browser.
echo Close the spawned terminal windows to stop the servers.
endlocal

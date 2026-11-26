#!/usr/bin/env bash
# scripts/start-local.sh — DEM-2026-001 / TASK-0012 (OPS)
# Sobe portal-api (dotnet, Release) e portal-web (ng serve local) em processos separados, só em loopback.
# Lê o .env LOCAL (fora do git). Não instala SDK/CLI. Não gera arquivo de configuração do front.
# Bind da API: Program.cs lê PORTAL_API_HOST/PORTAL_API_PORT (D7; confirmado pelo FS).
# Não exporta ASPNETCORE_URLS e não passa --urls. Usa --no-launch-profile.
# Build: caminho posicional (no SDK 10.0.404, "dotnet build --project" falha com MSB1001).
# --somente-mecanica: ensaios de PID antes do D7 (a API ainda pode escutar no padrão do Kestrel).
#   O ensaio N1 oficial (host, porta e CORS combinados) só vale depois da implementação do D7 pelo FS.
set -euo pipefail

die() { echo "[start-local] ERRO: $*" >&2; exit 1; }
ts() { TZ=America/Sao_Paulo date '+%Y-%m-%d %H:%M:%S %Z'; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || die "não foi possível entrar na raiz do monorepo."
RUN_DIR="$ROOT/run"
API_CSPROJ="apps/portal-api/src/Portal.Api/Portal.Api.csproj"
WEB_DIR="apps/portal-web"

SOMENTE_MECANICA=0
if [ "${1:-}" = "--somente-mecanica" ]; then SOMENTE_MECANICA=1; fi

[ -f ".env" ] || die ".env não existe. Copie .env.example para .env e preencha (o .env fica fora do git)."
[ -f "$API_CSPROJ" ] || die "$API_CSPROJ não existe."
[ -f "$WEB_DIR/package.json" ] || die "$WEB_DIR/package.json não existe."
[ -f "$WEB_DIR/angular.json" ] || die "$WEB_DIR/angular.json não existe."

command -v dotnet >/dev/null || die "dotnet não encontrado (SDK .NET 10; source ~/.config/ops-toolchain.sh)."
case "$(dotnet --version)" in 10.*) ;; *) die "SDK .NET série 10 exigida (ADR-0001 v2 D1); encontrado: $(dotnet --version)";; esac
command -v node >/dev/null || die "node não encontrado."
case "$(node -v)" in v24.*) ;; *) die "Node série 24 exigida como ferramenta (ADR-0001 v2 D3); encontrado: $(node -v)";; esac

set -a
# shellcheck disable=SC1091
. ./.env
set +a

# Uma só fonte de verdade para o bind (Program.cs). Remove herança da sessão ou entrada acidental.
unset ASPNETCORE_URLS ASPNETCORE_HTTP_PORTS ASPNETCORE_HTTPS_PORTS || true
# ng serve dá precedência à variável PORT sobre --port (MSG-COL do FS, 26/11 15:57).
unset PORT || true

for v in PORTAL_API_HOST PORTAL_API_PORT PORTAL_API_ALLOWED_ORIGIN LOG_LEVEL PORTAL_WEB_PORT PORTAL_API_BASE_URL; do
  [ -n "${!v:-}" ] || die "variável $v ausente ou vazia no .env."
done
[ "$PORTAL_API_HOST" = "127.0.0.1" ] || die "PORTAL_API_HOST deve ser loopback (127.0.0.1) neste teste (ADR-0001 D5/D7)."
[ "$PORTAL_API_ALLOWED_ORIGIN" != "*" ] || die "PORTAL_API_ALLOWED_ORIGIN não pode ser curinga (DEC-ARQ-0002 L-06)."

port_busy() { ss -ltn "sport = :$1" 2>/dev/null | tail -n +2 | grep -q .; }
if port_busy "$PORTAL_API_PORT"; then die "porta $PORTAL_API_PORT já está em uso na box. Nada foi iniciado."; fi
if port_busy "$PORTAL_WEB_PORT"; then die "porta $PORTAL_WEB_PORT já está em uso na box. Nada foi iniciado."; fi

mkdir -p "$RUN_DIR/logs"
if [ -f "$RUN_DIR/api.pid" ] || [ -f "$RUN_DIR/web.pid" ]; then
  die "run/*.pid já existe: rode scripts/stop-local.sh antes."
fi

echo "[start-local] $(ts) compilando portal-api em Release (caminho posicional)"
dotnet build "$API_CSPROJ" -c Release -warnaserror

echo "[start-local] $(ts) subindo portal-api (Release, bind pelo Program.cs) e portal-web (npx ng serve, loopback)"
(
  exec setsid dotnet run -c Release --project "$API_CSPROJ" --no-build --no-launch-profile
) >"$RUN_DIR/logs/api.log" 2>&1 < /dev/null &
echo $! > "$RUN_DIR/api.pid"
echo "Release" > "$RUN_DIR/api.configuration"

if [ ! -x "$WEB_DIR/node_modules/.bin/ng" ]; then
  if [ ! -f "$WEB_DIR/package-lock.json" ]; then
    die "$WEB_DIR/package-lock.json ausente (obrigatório, DEC-ARQ-0007 W1)."
  fi
  echo "[start-local] $(ts) node_modules do Portal ausente: executando npm ci (reproduzível)"
  (cd "$WEB_DIR" || exit 1; npm ci)
fi
if [ ! -x "$WEB_DIR/node_modules/.bin/ng" ]; then
  die "ng local não encontrado em $WEB_DIR/node_modules/.bin após npm ci."
fi

# Binário local. --define passa PORTAL_API_BASE_URL escapada como JSON (pedido do FS). Não gera arquivo de config.
(
  cd "$WEB_DIR" || exit 1
  unset PORT || true
  exec setsid npx --no-install ng serve --host 127.0.0.1 --port "$PORTAL_WEB_PORT" \
    --define "PORTAL_API_BASE_URL=\"${PORTAL_API_BASE_URL}\""
) >"$RUN_DIR/logs/web.log" 2>&1 < /dev/null &
echo $! > "$RUN_DIR/web.pid"

wait_http() {
  local url="$1" tries="${2:-60}"
  local i
  for i in $(seq 1 "$tries"); do
    if curl -fsS -o /dev/null --max-time 2 "$url" 2>/dev/null; then return 0; fi
    sleep 0.5
  done
  return 1
}
pid_vivo() { [ -n "${1:-}" ] && kill -0 "$1" 2>/dev/null; }

API_PID="$(cat "$RUN_DIR/api.pid")"
WEB_PID="$(cat "$RUN_DIR/web.pid")"

if [ "$SOMENTE_MECANICA" -eq 1 ]; then
  echo "[start-local] modo --somente-mecanica: ensaio de PID (N1 oficial só depois do D7 no código)."
  # Espera curta só para os processos permanecerem vivos; não exige GET /health na porta oficial.
  ok=1
  i=0
  while [ "$i" -lt 20 ]; do
    if pid_vivo "$API_PID" && pid_vivo "$WEB_PID"; then ok=0; break; fi
    sleep 0.25
    i=$((i + 1))
  done
  if [ "$ok" -ne 0 ] || ! pid_vivo "$API_PID" || ! pid_vivo "$WEB_PID"; then
    echo "[start-local] um dos processos morreu na subida; últimas linhas dos logs:" >&2
    tail -n 20 "$RUN_DIR/logs/api.log" >&2 || true
    tail -n 20 "$RUN_DIR/logs/web.log" >&2 || true
    "$ROOT/scripts/stop-local.sh" || true
    die "falha na mecânica de subida (PID)."
  fi
  # O Portal já deve escutar na porta oficial; a API, até o D7, pode ainda estar no padrão do Kestrel.
  if ! wait_http "http://127.0.0.1:${PORTAL_WEB_PORT}/" 80; then
    tail -n 20 "$RUN_DIR/logs/web.log" >&2 || true
    "$ROOT/scripts/stop-local.sh" || true
    die "falha na subida do Portal (ng serve)."
  fi
  echo "[start-local] AVISO: ensaio de mecânica apenas. GET /health em http://${PORTAL_API_HOST}:${PORTAL_API_PORT}/health ainda não é o critério oficial (D7 pendente)."
else
  if ! wait_http "http://${PORTAL_API_HOST}:${PORTAL_API_PORT}/health" 60; then
    echo "[start-local] portal-api NÃO respondeu em ~30 s em http://${PORTAL_API_HOST}:${PORTAL_API_PORT}/health; últimas linhas do log (sem valores de ambiente):" >&2
    tail -n 20 "$RUN_DIR/logs/api.log" >&2 || true
    "$ROOT/scripts/stop-local.sh" || true
    die "falha na subida da API. Se o D7 ainda não estiver no código, use --somente-mecanica."
  fi
  if ! wait_http "http://127.0.0.1:${PORTAL_WEB_PORT}/" 80; then
    tail -n 20 "$RUN_DIR/logs/web.log" >&2 || true
    "$ROOT/scripts/stop-local.sh" || true
    die "falha na subida do Portal."
  fi
fi

PORTAL_URL="http://127.0.0.1:${PORTAL_WEB_PORT}"
echo "[start-local] $(ts) OK: api pid=$(cat "$RUN_DIR/api.pid") web pid=$(cat "$RUN_DIR/web.pid") configuração=$(cat "$RUN_DIR/api.configuration") (logs em run/logs/, fora do git)"
echo "[start-local] Portal: ${PORTAL_URL}"
echo "[start-local] AVISO CORS: abra o Portal em ${PORTAL_URL}. http://localhost:${PORTAL_WEB_PORT} é outra origem e não deve ser usada em testes nem evidências."

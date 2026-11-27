#!/usr/bin/env bash
# scripts/stop-local.sh — DEM-2026-001 / TASK-0012 (OPS)
# Parada controlada pelos PIDs gravados em run/ (gitignorado). Sem pkill genérico.
# É o "rollback" da 1ª REL (não há versão/tag anterior) e a base para exercitar o CA-07.
# Ordem (confirmada com o FS): SIGTERM no PID específico; espera curta; só então SIGKILL no mesmo PID.
# Espera após SIGTERM: 10 s (proposta OPS).
# Critério de sucesso: nenhuma porta configurada continua escutando. Sai com código != 0 se algo permanecer.
set -uo pipefail

# Carrega o toolchain local da box, se existir (DOTNET_ROOT, PATH, telemetria).
if [ -f "${HOME}/.config/ops-toolchain.sh" ]; then
  # shellcheck disable=SC1091
  . "${HOME}/.config/ops-toolchain.sh"
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1
RUN_DIR="$ROOT/run"
# Proposta OPS (espera curta pedida pelo FS): 10 segundos após SIGTERM, antes do SIGKILL no mesmo PID.
WAIT_TERM_S=10
ts() { TZ=America/Sao_Paulo date '+%Y-%m-%d %H:%M:%S %Z'; }
T0=$(date +%s%N)
rc=0

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi

stop_one() { # nome pidfile token
  local name="$1" pidfile="$2" token="$3" pid waited=0
  if [ ! -f "$pidfile" ]; then
    echo "[stop-local] $name: sem pidfile (nada a parar)"
    return 0
  fi
  pid="$(cat "$pidfile")"
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "[stop-local] $name: pid $pid já não existe"
    rm -f "$pidfile"
    return 0
  fi
  if ! tr '\0' ' ' < "/proc/$pid/cmdline" 2>/dev/null | grep -q -- "$token"; then
    echo "[stop-local] $name: pid $pid NÃO corresponde a $token; não será encerrado (verificar manualmente)" >&2
    return 1
  fi
  # SIGTERM apenas no PID gravado (sem pkill, sem kill ao grupo). Permite o encerramento gracioso (CA-07).
  kill -TERM "$pid"
  while [ "$waited" -lt "$WAIT_TERM_S" ]; do
    if ! kill -0 "$pid" 2>/dev/null; then
      echo "[stop-local] $name: encerrado com SIGTERM (pid $pid, ${waited}s)"
      rm -f "$pidfile"
      return 0
    fi
    sleep 1
    waited=$((waited + 1))
  done
  if kill -0 "$pid" 2>/dev/null; then
    echo "[stop-local] AVISO: $name pid $pid não encerrou em ${WAIT_TERM_S}s após SIGTERM (espera de 10 s é proposta OPS). Enviando SIGKILL ao PID específico." >&2
    if tr '\0' ' ' < "/proc/$pid/cmdline" 2>/dev/null | grep -q -- "$token"; then
      kill -KILL "$pid" 2>/dev/null || true
      sleep 0.5
    else
      echo "[stop-local] $name: pid $pid já não confere com $token; SIGKILL não enviado." >&2
      return 1
    fi
  fi
  if kill -0 "$pid" 2>/dev/null; then
    echo "[stop-local] ERRO: $name pid $pid permanece após SIGKILL. Sem pkill genérico; escalar." >&2
    return 1
  fi
  echo "[stop-local] $name: encerrado com SIGKILL (pid $pid)"
  rm -f "$pidfile"
  return 0
}

echo "[stop-local] $(ts) parando portal-web e portal-api (SIGTERM, espera ${WAIT_TERM_S}s proposta, SIGKILL só se necessário)"
# Folha primeiro (quem escuta), depois o wrapper (npm/dotnet run), se ainda existir.
if ! stop_one web "$RUN_DIR/web.pid" "ng serve"; then rc=1; fi
if ! stop_one web-lancador "$RUN_DIR/web.launcher.pid" "npm"; then
  # o lançador pode ser npx ou npm exec; ambos têm "npm" no cmdline usual. Se o token falhar, tenta npx.
  if ! stop_one web-lancador "$RUN_DIR/web.launcher.pid" "npx"; then rc=1; fi
fi
if ! stop_one api "$RUN_DIR/api.pid" "Portal.Api"; then
  if ! stop_one api "$RUN_DIR/api.pid" "dotnet"; then rc=1; fi
fi
if ! stop_one api-lancador "$RUN_DIR/api.launcher.pid" "dotnet"; then rc=1; fi

port_busy() { ss -ltn "sport = :$1" 2>/dev/null | tail -n +2 | grep -q .; }
for v in PORTAL_API_PORT PORTAL_WEB_PORT; do
  p="${!v:-}"
  if [ -z "$p" ]; then
    continue
  fi
  if port_busy "$p"; then
    echo "[stop-local] ALERTA: porta de $v ainda escuta (processo órfão? sem pkill genérico; verificar manualmente)" >&2
    rc=1
  fi
done
T1=$(date +%s%N)
if [ "$rc" -eq 0 ]; then
  resultado=OK
else
  resultado=NOK
fi
echo "[stop-local] $(ts) duração=$(( (T1 - T0) / 1000000 )) ms resultado=$resultado"
exit $rc

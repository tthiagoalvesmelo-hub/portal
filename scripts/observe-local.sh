#!/usr/bin/env bash
# scripts/observe-local.sh — janela de observação R3 do G7 (parecer OPS §4.3)
# Amostra GET /health em loopback a cada INTERVALO s durante DURACAO s (padrão 900 s / 86400 s = 24 h).
# Uma linha por amostra (TSV), checagem humana de carga/PIDs no início, a cada 4 h e no fim.
# Sem painel, sem alerta (lacuna consciente: ADR-0001 v2 D5).
# Uso: scripts/observe-local.sh [--intervalo S] [--duracao S]
# Saída: evidencias/observacao/<ts>-<sha7>/ (gitignorada)
set -uo pipefail

INTERVALO=900
DURACAO=86400
while [ $# -gt 0 ]; do
  case "$1" in
    --intervalo) INTERVALO="$2"; shift 2;;
    --duracao) DURACAO="$2"; shift 2;;
    *) echo "arg inválido: $1" >&2; exit 2;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1
if [ ! -f .env ]; then
  echo "sem .env" >&2
  exit 1
fi
set -a
# shellcheck disable=SC1091
. ./.env
set +a
URL="http://${PORTAL_API_HOST}:${PORTAL_API_PORT}/health"
SHA="$(git rev-parse HEAD)"
EV="evidencias/observacao/$(TZ=America/Sao_Paulo date '+%Y-%m-%dT%H%M')-03-${SHA:0:7}"
mkdir -p "$EV"
ts() { TZ=America/Sao_Paulo date '+%Y-%m-%d %H:%M:%S'; }

{
  echo "inicio: $(ts) America/Sao_Paulo"
  echo "git_head: $SHA"
  echo "dotnet: $(dotnet --version 2>/dev/null || echo ausente)"
  echo "node: $(node -v)"
  echo "configuracao_api: $(cat run/api.configuration 2>/dev/null || echo nao-registrada)"
  echo "intervalo_s: $INTERVALO"
  echo "duracao_s: $DURACAO"
  echo "alvo: GET /health em loopback (valores de ambiente não gravados)"
} > "$EV/meta.txt"
printf 'hora\thttp\ttempo_s\tstatus_corpo\tpid_api\tpid_web\n' > "$EV/amostras.tsv"

checagem() {
  {
    echo "--- checagem '$1' em $(ts)"
    uptime
    for n in api web; do
      pid="$(cat "run/$n.pid" 2>/dev/null || true)"
      if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
        ps -o pid=,pcpu=,rss=,etime= -p "$pid" | sed "s/^/$n /"
      else
        echo "$n: processo AUSENTE"
      fi
    done
  } >> "$EV/checagens.txt"
}

END=$(( $(date +%s) + DURACAO ))
NEXT_CHECK=$(( $(date +%s) + 14400 ))
FALHAS=0
N=0
BODY="/tmp/.obs-body.$$"
checagem inicio
while [ "$(date +%s)" -lt "$END" ]; do
  N=$((N+1))
  OUT="$(curl -sS -o "$BODY" -w '%{http_code} %{time_total}' --max-time 6 "$URL" 2>/dev/null || echo '000 0')"
  CODE="${OUT%% *}"
  TT="${OUT##* }"
  ST="$(python3 -c 'import json,sys
try:
    print(json.load(open(sys.argv[1])).get("status","?"))
except Exception:
    print("?")
' "$BODY" 2>/dev/null || echo '?')"
  PA="$(cat run/api.pid 2>/dev/null || echo -)"
  PW="$(cat run/web.pid 2>/dev/null || echo -)"
  if kill -0 "$PA" 2>/dev/null; then KA=vivo; else KA=MORTO; fi
  if kill -0 "$PW" 2>/dev/null; then KW=vivo; else KW=MORTO; fi
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$(ts)" "$CODE" "$TT" "$ST" "$KA" "$KW" >> "$EV/amostras.tsv"
  if [ "$CODE" != "200" ] || [ "$KA" != "vivo" ] || [ "$KW" != "vivo" ]; then
    FALHAS=$((FALHAS+1))
  fi
  if [ "$(date +%s)" -ge "$NEXT_CHECK" ]; then
    checagem "4h"
    NEXT_CHECK=$(( $(date +%s) + 14400 ))
  fi
  sleep "$INTERVALO"
done
rm -f "$BODY"
checagem fim
if [ "$FALHAS" -eq 0 ]; then
  veredito=OK
else
  veredito=NOK
fi
{
  echo "fim: $(ts)"
  echo "amostras: $N"
  echo "amostras_com_falha: $FALHAS"
  echo "veredito_amostragem: $veredito"
  echo "observacao: veredito final do G7 inclui julgamento humano das checagens de CPU/memória (sem limiar numérico definido) — OPS."
} >> "$EV/meta.txt"
echo "[observe-local] $N amostras, $FALHAS com falha. Evidência em $EV (gitignorada)."
[ "$FALHAS" -eq 0 ]

#!/usr/bin/env bash
# scripts/check-version.sh — DEC-ARQ-0007 / ADR-0002 v1.1:
# <Version> do .csproj == tag portal-api-v<versão> == GET /health
# Uso:
#   scripts/check-version.sh                    # exige as três pontas (G6, quando houver tag)
#   scripts/check-version.sh --adiar-se-sem-tag # no verify-local, antes da 1ª tag
set -euo pipefail

die() { echo "[check-version] ERRO: $*" >&2; exit 1; }

ADIAR=0
if [ "${1:-}" = "--adiar-se-sem-tag" ]; then
  ADIAR=1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || die "não foi possível entrar na raiz do monorepo."
CSPROJ="apps/portal-api/src/Portal.Api/Portal.Api.csproj"
if [ ! -f "$CSPROJ" ]; then
  die "ausente $CSPROJ"
fi

# Lê o elemento <Version> do csproj (fonte única, ADR-0002 v1.1). Sem gravar segredos.
VERSAO="$(python3 -c '
import re, sys
text = open(sys.argv[1], encoding="utf-8").read()
ms = re.findall(r"<Version>([^<]+)</Version>", text)
if not ms:
    sys.stderr.write("elemento <Version> nao encontrado no csproj\n")
    sys.exit(1)
print(ms[0].strip())
' "$CSPROJ")"
TAG="portal-api-v${VERSAO}"
SHA="$(git rev-parse HEAD)"
echo "[check-version] manifesto_csproj=${VERSAO} tag_esperada=${TAG} head=${SHA:0:7}"

if ! git rev-parse -q --verify "refs/tags/${TAG}" >/dev/null; then
  if [ "$ADIAR" -eq 1 ]; then
    echo "[check-version] sem tag ${TAG}: conferência de tag e 3ª ponta ADIADAS para o G6."
    exit 0
  fi
  die "tag ${TAG} não existe."
fi
TAG_SHA="$(git rev-list -n1 "${TAG}")"
if [ "$TAG_SHA" != "$SHA" ]; then
  die "tag ${TAG} aponta para ${TAG_SHA}, não para HEAD ${SHA}"
fi
echo "[check-version] manifesto == tag no HEAD: OK"

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi
if [ -n "${PORTAL_API_HOST:-}" ] && [ -n "${PORTAL_API_PORT:-}" ]; then
  if RESP="$(curl -fsS --max-time 3 "http://${PORTAL_API_HOST}:${PORTAL_API_PORT}/health" 2>/dev/null)"; then
    V_HEALTH="$(printf '%s' "$RESP" | python3 -c "import sys,json; print(json.load(sys.stdin).get(\"version\",\"\"))")"
    if [ "$V_HEALTH" != "$VERSAO" ]; then
      die "DIVERGÊNCIA: /health.version=${V_HEALTH} manifesto=${VERSAO}"
    fi
    echo "[check-version] GET /health.version == manifesto: OK"
  else
    if [ "$ADIAR" -eq 1 ]; then
      echo "[check-version] serviço não está no ar: 3ª ponta ADIADA para o G6."
      exit 0
    fi
    die "GET /health não respondeu (G6 exige as três pontas)."
  fi
else
  if [ "$ADIAR" -eq 1 ]; then
    echo "[check-version] host/porta não disponíveis: 3ª ponta ADIADA para o G6."
    exit 0
  fi
  die "PORTAL_API_HOST/PORT ausentes no .env."
fi

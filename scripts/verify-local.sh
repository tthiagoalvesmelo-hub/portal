#!/usr/bin/env bash
# scripts/verify-local.sh — DEC-ARQ-0007 (substitui DEC-ARQ-0003): substituto local do "CI verde" para .NET + Angular
# Também cobre lint (0008) e SCA/SAST recortado (0009) que a 0007 executa.
# Uso: scripts/verify-local.sh [--papel autor|cr]   (cr = reexecução do Code Review; sufixo na evidência)
# Fail fast. Flags exatamente como a DEC-ARQ-0007 v1.1 vigente (compatível com a v1.2 ainda sem merge no PR #4).
# Não imprime valores de .env. Evidência gitignorada em evidencias/verify/.
# Pré-requisito: rede para NuGet Audit (A1) e npm audit (W5); sem rede, A1/W5 não valem como evidência.
set -euo pipefail

# Carrega o toolchain local da box, se existir (DOTNET_ROOT, PATH, telemetria).
if [ -f "${HOME}/.config/ops-toolchain.sh" ]; then
  # shellcheck disable=SC1091
  . "${HOME}/.config/ops-toolchain.sh"
fi

die() { echo "[verify-local] ERRO: $*" >&2; exit 1; }

PAPEL="autor"
if [ "${1:-}" = "--papel" ]; then
  PAPEL="${2:-autor}"
  shift 2
fi
case "$PAPEL" in
  autor|cr) ;;
  *) echo "papel inválido (autor|cr)" >&2; exit 2;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || die "não foi possível entrar na raiz do monorepo."
TS_DIR="$(TZ=America/Sao_Paulo date '+%Y-%m-%dT%H%M')-03"
SHA_FULL="$(git rev-parse HEAD)"
EV="evidencias/verify/${TS_DIR}-${SHA_FULL:0:7}-${PAPEL}"
mkdir -p "$EV"

sanitize() { sed -e "s#${HOME}#/home/<usuario>#g" -e "s#${ROOT}#<raiz>#g"; }
exec > >(sanitize | tee "$EV/log.txt") 2>&1

API_DIR="apps/portal-api"
API_CSPROJ="$API_DIR/src/Portal.Api/Portal.Api.csproj"
WEB_DIR="apps/portal-web"

finish() {
  local rc=$?
  local resultado
  if [ "$rc" -eq 0 ]; then
    resultado=OK
  else
    resultado=NOK
  fi
  {
    echo "demanda: DEM-2026-001"
    echo "task: TASK-0008/TASK-0012 (verify local, DEC-ARQ-0007 v1.1 vigente)"
    echo "papel: $PAPEL"
    echo "data_hora: $(TZ=America/Sao_Paulo date '+%Y-%m-%d %H:%M:%S %Z')"
    echo "git_head: $SHA_FULL"
    echo "git_status_porcelain_linhas: $(git status --porcelain | wc -l)"
    echo "dotnet: $(dotnet --version 2>/dev/null || echo ausente)"
    echo "node: $(node -v)"
    echo "npm: $(npm -v)"
    echo "codigo_saida: $rc"
    echo "resultado: $resultado"
  } > "$EV/meta.txt"
  echo "[verify-local] resultado=$resultado evidência em $EV (gitignorada)"
  exit "$rc"
}
trap finish EXIT

step() { echo; echo "=== $(TZ=America/Sao_Paulo date '+%H:%M:%S') $*"; }

step "pré-condições"
if [ -n "$(git status --porcelain)" ]; then
  echo "árvore de trabalho NÃO está limpa; o verify vale para um commit exato."
  git status --short
  exit 1
fi
command -v dotnet >/dev/null || die "dotnet não encontrado."
case "$(dotnet --version)" in
  10.*) echo "dotnet $(dotnet --version) (série 10 OK)";;
  *) die "SDK .NET série 10 exigida; encontrado $(dotnet --version)";;
esac
case "$(node -v)" in
  v24.*) echo "node $(node -v) (série 24 OK)";;
  *) die "Node série 24 exigida; encontrado $(node -v)";;
esac
if [ ! -f "$API_CSPROJ" ]; then
  die "ausente $API_CSPROJ."
fi
if [ ! -f "$WEB_DIR/package.json" ] || [ ! -f "$WEB_DIR/package-lock.json" ]; then
  die "ausente package.json ou package-lock.json em $WEB_DIR (DEC-ARQ-0007 W1)."
fi

# Alvo dos comandos dotnet: solução (.slnx/.sln) se existir, senão o csproj.
API_TARGET="$API_CSPROJ"
if [ -f "$API_DIR/PortalApi.sln" ]; then
  API_TARGET="$API_DIR/PortalApi.sln"
elif [ -f "$API_DIR/PortalApi.slnx" ]; then
  API_TARGET="$API_DIR/PortalApi.slnx"
else
  found="$(find "$API_DIR" -maxdepth 1 \( -name '*.sln' -o -name '*.slnx' \) | head -n 1 || true)"
  if [ -n "$found" ]; then
    API_TARGET="$found"
  fi
fi
echo "alvo_dotnet: $API_TARGET"

# ---- API A1–A5 (DEC-ARQ-0007) ----
step "A1 dotnet restore --locked-mode (NuGet Audit no restore, propriedades no Directory.Build.props do FS)"
dotnet restore --locked-mode "$API_TARGET"

step "A2 dotnet format --verify-no-changes (DEC-ARQ-0008)"
# Ordem do README do monorepo: projeto primeiro, depois a opção.
dotnet format "$API_TARGET" --verify-no-changes

step "A3 dotnet build -c Release --no-restore -warnaserror"
dotnet build "$API_TARGET" -c Release --no-restore -warnaserror

step "A4 dotnet test -c Release --no-build + Coverlet ≥80% linhas (flags da DEC-ARQ-0007)"
dotnet test "$API_TARGET" -c Release --no-build \
  /p:CollectCoverage=true \
  /p:Threshold=80 \
  /p:ThresholdType=line \
  /p:ThresholdStat=total \
  /p:CoverletOutputFormat=cobertura

step "A5 dotnet list package --vulnerable --include-transitive --format json --output-version 1"
# O código de saída, por si só, não decide (DEC-ARQ-0007 v1.1; medido na v1.2 no SDK 10.0.404).
A5_JSON="$EV/nuget-vulnerable.json"
A5_ERR="$EV/nuget-vulnerable.stderr.txt"
set +e
dotnet list "$API_TARGET" package --vulnerable --include-transitive --format json --output-version 1 >"$A5_JSON" 2>"$A5_ERR"
A5_RC=$?
set -e
if [ -s "$A5_ERR" ]; then
  echo "A5 stderr (sanitizado no log):"
  cat "$A5_ERR"
fi
python3 - "$A5_JSON" "$A5_RC" << 'PY'
import json, sys
path, rc_s = sys.argv[1], sys.argv[2]
rc = int(rc_s)
raw = open(path, encoding="utf-8").read().strip()
if not raw:
    sys.stderr.write("A5: saída JSON vazia (código de saída %s). Falha de execução.\n" % rc)
    sys.exit(1)
try:
    data = json.loads(raw)
except json.JSONDecodeError as e:
    sys.stderr.write("A5: JSON inválido (%s); código de saída %s. Falha de execução.\n" % (e, rc))
    sys.exit(1)

def pkgs(fw, key):
    return fw.get(key) or []

high = []
report = []
for proj in (data.get("projects") or []):
    for fw in (proj.get("frameworks") or []):
        for key in ("topLevelPackages", "transitivePackages"):
            for pkg in pkgs(fw, key):
                ident = pkg.get("id") or "?"
                ver = pkg.get("resolvedVersion") or pkg.get("requestedVersion") or "?"
                for vul in (pkg.get("vulnerabilities") or []):
                    sev = str(vul.get("severity") or "")
                    rec = "%s %s %s (%s)" % (ident, ver, sev, key)
                    report.append(rec)
                    if sev.lower() in ("high", "critical"):
                        high.append(rec)
if report:
    print("A5: vulnerabilidades listadas:")
    for r in report:
        print("  -", r)
else:
    print("A5: nenhuma vulnerabilidade listada no JSON.")
if high:
    sys.stderr.write("A5: a saída JSON lista severidade High ou Critical:\n")
    for r in high:
        sys.stderr.write("  - %s\n" % r)
    sys.exit(1)
print("A5: nenhuma severidade High/Critical no JSON (código de saída do dotnet list: %s; não usado como veredito de vulnerabilidade)." % rc)
# Código ≠ 0 só reprova se for falha de execução; aqui o JSON foi válido e sem High/Critical.
if rc != 0:
    sys.stderr.write("A5: código de saída %s com JSON válido e sem High/Critical — tratado como êxito da listagem (oráculo é o JSON).\n" % rc)
PY

# ---- Web W1–W5 (DEC-ARQ-0007) ----
step "W1 npm ci (package-lock.json obrigatório)"
# npm 11 pode bloquear o script opcional do msgpackr-extract; build e testes devem passar sem ele (registro do FS).
(cd "$WEB_DIR" || exit 1; npm ci)
NG=(npx --no-install ng)

step "W2 ng lint (DEC-ARQ-0008; zero avisos)"
(cd "$WEB_DIR" || exit 1; "${NG[@]}" lint)

step "W3 ng test --no-watch --coverage (≥80% linhas; limiar no angular.json, @vitest/coverage-v8)"
(cd "$WEB_DIR" || exit 1; "${NG[@]}" test --no-watch --coverage)

step "W4 ng build (configuração de produção; defaultConfiguration=production no angular.json)"
(cd "$WEB_DIR" || exit 1; "${NG[@]}" build)

step "W5 npm audit --audit-level=high"
(cd "$WEB_DIR" || exit 1; npm audit --audit-level=high)

step "check-version (ADR-0002 / DEC-ARQ-0007: só quando houver tag; senão adiado para o G6)"
if [ -x "$ROOT/scripts/check-version.sh" ]; then
  "$ROOT/scripts/check-version.sh" --adiar-se-sem-tag
else
  echo "scripts/check-version.sh ainda não depositado; conferência de versão no G6."
fi
echo
echo "[verify-local] todas as etapas aplicáveis passaram."

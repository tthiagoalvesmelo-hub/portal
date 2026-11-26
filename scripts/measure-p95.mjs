#!/usr/bin/env node
// scripts/measure-p95.mjs — DEC-ARQ-0010 (substitui DEC-ARQ-0006)
// Mede GET /health em loopback com fetch global do Node 24. Sem pacote novo, sem autocannon.
// A API deve ja estar no ar em Release (start-local.sh). 50 requisicoes de aquecimento (descartadas) + 200 sequenciais, concorrencia 1.
// Oraculo indicativo: p95 <= 100 ms (nao e SLO e nao bloqueia aceite funcional).
// Evidencia: evidencias/p95/<AAAA-MM-DDTHHMM-03>-<sha7>/  (gitignorada, sanitizada).
import { readFileSync, writeFileSync, mkdirSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { homedir } from "node:os";

const WARMUP = 50;
const MEASURE = 200;
const ORACLE_MS = 100;

function die(msg) {
  console.error(`[measure-p95] ERRO: ${msg}`);
  process.exit(1);
}
function sh(cmd, args) {
  return execFileSync(cmd, args, { encoding: "utf8" }).trim();
}
function tzdate(fmt) {
  return execFileSync("date", [`+${fmt}`], {
    encoding: "utf8",
    env: { ...process.env, TZ: "America/Sao_Paulo" },
  }).trim();
}
function sanitize(text) {
  return String(text).replaceAll(homedir(), "/home/<usuario>");
}
function loadEnv(path) {
  const out = {};
  for (const raw of readFileSync(path, "utf8").split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("#")) continue;
    const i = line.indexOf("=");
    if (i <= 0) continue;
    out[line.slice(0, i)] = line.slice(i + 1);
  }
  return out;
}
function percentile(sorted, q) {
  if (sorted.length === 0) return null;
  const idx = Math.ceil((q / 100) * sorted.length) - 1;
  return sorted[Math.max(0, Math.min(sorted.length - 1, idx))];
}

if (!existsSync(".env")) die(".env ausente (fora do git).");
const env = loadEnv(".env");
for (const k of ["PORTAL_API_HOST", "PORTAL_API_PORT"]) {
  if (!env[k]) die("variavel " + k + " ausente ou vazia no .env");
}
if (env.PORTAL_API_HOST !== "127.0.0.1") die("medicao so em loopback (127.0.0.1).");
const url = "http://" + env.PORTAL_API_HOST + ":" + env.PORTAL_API_PORT + "/health";

if (!existsSync("run/api.configuration") || readFileSync("run/api.configuration", "utf8").trim() !== "Release") {
  die("API nao esta registrada como Release (rode scripts/start-local.sh; DEC-ARQ-0010).");
}

const sha = sh("git", ["rev-parse", "HEAD"]);
const nowSP = tzdate("%Y-%m-%dT%H%M") + "-03";
const ev = "evidencias/p95/" + nowSP + "-" + sha.slice(0, 7);
mkdirSync(ev, { recursive: true });

async function one() {
  const t0 = performance.now();
  try {
    const res = await fetch(url, { method: "GET" });
    const ms = performance.now() - t0;
    return { ok: true, status: res.status, ms };
  } catch {
    const ms = performance.now() - t0;
    return { ok: false, status: 0, ms };
  }
}

const warm = [];
for (let i = 0; i < WARMUP; i++) warm.push(await one());
const samples = [];
for (let i = 0; i < MEASURE; i++) samples.push(await one());

const times = samples.map((s) => s.ms).sort((a, b) => a - b);
const errors = samples.filter((s) => !s.ok || s.status !== 200).length;
const statusDist = {};
for (const s of samples) {
  const key = String(s.status);
  statusDist[key] = (statusDist[key] || 0) + 1;
}
const min = times[0];
const max = times[times.length - 1];
const p50 = percentile(times, 50);
const p95v = percentile(times, 95);
const p99 = percentile(times, 99);
const oracle = p95v <= ORACLE_MS && errors === 0;
const mem = sh("awk", ["/MemAvailable:/ {printf \"%.0f\", $2}", "/proc/meminfo"]);

const meta = [
  "demanda: DEM-2026-001",
  "task: TASK-0010/TASK-0012 (p95 indicativo, DEC-ARQ-0010)",
  "data_hora: " + tzdate("%Y-%m-%d %H:%M:%S %Z"),
  "git_head: " + sha,
  "dotnet: " + sh("dotnet", ["--version"]),
  "node: " + sh("node", ["-v"]),
  "configuracao: Release",
  "nproc: " + sh("nproc", []),
  "uptime: " + sh("uptime", []),
  "memoria_disponivel_kb: " + mem,
  "alvo: GET /health em loopback (valores de ambiente nao gravados)",
  "aquecimento: " + WARMUP + " (descartadas)",
  "medicao: " + MEASURE + " sequenciais, concorrencia 1",
  "min_ms: " + min.toFixed(3),
  "p50_ms: " + p50.toFixed(3),
  "p95_ms: " + p95v.toFixed(3),
  "p99_ms: " + p99.toFixed(3),
  "max_ms: " + max.toFixed(3),
  "erros: " + errors,
  "distribuicao_status: " + JSON.stringify(statusDist),
  "oraculo: p95 <= " + ORACLE_MS + " ms (indicativo; nao SLO)",
  "resultado_oraculo: " + (oracle ? "OK" : "NOK"),
  "declaracao: ausencia de outro ensaio pesado no momento da medicao — operador OPS/QA",
  "primeira_medicao: vira linha de base provisoria",
].join("\n") + "\n";

writeFileSync(ev + "/meta.txt", sanitize(meta));
writeFileSync(
  ev + "/amostras.json",
  JSON.stringify({
    warmup_descartado: warm.length,
    amostras: samples.map((s) => ({ status: s.status, ms: Number(s.ms.toFixed(3)) })),
  }, null, 2),
);
console.log(
  "[measure-p95] p95=" + p95v.toFixed(3) + " ms erros=" + errors +
  " resultado=" + (oracle ? "OK" : "NOK") + " evidencia em " + ev + " (gitignorada)",
);
process.exit(oracle ? 0 : 1);

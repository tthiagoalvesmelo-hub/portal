/**
 * Lógica de apresentação da tela de status (US-0002; UX-0001 v1.1), separada do DOM.
 * Leitor tolerante: campos desconhecidos são ignorados e nunca exibidos.
 * Qualquer resposta que não seja HTTP 200 com corpo válido e status "ok" vira erro (RN-0002).
 */

export type HealthView =
  | { readonly kind: 'carregando' }
  | {
      readonly kind: 'saudavel';
      readonly status: string;
      readonly version: string;
      readonly timestamp: string;
      readonly localTime: string;
    }
  | { readonly kind: 'erro' };

export const LOADING: HealthView = { kind: 'carregando' };
export const ERROR: HealthView = { kind: 'erro' };

/** Textos constantes do front (UX-0001 §7). Mensagens de erro nunca derivam da resposta. */
export const STATUS_TEXTS = {
  pageTitle: 'Status da API',
  intro: 'Situação da API base do Portal Web, consultada ao abrir ou atualizar esta tela.',
  cardLabel: 'Resultado da consulta de status',
  loadingTitle: 'Verificando status da API…',
  loadingHelp: 'Aguarde a resposta. Nenhum valor é exibido até a consulta terminar.',
  loadingAnnouncement: 'Verificando status da API.',
  healthyTitle: 'API operacional',
  healthyBadge: 'Saudável',
  healthyHelp: 'A API respondeu normalmente à última consulta.',
  errorTitle: 'Status indisponível',
  errorBadge: 'Erro',
  errorMessage:
    'Não foi possível obter o status da API. Até que uma nova consulta seja concluída com sucesso, não considere a API operacional.',
  errorHint: 'Use “Atualizar status” para tentar novamente.',
  errorAnnouncement: 'Status indisponível. Não foi possível obter o status da API.',
  labelStatus: 'Status (valor retornado)',
  labelVersion: 'Versão',
  labelTimestamp: 'Data e hora da resposta',
  localTimeLegend: 'horário de Brasília (UTC−03:00)',
  utcPrefix: 'UTC:',
  refresh: 'Atualizar status',
} as const;

/** ServiceVersion do contrato (SemVer sem metadados de build). Em JS, `$` sem flag m só casa no fim da entrada. */
export const VERSION_PATTERN =
  /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-((0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?$/;

/** Timestamp do contrato: UTC com Z e exatamente 3 casas de milissegundos. */
export const TIMESTAMP_PATTERN =
  /^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])T([01]\d|2[0-3]):[0-5]\d:[0-5]\d\.\d{3}Z$/;

export function healthyAnnouncement(version: string): string {
  return `API operacional. Versão ${version}.`;
}

/** pattern + data real do calendário (rejeita, por exemplo, 31/02). */
export function isValidTimestamp(value: string): boolean {
  if (!TIMESTAMP_PATTERN.test(value)) {
    return false;
  }
  const parsed = new Date(value);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString() === value;
}

/** Conversão só de exibição: UTC ⇒ America/Sao_Paulo no formato dd/MM/aaaa HH:mm:ss. */
export function formatSaoPaulo(isoUtc: string): string {
  const parts = new Intl.DateTimeFormat('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(isoUtc));
  const part = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((p) => p.type === type)?.value ?? '';
  return `${part('day')}/${part('month')}/${part('year')} ${part('hour')}:${part('minute')}:${part('second')}`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Interpreta a resposta de GET /health. 503 válido (caso b) e qualquer desvio (caso a) ⇒ erro. */
export function interpretResponse(httpStatus: number, bodyText: string): HealthView {
  if (httpStatus !== 200) {
    return ERROR;
  }
  let body: unknown;
  try {
    body = JSON.parse(bodyText);
  } catch {
    return ERROR;
  }
  if (!isRecord(body)) {
    return ERROR;
  }
  const { status, version, timestamp } = body;
  if (
    status !== 'ok' ||
    typeof version !== 'string' ||
    !VERSION_PATTERN.test(version) ||
    typeof timestamp !== 'string' ||
    !isValidTimestamp(timestamp)
  ) {
    return ERROR;
  }
  return { kind: 'saudavel', status, version, timestamp, localTime: formatSaoPaulo(timestamp) };
}

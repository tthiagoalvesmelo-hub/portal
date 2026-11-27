import { InjectionToken } from '@angular/core';

/**
 * Endereço base da portal-api (ADR-0001 v2.0 D7: PORTAL_API_BASE_URL).
 * Injetado no build/serve com `--define "PORTAL_API_BASE_URL=\"http://...\""`; nunca versionado com valor.
 * Ausente ou inválido ⇒ null ⇒ a tela fica no estado de erro (nunca assume "saudável").
 */
declare const PORTAL_API_BASE_URL: string | undefined;

export const API_BASE_URL = new InjectionToken<string | null>('API_BASE_URL');

/** Aceita só URL absoluta http/https, sem credenciais, query ou fragmento; devolve sem barra final. */
export function parseApiBaseUrl(value: unknown): string | null {
  if (typeof value !== 'string' || value.trim() === '' || !URL.canParse(value)) {
    return null;
  }
  const url = new URL(value);
  const allowedScheme = url.protocol === 'http:' || url.protocol === 'https:';
  const clean = url.username === '' && url.password === '' && url.search === '' && url.hash === '';
  if (!allowedScheme || !clean || value.includes('?') || value.includes('#')) {
    return null;
  }
  return url.href.replace(/\/+$/, '');
}

/** Leitura única do valor definido no build (usada pela fábrica do provedor em app.config.ts). */
export function readDefinedApiBaseUrl(): string | null {
  return parseApiBaseUrl(
    typeof PORTAL_API_BASE_URL === 'undefined' ? undefined : PORTAL_API_BASE_URL,
  );
}

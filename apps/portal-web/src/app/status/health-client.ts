import { Injectable, InjectionToken, inject } from '@angular/core';
import { API_BASE_URL } from '../config/api-base-url';
import { ERROR, HealthView, interpretResponse } from './health-status';

/** fetch injetável para os testes (sem alterar o comportamento de produção). */
export const FETCH = new InjectionToken<typeof fetch>('FETCH', {
  providedIn: 'root',
  factory: () => globalThis.fetch.bind(globalThis),
});

/** Tempo limite do cliente (DEC-AS-0001): 5 s, sem nova tentativa automática. */
export const HEALTH_TIMEOUT_MS = 5000;

@Injectable({ providedIn: 'root' })
export class HealthClient {
  private readonly fetchFn = inject(FETCH);
  private readonly baseUrl = inject(API_BASE_URL);

  /** Uma única requisição GET simples (sem cabeçalhos extras, logo sem preflight), sem cache. */
  async check(): Promise<HealthView> {
    if (this.baseUrl === null) {
      return ERROR;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), HEALTH_TIMEOUT_MS);
    try {
      const response = await this.fetchFn(`${this.baseUrl}/health`, {
        method: 'GET',
        cache: 'no-store',
        credentials: 'omit',
        signal: controller.signal,
      });
      const text = await response.text();
      return interpretResponse(response.status, text);
    } catch {
      return ERROR;
    } finally {
      clearTimeout(timer);
    }
  }
}

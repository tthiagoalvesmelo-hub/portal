import { HEALTH_TIMEOUT_MS } from './health-client';

/** Exemplos derivados do openapi 1.0.0 (stack-conhecimento, SHA e34e6b9): saudavel e indisponivel. */
export const CONTRACT_OK = {
  status: 'ok',
  version: '0.1.0',
  timestamp: '2026-11-26T15:30:00.123Z',
};
export const CONTRACT_UNAVAILABLE = {
  status: 'unavailable',
  version: '0.1.0',
  timestamp: '2026-11-26T15:30:05.456Z',
};

export interface FakeCall {
  readonly url: string;
  readonly init: RequestInit | undefined;
}

/** Resposta pendente controlada pelo teste (permite inspecionar o estado Carregando). */
export class Deferred {
  resolve!: (response: Response) => void;
  reject!: (reason: unknown) => void;
  readonly promise = new Promise<Response>((resolve, reject) => {
    this.resolve = resolve;
    this.reject = reject;
  });
}

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(typeof body === 'string' ? body : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
  });
}

/** fetch falso: cada chamada consome a próxima resposta da fila (Response, Deferred ou erro). */
export function fakeFetch(queue: (Response | Deferred | Error | 'pendurar')[]): {
  fn: typeof fetch;
  calls: FakeCall[];
} {
  const calls: FakeCall[] = [];
  const fn = ((input: RequestInfo | URL, init?: RequestInit) => {
    calls.push({ url: String(input), init });
    const next = queue.shift();
    if (next === undefined) {
      return Promise.reject(new Error('fila vazia'));
    }
    if (next === 'pendurar') {
      return new Promise<Response>((_, reject) => {
        init?.signal?.addEventListener('abort', () =>
          reject(new DOMException('abortado', 'AbortError')),
        );
      });
    }
    if (next instanceof Deferred) {
      return next.promise;
    }
    return next instanceof Error ? Promise.reject(next) : Promise.resolve(next);
  }) as typeof fetch;
  return { fn, calls };
}

export { HEALTH_TIMEOUT_MS };

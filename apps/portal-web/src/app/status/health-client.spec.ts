import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-base-url';
import { FETCH, HealthClient } from './health-client';
import { ERROR } from './health-status';
import { CONTRACT_OK, HEALTH_TIMEOUT_MS, fakeFetch, jsonResponse } from './testing';

function setup(baseUrl: string | null, queue: Parameters<typeof fakeFetch>[0]) {
  const fake = fakeFetch(queue);
  TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: baseUrl },
      { provide: FETCH, useValue: fake.fn },
    ],
  });
  return { client: TestBed.inject(HealthClient), calls: fake.calls };
}

describe('HealthClient', () => {
  afterEach(() => vi.useRealTimers());

  it('CT-0026 — GET simples em <base>/health, sem cache e sem cabeçalhos extras', async () => {
    const { client, calls } = setup('http://127.0.0.1:8080', [jsonResponse(200, CONTRACT_OK)]);

    const view = await client.check();

    expect(view.kind).toBe('saudavel');
    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe('http://127.0.0.1:8080/health');
    expect(calls[0].init?.method).toBe('GET');
    expect(calls[0].init?.cache).toBe('no-store');
    expect(calls[0].init?.headers).toBeUndefined();
  });

  it('CT-0028 — sem resposta em 5 s: abortada, exatamente uma requisição, estado de erro', async () => {
    vi.useFakeTimers();
    const { client, calls } = setup('http://127.0.0.1:8080', ['pendurar']);

    const pending = client.check();
    await vi.advanceTimersByTimeAsync(HEALTH_TIMEOUT_MS - 1);
    expect(calls[0].init?.signal?.aborted).toBe(false);
    await vi.advanceTimersByTimeAsync(1);

    await expect(pending).resolves.toEqual(ERROR);
    expect(calls[0].init?.signal?.aborted).toBe(true);
    expect(calls).toHaveLength(1);
  });

  it('CT-0028 — falha de rede vira erro', async () => {
    const { client } = setup('http://127.0.0.1:8080', [new TypeError('Failed to fetch')]);

    await expect(client.check()).resolves.toEqual(ERROR);
  });

  it('CT-0028 — base URL ausente ou inválida: erro sem requisição', async () => {
    const { client, calls } = setup(null, []);

    await expect(client.check()).resolves.toEqual(ERROR);
    expect(calls).toHaveLength(0);
  });
});

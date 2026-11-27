import { ERROR, formatSaoPaulo, interpretResponse, isValidTimestamp } from './health-status';
import { CONTRACT_OK, CONTRACT_UNAVAILABLE } from './testing';

describe('health-status (lógica de apresentação)', () => {
  it('CT-0026 — 200 do contrato com campo desconhecido: valores brutos preservados e campo ignorado', () => {
    const view = interpretResponse(200, JSON.stringify({ ...CONTRACT_OK, extra: 'ignorar' }));

    expect(view).toEqual({
      kind: 'saudavel',
      status: 'ok',
      version: '0.1.0',
      timestamp: '2026-11-26T15:30:00.123Z',
      localTime: '26/11/2026 12:30:00',
    });
  });

  it('CT-0026 — conversão adicional para America/Sao_Paulo no formato dd/MM/aaaa HH:mm:ss', () => {
    expect(formatSaoPaulo('2026-11-26T15:34:56.789Z')).toBe('26/11/2026 12:34:56');
    expect(formatSaoPaulo('2026-01-01T02:00:00.000Z')).toBe('31/12/2025 23:00:00');
  });

  const invalidCases: [string, number, string][] = [
    ['405', 405, JSON.stringify({ type: 'about:blank', title: 'Method Not Allowed', status: 405 })],
    [
      '500',
      500,
      JSON.stringify({ type: 'about:blank', title: 'Internal Server Error', status: 500 }),
    ],
    ['404', 404, JSON.stringify({ type: 'about:blank', title: 'Not Found', status: 404 })],
    ['200 com status diferente de ok', 200, JSON.stringify({ ...CONTRACT_OK, status: 'degraded' })],
    ['200 com JSON malformado', 200, '{"status":"ok",'],
    ['200 com campo ausente', 200, JSON.stringify({ status: 'ok', version: '0.1.0' })],
    [
      '200 com version fora do SemVer',
      200,
      JSON.stringify({ ...CONTRACT_OK, version: '0.1.0+abc' }),
    ],
    [
      '200 com timestamp inválido',
      200,
      JSON.stringify({ ...CONTRACT_OK, timestamp: '2026-02-31T10:00:00.000Z' }),
    ],
    [
      '200 com timestamp sem milissegundos',
      200,
      JSON.stringify({ ...CONTRACT_OK, timestamp: '2026-11-26T15:30:00Z' }),
    ],
    ['200 com corpo que não é objeto', 200, '[]'],
    ['200 com version não textual', 200, JSON.stringify({ ...CONTRACT_OK, version: 1 })],
  ];

  it.each(invalidCases)('CT-0028 — %s vira erro', (_name, status, body) => {
    expect(interpretResponse(status, body)).toEqual(ERROR);
  });

  it('CT-0029 — 503 válido do contrato vira o mesmo erro, sem valores', () => {
    expect(interpretResponse(503, JSON.stringify(CONTRACT_UNAVAILABLE))).toEqual(ERROR);
  });

  it('CT-0028 — timestamp: pattern e calendário real', () => {
    expect(isValidTimestamp('2026-11-26T15:34:56.789Z')).toBe(true);
    expect(isValidTimestamp('2026-02-31T10:00:00.000Z')).toBe(false);
    expect(isValidTimestamp('2026-11-26T15:34:56.789Z\n')).toBe(false);
  });

  it('CT-0032 — marcação no campo status não passa da validação (vira erro)', () => {
    const view = interpretResponse(
      200,
      JSON.stringify({ ...CONTRACT_OK, status: '<img src=x onerror=alert(1)>' }),
    );
    expect(view).toEqual(ERROR);
  });
});

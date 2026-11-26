import { parseApiBaseUrl, readDefinedApiBaseUrl } from './api-base-url';

describe('API_BASE_URL (PORTAL_API_BASE_URL)', () => {
  it('aceita http/https absoluto e remove a barra final', () => {
    expect(parseApiBaseUrl('http://127.0.0.1:8080')).toBe('http://127.0.0.1:8080');
    expect(parseApiBaseUrl('https://api.exemplo.test/')).toBe('https://api.exemplo.test');
  });

  it.each([
    undefined,
    '',
    '   ',
    'nao-e-url',
    'ftp://127.0.0.1:8080',
    'http://usuario:senha@127.0.0.1:8080',
    'http://127.0.0.1:8080?x=1',
    'http://127.0.0.1:8080#frag',
    'http://127.0.0.1:8080/?',
    42,
  ])('rejeita valor inválido %s', (value) => {
    expect(parseApiBaseUrl(value)).toBeNull();
  });

  it('sem --define no teste, o valor lido é null (fail closed)', () => {
    expect(readDefinedApiBaseUrl()).toBeNull();
  });
});

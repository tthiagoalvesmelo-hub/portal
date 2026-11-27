import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-base-url';
import { FETCH, HealthClient } from './health-client';
import { HealthView, STATUS_TEXTS } from './health-status';
import { StatusPage } from './status-page';
import { CONTRACT_OK, CONTRACT_UNAVAILABLE, Deferred, fakeFetch, jsonResponse } from './testing';

type Queue = Parameters<typeof fakeFetch>[0];

async function render(queue: Queue) {
  const fake = fakeFetch(queue);
  TestBed.configureTestingModule({
    imports: [StatusPage],
    providers: [
      { provide: API_BASE_URL, useValue: 'http://127.0.0.1:8080' },
      { provide: FETCH, useValue: fake.fn },
    ],
  });
  const fixture = TestBed.createComponent(StatusPage);
  fixture.detectChanges();
  return { fixture, calls: fake.calls, el: fixture.nativeElement as HTMLElement };
}

async function settle(fixture: ComponentFixture<StatusPage>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

const q = (el: HTMLElement, testId: string) => el.querySelector(`[data-testid="${testId}"]`);
const estado = (el: HTMLElement) => el.querySelector('[data-estado]')?.getAttribute('data-estado');
const card = (el: HTMLElement) => el.querySelector('section.cartao') as HTMLElement;
const button = (el: HTMLElement) => q(el, 'atualizar') as HTMLButtonElement;

function expectGenericError(el: HTMLElement): void {
  expect(estado(el)).toBe('erro');
  expect(q(el, 'titulo')?.textContent).toBe(STATUS_TEXTS.errorTitle);
  expect(q(el, 'selo')?.textContent).toBe(STATUS_TEXTS.errorBadge);
  expect(q(el, 'mensagem-erro')?.textContent).toBe(STATUS_TEXTS.errorMessage);
  expect(el.querySelector('[role="alert"]')?.textContent).toBe(STATUS_TEXTS.errorAnnouncement);
  const text = el.textContent ?? '';
  expect(text).not.toContain(STATUS_TEXTS.healthyBadge);
  expect(text).not.toMatch(/\b(404|405|500|503)\b/);
  expect(text).not.toContain('127.0.0.1');
  expect(text).not.toContain('http');
  expect(text).not.toContain('0.1.0');
  expect(text).not.toContain('2026-11-26T');
  expect(el.querySelector('dl')).toBeNull();
}

describe('StatusPage (US-0002)', () => {
  afterEach(() => vi.useRealTimers());

  it('CT-0026 — exibe status, versão e timestamp UTC brutos e a hora de Brasília como adicional; ignora campo desconhecido', async () => {
    const { fixture, el } = await render([
      jsonResponse(200, { ...CONTRACT_OK, campoNovo: 'nao-exibir' }),
    ]);
    await settle(fixture);

    expect(q(el, 'valor-status')?.textContent).toBe(CONTRACT_OK.status);
    expect(q(el, 'valor-versao')?.textContent).toBe(CONTRACT_OK.version);
    expect(q(el, 'valor-hora-utc')?.textContent).toBe(CONTRACT_OK.timestamp);
    expect(q(el, 'valor-hora-local')?.textContent).toBe('26/11/2026 12:30:00');
    expect(el.textContent).toContain(STATUS_TEXTS.localTimeLegend);
    expect(el.textContent).not.toContain('nao-exibir');
    expect(el.textContent).not.toContain('campoNovo');
  });

  it('CT-0027 — saudável tem título, selo, ícone decorativo, borda própria, região polite e aria-busy=false', async () => {
    const { fixture, el } = await render([jsonResponse(200, CONTRACT_OK)]);
    await settle(fixture);

    expect(q(el, 'titulo')?.textContent).toBe(STATUS_TEXTS.healthyTitle);
    expect(q(el, 'selo')?.textContent).toBe(STATUS_TEXTS.healthyBadge);
    expect(q(el, 'icone')?.getAttribute('aria-hidden')).toBe('true');
    expect(el.querySelector('[data-estado="saudavel"]')?.classList).toContain('est-ok');
    expect(el.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe(
      'API operacional. Versão 0.1.0.',
    );
    expect(card(el).getAttribute('aria-busy')).toBe('false');
    expect(button(el).getAttribute('aria-disabled')).toBe('false');
  });

  const errorCases: [string, Queue[number]][] = [
    ['falha de rede', new TypeError('Failed to fetch')],
    ['405', jsonResponse(405, { type: 'about:blank', title: 'Method Not Allowed', status: 405 })],
    [
      '500',
      jsonResponse(500, { type: 'about:blank', title: 'Internal Server Error', status: 500 }),
    ],
    ['404', jsonResponse(404, { type: 'about:blank', title: 'Not Found', status: 404 })],
    ['200 com status diferente de ok', jsonResponse(200, { ...CONTRACT_OK, status: 'degraded' })],
    ['200 com JSON malformado', jsonResponse(200, '{"status":')],
    ['200 com campo ausente', jsonResponse(200, { status: 'ok', version: '0.1.0' })],
    ['200 com version fora do SemVer', jsonResponse(200, { ...CONTRACT_OK, version: 'v0.1' })],
    ['200 com timestamp inválido', jsonResponse(200, { ...CONTRACT_OK, timestamp: '26/11/2026' })],
  ];

  it.each(errorCases)(
    'CT-0028 — %s: estado de erro genérico, sem valores',
    async (_name, response) => {
      const { fixture, el, calls } = await render([response]);
      await settle(fixture);

      expectGenericError(el);
      expect(calls).toHaveLength(1);
    },
  );

  it('CT-0028 — sem resposta em 5 s: erro, exatamente uma requisição, sem nova tentativa', async () => {
    vi.useFakeTimers();
    const { fixture, el, calls } = await render(['pendurar']);

    await vi.advanceTimersByTimeAsync(4999);
    fixture.detectChanges();
    expect(estado(el)).toBe('carregando');
    await vi.advanceTimersByTimeAsync(1);
    fixture.detectChanges();
    expectGenericError(el);
    await vi.advanceTimersByTimeAsync(30000);
    expect(calls).toHaveLength(1);
  });

  it('CT-0029 — 503 válido: mesmo erro genérico e version/timestamp recebidos fora do DOM', async () => {
    const { fixture, el } = await render([jsonResponse(503, CONTRACT_UNAVAILABLE)]);
    await settle(fixture);

    expectGenericError(el);
    expect(el.innerHTML).not.toContain(CONTRACT_UNAVAILABLE.version);
    expect(el.innerHTML).not.toContain(CONTRACT_UNAVAILABLE.timestamp);
    expect(el.innerHTML).not.toContain('unavailable');
  });

  it('CT-0030 — estado inicial é Carregando (aria-busy, botão aria-disabled, sem valores)', async () => {
    const pending = new Deferred();
    const { el } = await render([pending]);

    expect(estado(el)).toBe('carregando');
    expect(card(el).getAttribute('aria-busy')).toBe('true');
    expect(button(el).getAttribute('aria-disabled')).toBe('true');
    expect(button(el).disabled).toBe(false);
    expect(el.querySelector('[aria-live="polite"]')?.textContent?.trim()).toBe(
      STATUS_TEXTS.loadingAnnouncement,
    );
    expect(q(el, 'valor-versao')).toBeNull();
    expect(el.textContent).not.toContain(STATUS_TEXTS.healthyBadge);
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('CT-0030 — atualizar a partir do saudável remove os valores anteriores do DOM e ignora nova ativação', async () => {
    const second = new Deferred();
    const { fixture, el, calls } = await render([jsonResponse(200, CONTRACT_OK), second]);
    await settle(fixture);
    expect(q(el, 'valor-versao')?.textContent).toBe('0.1.0');

    button(el).click();
    fixture.detectChanges();

    expect(estado(el)).toBe('carregando');
    expect(q(el, 'valor-versao')).toBeNull();
    expect(q(el, 'valor-hora-utc')).toBeNull();
    expect(el.innerHTML).not.toContain('0.1.0');
    expect(el.innerHTML).not.toContain(CONTRACT_OK.timestamp);
    expect(card(el).getAttribute('aria-busy')).toBe('true');
    expect(button(el).getAttribute('aria-disabled')).toBe('true');

    button(el).click();
    button(el).click();
    expect(calls).toHaveLength(2);

    second.resolve(jsonResponse(200, { ...CONTRACT_OK, version: '0.2.0' }));
    await settle(fixture);
    expect(q(el, 'valor-versao')?.textContent).toBe('0.2.0');
  });

  it('CT-0031 — recuperação: Erro → Carregando → Saudável com os valores novos', async () => {
    const next = new Deferred();
    const { fixture, el } = await render([jsonResponse(500, {}), next]);
    await settle(fixture);
    expectGenericError(el);

    button(el).click();
    fixture.detectChanges();
    expect(estado(el)).toBe('carregando');

    next.resolve(jsonResponse(200, { ...CONTRACT_OK, timestamp: '2026-11-26T16:00:00.000Z' }));
    await settle(fixture);
    expect(estado(el)).toBe('saudavel');
    expect(q(el, 'valor-hora-utc')?.textContent).toBe('2026-11-26T16:00:00.000Z');
  });

  it('CT-0031 — se a nova consulta falhar, permanece Erro', async () => {
    const { fixture, el, calls } = await render([
      jsonResponse(500, {}),
      new TypeError('Failed to fetch'),
    ]);
    await settle(fixture);

    button(el).click();
    await settle(fixture);

    expectGenericError(el);
    expect(calls).toHaveLength(2);
  });

  it('CT-0031 — ativação por teclado: <button type="button"> nativo (Enter e Espaço geram clique) e rótulo estável', async () => {
    const { fixture, el } = await render([jsonResponse(500, {})]);
    await settle(fixture);

    expect(button(el).tagName).toBe('BUTTON');
    expect(button(el).type).toBe('button');
    expect(button(el).textContent?.trim()).toBe(STATUS_TEXTS.refresh);
    expect(button(el).hasAttribute('disabled')).toBe(false);
  });

  it('CT-0032 — erro 500 com texto técnico no corpo: mensagem constante do front, nada do corpo no DOM', async () => {
    const { fixture, el } = await render([
      jsonResponse(500, 'Exception at /srv/app/Program.cs:42\n   at Portal.Api.Main()'),
    ]);
    await settle(fixture);

    expectGenericError(el);
    expect(el.textContent).not.toContain('Exception');
    expect(el.textContent).not.toContain('/srv/app');
  });
});

describe('StatusPage — renderização só como texto (CT-0032)', () => {
  it('CT-0032 — valor com marcação aparece como texto literal e nenhum elemento é criado', async () => {
    const markup = '<img src=x onerror=alert(1)>';
    const view: HealthView = {
      kind: 'saudavel',
      status: markup,
      version: markup,
      timestamp: markup,
      localTime: markup,
    };
    TestBed.configureTestingModule({
      imports: [StatusPage],
      providers: [{ provide: HealthClient, useValue: { check: () => Promise.resolve(view) } }],
    });
    const fixture = TestBed.createComponent(StatusPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('img')).toBeNull();
    expect(q(el, 'valor-status')?.textContent).toBe(markup);
    expect(q(el, 'valor-versao')?.textContent).toBe(markup);
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { appConfig } from './app.config';
import { API_BASE_URL } from './config/api-base-url';
import { StatusPage } from './status/status-page';

describe('App (casca)', () => {
  it('cria o componente raiz com o cabeçalho provisório', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('header')?.textContent).toContain(
      'Portal Web',
    );
  });

  it('a rota /status abre a tela de status e a raiz redireciona para ela', () => {
    expect(routes.find((r) => r.path === 'status')?.component).toBe(StatusPage);
    expect(routes.find((r) => r.path === '')?.redirectTo).toBe('status');
    expect(routes.find((r) => r.path === '**')?.redirectTo).toBe('status');
  });

  it('a configuração lê API_BASE_URL uma vez e, sem --define, fica null (fail closed)', () => {
    TestBed.configureTestingModule({ providers: appConfig.providers });

    expect(TestBed.inject(API_BASE_URL)).toBeNull();
  });
});

import { TestBed } from '@angular/core/testing';
import { App } from './app';

// Teste de fumaça do esqueleto D6 (TASK-0008). Os testes CT-0026..CT-0032 entram na implementação.
describe('App (esqueleto)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
  });

  it('cria o componente raiz', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});

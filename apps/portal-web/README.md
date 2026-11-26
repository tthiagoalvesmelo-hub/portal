# portal-web

Workspace Angular 22.3 do Portal Web (ADR-0001 v2.0 D3/D6), gerado pelo Angular CLI 22.3.0 (`ng new`) com os padrões do CLI.
Testes com Vitest + jsdom (`ng test`), lint com angular-eslint (`ng lint`) e cobertura com `@vitest/coverage-v8` (limiar de 80% de linhas no `angular.json`, DEC-ARQ-0007 W3).

## Comandos

```bash
npm ci                              # instala exatamente o package-lock.json
npx ng serve                        # servidor de desenvolvimento (a subida oficial é do OPS: scripts/start-local.sh)
npx ng lint                         # lint, zero avisos (DEC-ARQ-0008)
npx ng test --no-watch --coverage   # testes unitários e cobertura
npx ng build                        # build de produção em dist/
```

A tela de status (US-0002) e a injeção de `PORTAL_API_BASE_URL` entram na implementação da TASK-0008.

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

## Tela de status (US-0002)

Rota `/status` (a raiz redireciona para ela). Estados Carregando, Saudável e Erro conforme o UX-0001 v1.1:
consulta única a `GET <PORTAL_API_BASE_URL>/health` com tempo limite de 5 s, sem nova tentativa automática e sem cache.

### `PORTAL_API_BASE_URL`

O endereço da API entra no build/serve por `--define` e é lido uma única vez pelo token `API_BASE_URL` (`src/app/config/api-base-url.ts`):

```bash
npx ng serve --define "PORTAL_API_BASE_URL=\"$PORTAL_API_BASE_URL\""
npx ng build --define "PORTAL_API_BASE_URL=\"$PORTAL_API_BASE_URL\""
```

Só é aceita URL absoluta `http`/`https`, sem credenciais, query ou fragmento. Ausente ou inválida ⇒ a tela fica em Erro (nunca "Saudável").
O valor real fica só no `.env` local (fora do git); os testes substituem o token.

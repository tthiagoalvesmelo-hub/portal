# portal

Monorepo do Portal Web e da API base (C#/.NET e Angular), conforme o ADR-0001 v2.0 (D6).

- Repositório: https://github.com/tthiagoalvesmelo-hub/portal
- Espelho local: `/workspace/projetos/portal`
- Conhecimento, contratos e decisões: https://github.com/tthiagoalvesmelo-hub/stack-conhecimento (ponteiros em [`docs/README.md`](docs/README.md))

Este repositório é **público**. Não grave segredo, credencial, dado pessoal ou dado real em commit, arquivo ou mensagem. Valores locais ficam no `.env` (fora do git); [`.env.example`](.env.example) traz **só os nomes** (ADR-0001 v2.0 D7).

## Estrutura (ADR-0001 v2.0 D6)

```
apps/portal-api/   # ASP.NET Core Minimal APIs (.NET 10 LTS) + tests/Portal.Api.Tests (xUnit, WebApplicationFactory, Coverlet)
apps/portal-web/   # workspace Angular 22.3 (ng new; Vitest + jsdom; angular-eslint)
scripts/           # scripts locais (OPS, TASK-0012; verify-local/check-version/measure-p95 — DEC-ARQ-0007/0010)
docs/              # somente ponteiros para o stack-conhecimento
```

## Pré-requisitos

SDK .NET 10 (série fixada em `global.json`), Node 24 (`^24.15`) e npm. Na box: `source ~/.config/ops-toolchain.sh`.

## Comandos

API (`apps/portal-api`):

```bash
dotnet restore --locked-mode PortalApi.slnx
dotnet format PortalApi.slnx --verify-no-changes
dotnet build PortalApi.slnx -c Release --no-restore -warnaserror
dotnet test PortalApi.slnx -c Release --no-build
```

Portal (`apps/portal-web`):

```bash
npm ci
npx ng lint
npx ng build
npx ng test --no-watch --coverage
```

A versão da API vem só do `<Version>` de `apps/portal-api/src/Portal.Api/Portal.Api.csproj` (ADR-0002 v1.1).

## Fluxo

Alterações seguem PR real no GitHub (AH-0004): ramo `feat|fix|chore/TASK-nnnn-descricao`, commits citando IDs com o trailer `Agente: <sigla>`, revisão do CR no G4, merge pelo dono da área; quem produz não aprova. Nada direto na `main`.

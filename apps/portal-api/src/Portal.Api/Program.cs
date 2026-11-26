// Composição (root) do portal-api — esqueleto D6 (ADR-0001 v2.0), DEM-2026-001 / TASK-0008.
// Endpoints, middlewares e configuração (D7/D8) entram na implementação da US-0001.
// Sem ganchos de falha na composição de produção (DEC-ARQ-0011).
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run();

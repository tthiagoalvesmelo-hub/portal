using Portal.Api.Configuration;
using Portal.Api.Health;
using Portal.Api.Http;

// Composição de produção da portal-api (ADR-0001 v2.0 D2/D8). Não há endpoint, query, cabeçalho,
// variável ou chave de configuração capaz de forçar falha (DEC-ARQ-0011); dublês só em teste.
var builder = WebApplication.CreateBuilder(args);

var settings = PortalApiSettings.Load(builder.Configuration);
var version = ServiceVersion.FromAssembly(typeof(Program).Assembly);

builder.Logging.SetMinimumLevel(settings.LogLevel.Level);
// DEC-ARQ-0013: o framework fica em Warning por regra em código; LOG_LEVEL não o reabilita.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Listen(settings.Host, settings.Port);
});

builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = FixedShutdownDrain.ShutdownTimeout);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(version);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IHealthState, HostLifetimeHealthState>();
builder.Services.AddSingleton<IInternalFailureSource, NoInternalFailure>();
builder.Services.AddSingleton<IShutdownDrain, FixedShutdownDrain>();
builder.Services.AddSingleton<ShutdownCoordinator>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(settings.AllowedOrigin)
    .WithMethods(HttpMethods.Get, HttpMethods.Head)));

var app = builder.Build();

var coordinator = app.Services.GetRequiredService<ShutdownCoordinator>();
app.Lifetime.ApplicationStopping.Register(coordinator.OnStopping);

if (settings.LogLevel.FellBack)
{
    StartupLog.InvalidLogLevel(app.Logger);
}

app.UseMiddleware<StandardHeadersMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<StatusProblemMiddleware>();
app.UseRouting();
app.UseCors();
app.MapHealth();

await app.RunAsync().ConfigureAwait(false);

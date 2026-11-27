using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Portal.Api.Configuration;
using Portal.Api.Health;

namespace Portal.Api.Tests.Support;

/// <summary>
/// Composição de produção (Program) com a configuração mínima válida via UseSetting.
/// Overrides só por ConfigureTestServices (DEC-ARQ-0011). A drenagem real (3 s) é trocada por padrão
/// para não atrasar o descarte; os testes de "fábrica sem override" usam <see cref="WithoutOverrides"/>.
/// </summary>
public static class PortalApiFactory
{
    /// <summary>Categoria da aplicação elevada a Debug nos testes de log (prefixo do namespace raiz).</summary>
    public const string AppCategoryPrefix = "Portal.Api";

    public static WebApplicationFactory<Program> WithoutOverrides(string? environment = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            ApplySettings(builder, Oracles.AllowedOrigin);
            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }
        });

    public static WebApplicationFactory<Program> Create(
        Action<IServiceCollection>? testServices = null,
        string? allowedOrigin = Oracles.AllowedOrigin,
        string? environment = null,
        CapturingLoggerProvider? logSink = null,
        string? logLevel = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            ApplySettings(builder, allowedOrigin);
            if (logLevel is not null)
            {
                builder.UseSetting(PortalApiSettings.LogLevelKey, logLevel);
            }

            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }

            if (logSink is not null)
            {
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(logSink);
                    logging.AddFilter(AppCategoryPrefix, LogLevel.Debug);
                });
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IShutdownDrain>();
                services.AddSingleton<IShutdownDrain, NoWaitDrain>();
                testServices?.Invoke(services);
            });
        });

    private static void ApplySettings(IWebHostBuilder builder, string? allowedOrigin)
    {
        builder.UseSetting(PortalApiSettings.PortKey, Oracles.TestPort);
        if (allowedOrigin is not null)
        {
            builder.UseSetting(PortalApiSettings.AllowedOriginKey, allowedOrigin);
        }
    }

    public static void Replace<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(implementation);
    }
}

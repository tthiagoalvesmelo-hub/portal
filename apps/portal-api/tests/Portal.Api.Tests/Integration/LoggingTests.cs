using Portal.Api.Health;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>
/// D8 item 13 com o oráculo da DEC-ARQ-0013: só a categoria da aplicação (<see cref="PortalApiFactory.AppCategoryPrefix"/>)
/// é elevada a Debug pelo teste; o framework continua em Warning pela regra em código do Program.cs.
/// O X-Request-Id vai no estado estruturado, campo "RequestId".
/// </summary>
public sealed class LoggingTests
{
    private const string RequestIdField = "RequestId";

    [Fact]
    [Trait("CT", "CT-0023")]
    public async Task Get200GeraDebugCorrelacionadoENadaAcimaDeDebug()
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = PortalApiFactory.Create(logSink: sink);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("X-Request-Id", Oracles.KnownRequestId);

        var before = sink.Mark();
        using var response = await client.SendAsync(request);
        var window = sink.Window(before, sink.Mark());

        Assert.Equal(200, (int)response.StatusCode);
        var app = window.Where(e => e.Category.StartsWith(PortalApiFactory.AppCategoryPrefix, StringComparison.Ordinal)).ToList();
        Assert.Contains(app, e => e.Level == LogLevel.Debug
            && e.State.Any(kv => kv.Key == RequestIdField && Equals(kv.Value, Oracles.KnownRequestId)));
        Assert.DoesNotContain(window, e => e.Level >= LogLevel.Information);
        Assert.DoesNotContain(window, e => e.Category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) && e.Level >= LogLevel.Information);

        var loggerFactory = factory.Services.GetRequiredService<ILoggerFactory>();
        Assert.False(loggerFactory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").IsEnabled(LogLevel.Information));
    }

    [Fact]
    [Trait("CT", "CT-0024")]
    public async Task Falha500GeraErrorDaAplicacaoECorpoMinimo()
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = PortalApiFactory.Create(
            s => PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource()),
            logSink: sink);
        using var client = factory.CreateClient();

        var before = sink.Mark();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var window = sink.Window(before, sink.Mark());

        await InjectedFailureTests.AssertInternalErrorAsync(response);
        Assert.Contains(window, e => e.Level == LogLevel.Error
            && e.Category.StartsWith(PortalApiFactory.AppCategoryPrefix, StringComparison.Ordinal)
            && e.Exception is not null);
    }

    public static TheoryData<string> Paths => new() { "200", "503", "500" };

    [Theory]
    [MemberData(nameof(Paths))]
    [Trait("CT", "CT-0025")]
    public async Task LogsDaJanelaNaoTrazemNadaDaDenylist(string path)
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = PortalApiFactory.Create(
            s =>
            {
                if (path == "503")
                {
                    PortalApiFactory.Replace<IHealthState>(s, new UnavailableHealthState());
                }

                if (path == "500")
                {
                    PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource());
                }
            },
            logSink: sink);
        using var client = factory.CreateClient();

        var before = sink.Mark();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var window = sink.Window(before, sink.Mark());

        Assert.Equal(int.Parse(path, System.Globalization.CultureInfo.InvariantCulture), (int)response.StatusCode);
        Assert.NotEmpty(window);
        foreach (var entry in window)
        {
            var text = string.Join('\n', new[] { entry.Message, entry.Exception?.Message ?? string.Empty }
                .Concat(entry.State.Select(kv => $"{kv.Key}={kv.Value}")));
            Assert.Empty(Denylist.Findings(text));
            if (entry.Exception?.StackTrace is not null)
            {
                Assert.Equal(LogLevel.Error, entry.Level);
            }
        }

        Assert.DoesNotContain("   at ", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogLevelInvalidoGeraAvisoNaPartida()
    {
        var sink = new CapturingLoggerProvider();
        await using var factory = PortalApiFactory.Create(logSink: sink, logLevel: "verbose");
        using var client = factory.CreateClient();

        var entries = sink.Window(0, sink.Mark());

        Assert.Contains(entries, e => e.Level == LogLevel.Warning && e.Message.Contains("LOG_LEVEL", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Message.Contains("verbose", StringComparison.Ordinal));
    }
}

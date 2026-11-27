using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Portal.Api.Health;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Unit;

/// <summary>
/// US-0001 CA-07 / DEC-ARQ-0012 §3 (R4): o estado "encerrando" vem do ApplicationStopping pela abstração injetada (M7)
/// e o /health responde 503 durante a janela. A medição com processo real e SIGTERM está no spike (evidência no PR).
/// </summary>
public sealed class ShutdownTests
{
    [Fact]
    [Trait("CT", "CT-0018")]
    public void CoordenadorMarcaEncerrandoAntesDeEsperarAJanela()
    {
        var state = new HostLifetimeHealthState();
        var observed = false;
        var drain = new CallbackDrain(() => observed = state.IsUnavailable);
        var coordinator = new ShutdownCoordinator(state, drain, Microsoft.Extensions.Logging.Abstractions.NullLogger<ShutdownCoordinator>.Instance);

        Assert.False(state.IsUnavailable);
        coordinator.OnStopping();

        Assert.True(observed);
        Assert.True(state.IsUnavailable);
    }

    [Fact]
    public void ConstantesDeEncerramentoSeguemADecisao()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), FixedShutdownDrain.Window);
        Assert.Equal(TimeSpan.FromSeconds(5), FixedShutdownDrain.ShutdownTimeout);
        Assert.True(FixedShutdownDrain.Window < FixedShutdownDrain.ShutdownTimeout);
    }

    [Fact]
    [Trait("CT", "CT-0018")]
    public async Task DuranteAJanelaDeDrenagemOHealthResponde503()
    {
        using var gate = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var drain = new CallbackDrain(() =>
        {
            gate.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        });
        await using var factory = PortalApiFactory.Create(s => PortalApiFactory.Replace<IShutdownDrain>(s, drain));
        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        var lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();

        var stopping = Task.Run(lifetime.StopApplication);
        Assert.True(gate.Wait(TimeSpan.FromSeconds(10)));
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        release.Set();
        await stopping;

        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.Equal(503, (int)response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ContractValidator.ValidateHealthUnavailable(body.RootElement));
        Assert.Null(Oracles.Header(response, "Retry-After"));
    }

    [Fact]
    public void DrenagemFixaEsperaAJanela()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        new FixedShutdownDrain().Wait();
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(2.9), TimeSpan.FromSeconds(4));
    }

    private sealed class CallbackDrain(Action callback) : IShutdownDrain
    {
        public void Wait() => callback();
    }
}

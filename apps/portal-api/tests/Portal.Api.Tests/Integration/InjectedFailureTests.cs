using System.Text.Json;

using Portal.Api.Health;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>DEC-ARQ-0011 (L-12): 500 e 503 só por injeção; canais proibidos não alteram nada.</summary>
public sealed class InjectedFailureTests
{
    [Fact]
    [Trait("CT", "CT-0017")]
    public async Task FalhaInjetadaResponde500ComProblemaMinimo()
    {
        await using var factory = PortalApiFactory.Create(s => PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource()));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        await AssertInternalErrorAsync(response);
    }

    [Fact]
    [Trait("CT", "CT-0018")]
    public async Task EstadoIndisponivelInjetadoResponde503NoEsquemaSemRetryAfter()
    {
        var clock = new ManualTimeProvider(Oracles.FixedInstant);
        await using var factory = PortalApiFactory.Create(s =>
        {
            PortalApiFactory.Replace<IHealthState>(s, new UnavailableHealthState());
            PortalApiFactory.Replace<TimeProvider>(s, clock);
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal(Oracles.JsonContentType, Oracles.ContentType(response));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ContractValidator.ValidateHealthUnavailable(body.RootElement));
        Assert.Equal(Oracles.FixedInstantFormatted, body.RootElement.GetProperty("timestamp").GetString());
        Assert.Null(Oracles.Header(response, "Retry-After"));
        Oracles.AssertStandardHeaders(response);
    }

    /// <summary>O 500 preserva o Access-Control-Allow-Origin da origem permitida (o Portal lê o erro, não uma falha de CORS).</summary>
    [Fact]
    [Trait("CT", "CT-0017")]
    public async Task FalhaInjetadaComOrigemPermitidaMantemAcao()
    {
        await using var factory = PortalApiFactory.Create(s => PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource()));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", Oracles.AllowedOrigin);

        using var response = await client.SendAsync(request);

        await AssertInternalErrorAsync(response);
        Assert.Equal(Oracles.AllowedOrigin, Oracles.Header(response, "Access-Control-Allow-Origin"));
    }

    public static TheoryData<string, string?, string?> ForbiddenChannels => new()
    {
        { "/health?force=503", null, null },
        { "/health?status=500", null, null },
        { "/health?fail=true", null, null },
        { "/health", "X-Force-Status", "503" },
        { "/health", "X-Health-Fail", "1" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenChannels))]
    [Trait("CT", "CT-0019")]
    public async Task CanaisProibidosNaoForcamFalha(string path, string? header, string? value)
    {
        await using var factory = PortalApiFactory.WithoutOverrides();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (header is not null)
        {
            request.Headers.Add(header, value);
        }

        using var response = await client.SendAsync(request);

        await AssertHealthyAsync(response);
    }

    [Fact]
    [Trait("CT", "CT-0019")]
    public async Task VariaveisDeAmbienteFicticiasNaoForcamFalha()
    {
        Environment.SetEnvironmentVariable("FORCE_HEALTH_STATUS", "503");
        Environment.SetEnvironmentVariable("HEALTH_FAIL", "1");
        try
        {
            await using var factory = PortalApiFactory.WithoutOverrides();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

            await AssertHealthyAsync(response);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORCE_HEALTH_STATUS", null);
            Environment.SetEnvironmentVariable("HEALTH_FAIL", null);
        }
    }

    [Fact]
    [Trait("CT", "CT-0021")]
    public async Task FalhaInjetadaEmDevelopmentContinuaProblemaMinimo()
    {
        await using var factory = PortalApiFactory.Create(
            s => PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource()),
            environment: "Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        await AssertInternalErrorAsync(response);
        Assert.DoesNotContain("text/html", Oracles.ContentType(response), StringComparison.Ordinal);
    }

    internal static async Task AssertInternalErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(500, (int)response.StatusCode);
        Assert.Equal(Oracles.ProblemContentType, Oracles.ContentType(response));
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Empty(ContractValidator.ValidateProblem(body.RootElement, "Internal Server Error", 500));
        Assert.DoesNotContain("traceId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        Oracles.AssertStandardHeaders(response);
    }

    private static async Task AssertHealthyAsync(HttpResponseMessage response)
    {
        Assert.Equal(200, (int)response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ContractValidator.ValidateHealthOk(body.RootElement));
    }
}

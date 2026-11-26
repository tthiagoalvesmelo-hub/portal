using Portal.Api.Configuration;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>DEC-ARQ-0002 L-06 / D8 item 8: CORS exato e fail closed.</summary>
public sealed class CorsTests
{
    [Fact]
    [Trait("CT", "CT-0014")]
    public async Task OrigemPermitidaRecebeAcaoExatoSemCredenciais()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", Oracles.AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(Oracles.AllowedOrigin, Oracles.Header(response, "Access-Control-Allow-Origin"));
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    [Trait("CT", "CT-0014")]
    public async Task OrigemNegadaRecebe200SemAcaoESem403()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", Oracles.OtherOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Origin"));
    }

    public static TheoryData<string?> InvalidOrigins => new() { null, "*", "nao-e-url" };

    [Theory]
    [MemberData(nameof(InvalidOrigins))]
    [Trait("CT", "CT-0015")]
    public async Task OrigemAusenteCuringaOuInvalidaImpedeAInicializacao(string? origin)
    {
        // "Ausente" = nem configuração nem variável de ambiente; o valor do processo é restaurado no fim.
        var previous = Environment.GetEnvironmentVariable(PortalApiSettings.AllowedOriginKey);
        Environment.SetEnvironmentVariable(PortalApiSettings.AllowedOriginKey, null);
        try
        {
            await using var factory = PortalApiFactory.Create(allowedOrigin: origin);

            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            var message = error.ToString();
            Assert.Contains(PortalApiSettings.AllowedOriginKey, message, StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(origin))
            {
                Assert.DoesNotContain($"'{origin}'", message, StringComparison.Ordinal);
                Assert.DoesNotContain("nao-e-url", message, StringComparison.Ordinal);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(PortalApiSettings.AllowedOriginKey, previous);
        }
    }
}

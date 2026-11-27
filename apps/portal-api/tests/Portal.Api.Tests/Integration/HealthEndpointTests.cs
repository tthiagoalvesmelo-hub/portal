using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>US-0001 CA-01..CA-06: corpo, status, timestamp, versão, leitura repetida e HEAD.</summary>
public sealed class HealthEndpointTests
{
    [Fact]
    [Trait("CT", "CT-0001")]
    [Trait("CT", "CT-0020")]
    public async Task GetHealthSemOverrideResponde200ComExatamenteTresCampos()
    {
        await using var factory = PortalApiFactory.WithoutOverrides();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(Oracles.JsonContentType, Oracles.ContentType(response));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ContractValidator.ValidateHealthOk(body.RootElement));
        Assert.Equal(["status", "version", "timestamp"], body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.All(body.RootElement.EnumerateObject(), p => Assert.False(string.IsNullOrEmpty(p.Value.GetString())));
    }

    [Fact]
    [Trait("CT", "CT-0002")]
    public async Task StatusEhExatamenteOk()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var body = JsonDocument.Parse(await client.GetStringAsync(new Uri("/health", UriKind.Relative)));

        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("CT", "CT-0003")]
    public async Task TimestampVemDoRelogioInjetadoComTresCasasEmCulturaInvariante()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            var clock = new ManualTimeProvider(Oracles.FixedInstant);
            await using var factory = PortalApiFactory.Create(s => PortalApiFactory.Replace<TimeProvider>(s, clock));
            using var client = factory.CreateClient();

            using var body = JsonDocument.Parse(await client.GetStringAsync(new Uri("/health", UriKind.Relative)));

            Assert.Equal(Oracles.FixedInstantFormatted, body.RootElement.GetProperty("timestamp").GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    [Trait("CT", "CT-0006")]
    public async Task VersionIgualAoVersionDoCsprojSemMetadadosDeBuild()
    {
        var csproj = XDocument.Load(LocateApiCsproj());
        var expected = csproj.Descendants("Version").Single().Value;
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var body = JsonDocument.Parse(await client.GetStringAsync(new Uri("/health", UriKind.Relative)));
        var version = body.RootElement.GetProperty("version").GetString();

        Assert.Equal(expected, version);
        Assert.DoesNotContain("+", version, StringComparison.Ordinal);
        Assert.True(ContractValidator.IsValidVersion(version));
    }

    [Fact]
    [Trait("CT", "CT-0007")]
    public async Task ChamadasRepetidasMantemStatusEVersaoETimestampAcompanhaORelogio()
    {
        var clock = new ManualTimeProvider(Oracles.FixedInstant);
        await using var factory = PortalApiFactory.Create(s => PortalApiFactory.Replace<TimeProvider>(s, clock));
        using var client = factory.CreateClient();
        var bodies = new List<JsonElement>();

        for (var i = 0; i < 3; i++)
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync(new Uri("/health", UriKind.Relative)));
            bodies.Add(doc.RootElement.Clone());
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Single(bodies.Select(b => b.GetProperty("status").GetString()).Distinct());
        Assert.Single(bodies.Select(b => b.GetProperty("version").GetString()).Distinct());
        Assert.Equal(
            ["2026-11-26T15:34:56.789Z", "2026-11-26T15:34:57.789Z", "2026-11-26T15:34:58.789Z"],
            bodies.Select(b => b.GetProperty("timestamp").GetString()));
    }

    [Fact]
    [Trait("CT", "CT-0009")]
    public async Task HeadTemMesmoStatusEMesmosCabecalhosDoGetSemCorpo()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var get = await client.GetAsync(new Uri("/health", UriKind.Relative));
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, new Uri("/health", UriKind.Relative));
        using var head = await client.SendAsync(headRequest);

        Assert.Equal(get.StatusCode, head.StatusCode);
        Assert.Equal(200, (int)head.StatusCode);
        Assert.Equal(Oracles.ContentType(get), Oracles.ContentType(head));
        Assert.Equal(Oracles.Header(get, "Cache-Control"), Oracles.Header(head, "Cache-Control"));
        Assert.Equal(Oracles.Header(get, "X-Content-Type-Options"), Oracles.Header(head, "X-Content-Type-Options"));
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
    }

    private static string LocateApiCsproj()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Portal.Api", "Portal.Api.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Portal.Api.csproj não localizado a partir do diretório de saída dos testes.");
    }
}

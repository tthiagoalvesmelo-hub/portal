using Portal.Api.Health;
using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>D8 item 6, L-03, L-04 e RN-0001: cabeçalhos de toda resposta, X-Request-Id e denylist.</summary>
public sealed class HeadersTests
{
    [Fact]
    [Trait("CT", "CT-0012")]
    public async Task TodaRespostaTemNoStoreENosniff()
    {
        var collected = await Responses.AllPathsAsync();

        Assert.Equal(["200", "503", "404", "405", "500", "preflight"], collected.Select(c => c.Name));
        Assert.Equal([200, 503, 404, 405, 500, 204], collected.Select(c => (int)c.Response.StatusCode));
        Assert.All(collected, c => Oracles.AssertStandardHeaders(c.Response));
        foreach (var c in collected)
        {
            c.Response.Dispose();
        }
    }

    [Fact]
    [Trait("CT", "CT-0013")]
    public async Task XRequestIdValidoEhDevolvidoIdentico()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("X-Request-Id", Oracles.KnownRequestId);

        using var response = await client.SendAsync(request);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(Oracles.KnownRequestId, Oracles.Header(response, "X-Request-Id"));
    }

    public static TheoryData<string?> InvalidRequestIds => new()
    {
        null,
        "3F2B8C4E-1A2B-4C3D-8E9F-0A1B2C3D4E5F",
        "abc",
        "3f2b8c4e-1a2b-4c3d-8e9f-0a1b2c3d4e5f0",
        "{3f2b8c4e-1a2b-4c3d-8e9f-0a1b2c3d4e5f}",
    };

    [Theory]
    [MemberData(nameof(InvalidRequestIds))]
    [Trait("CT", "CT-0013")]
    public async Task XRequestIdAusenteOuInvalidoGeraUuidV4SemEcoarENunca400(string? received)
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        if (received is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Request-Id", received);
        }

        using var response = await client.SendAsync(request);
        var returned = Oracles.Header(response, "X-Request-Id");

        Assert.Equal(200, (int)response.StatusCode);
        Assert.True(ContractValidator.IsUuidV4(returned), "UUID v4 canônico gerado");
        Assert.NotEqual(received, returned);
    }

    [Fact]
    [Trait("CT", "CT-0016")]
    public async Task CorpoECabecalhosNaoTrazemNadaDaDenylist()
    {
        var collected = await Responses.AllPathsAsync();

        foreach (var c in collected.Where(c => c.Name is "200" or "405" or "503" or "500"))
        {
            var headers = string.Join('\n', c.Response.Headers.Concat(c.Response.Content.Headers)
                .Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));
            Assert.Empty(Denylist.Findings(c.Body + "\n" + headers));
            Assert.Null(Oracles.Header(c.Response, "Server"));
            Assert.Null(Oracles.Header(c.Response, "X-Powered-By"));
        }

        foreach (var c in collected)
        {
            c.Response.Dispose();
        }
    }
}

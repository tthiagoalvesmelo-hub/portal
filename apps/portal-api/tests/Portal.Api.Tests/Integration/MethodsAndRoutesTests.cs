using System.Text.Json;

using Microsoft.AspNetCore.Routing;

using Portal.Api.Tests.Support;

namespace Portal.Api.Tests.Integration;

/// <summary>US-0001 CA-08 e padrões do serviço: 405, preflight (DEC-ARQ-0012 §1), 404 e superfície (§2).</summary>
public sealed class MethodsAndRoutesTests
{
    public static TheoryData<string> DisallowedMethods => new() { "POST", "PUT", "PATCH", "DELETE" };

    [Theory]
    [MemberData(nameof(DisallowedMethods))]
    [Trait("CT", "CT-0008")]
    public async Task MetodoNaoPermitidoResponde405ComAllowEProblemaMinimo(string method)
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri("/health", UriKind.Relative));

        using var response = await client.SendAsync(request);

        await AssertMethodNotAllowedAsync(response);
    }

    /// <summary>
    /// OPTIONS sem preflight ⇒ 405 (DEC-ARQ-0012 §1.3). CT-0050, ID definido pela QA (reserva no registro pendente,
    /// no PR de ajuste dos CT após o merge dos PRs #4 e #7);
    /// variantes parametrizadas: sem Origin e sem Access-Control-Request-Method, só Origin permitida, só ACRM.
    /// Com só Origin, o ACAO pode existir e não é verificado (§1.3).
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(Oracles.AllowedOrigin, null)]
    [InlineData(null, "GET")]
    [Trait("CT", "CT-0050")]
    public async Task OptionsSemPreflightResponde405(string? origin, string? requestMethod)
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/health", UriKind.Relative));
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (requestMethod is not null)
        {
            request.Headers.Add("Access-Control-Request-Method", requestMethod);
        }

        using var response = await client.SendAsync(request);

        await AssertMethodNotAllowedAsync(response);
    }

    [Fact]
    [Trait("CT", "CT-0010")]
    public async Task PreflightDaOrigemPermitidaResponde204SemCorpoComMetodosGetHead()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight(Oracles.AllowedOrigin));

        Assert.Equal(204, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(Oracles.AllowedOrigin, Oracles.Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal(Oracles.AllowedMethodsSet, Oracles.MethodSet(Oracles.Header(response, "Access-Control-Allow-Methods")));
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Credentials"));
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Headers"));
        Oracles.AssertStandardHeaders(response);
    }

    /// <summary>DEC-ARQ-0012 §1.2: preflight de origem não permitida ⇒ 204 sem ACAO/ACAM, nunca 403.</summary>
    [Fact]
    [Trait("CT", "CT-0010")]
    public async Task PreflightDeOrigemNegadaResponde204SemCabecalhosCors()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight(Oracles.OtherOrigin));

        Assert.Equal(204, (int)response.StatusCode);
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Origin"));
        Assert.Null(Oracles.Header(response, "Access-Control-Allow-Methods"));
        Oracles.AssertStandardHeaders(response);
    }

    [Fact]
    [Trait("CT", "CT-0011")]
    public async Task RotaInexistenteResponde404ComProblemaMinimo()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/inexistente", UriKind.Relative));

        Assert.Equal(404, (int)response.StatusCode);
        Assert.Equal(Oracles.ProblemContentType, Oracles.ContentType(response));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ContractValidator.ValidateProblem(body.RootElement, "Not Found", 404));
        Oracles.AssertStandardHeaders(response);
    }

    [Fact]
    [Trait("CT", "CT-0011")]
    public async Task HeadEmRotaInexistenteResponde404SemCorpo()
    {
        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, new Uri("/inexistente", UriKind.Relative));

        using var response = await client.SendAsync(request);

        Assert.Equal(404, (int)response.StatusCode);
        Assert.Equal(Oracles.ProblemContentType, Oracles.ContentType(response));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Oracles.AssertStandardHeaders(response);
    }

    [Fact]
    [Trait("CT", "CT-0022")]
    public async Task SuperficieDaComposicaoDeProducaoEhSomenteHealthGetEHead()
    {
        await using var factory = PortalApiFactory.WithoutOverrides();
        using var client = factory.CreateClient();
        var source = factory.Services.GetRequiredService<EndpointDataSource>();

        var surface = source.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => $"{e.RoutePattern.RawText} {m}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["/health GET", "/health HEAD"], surface);
        Assert.Equal(source.Endpoints.Count, source.Endpoints.OfType<RouteEndpoint>().Count());
    }

    internal static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    private static async Task AssertMethodNotAllowedAsync(HttpResponseMessage response)
    {
        Assert.Equal(405, (int)response.StatusCode);
        Assert.Equal(Oracles.AllowedMethodsSet, Oracles.MethodSet(Oracles.Header(response, "Allow")));
        Assert.Equal(Oracles.ProblemContentType, Oracles.ContentType(response));
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Empty(ContractValidator.ValidateProblem(body.RootElement, "Method Not Allowed", 405));
        Assert.DoesNotContain("traceId", text, StringComparison.OrdinalIgnoreCase);
        Oracles.AssertStandardHeaders(response);
    }
}

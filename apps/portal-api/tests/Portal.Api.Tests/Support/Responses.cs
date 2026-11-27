using Portal.Api.Health;

namespace Portal.Api.Tests.Support;

/// <summary>Coleta das respostas de todos os caminhos (200, 503, 404, 405, 500, preflight) para CT-0012/0016.</summary>
public static class Responses
{
    public sealed record Collected(string Name, HttpResponseMessage Response, string Body);

    public static async Task<Collected> GetHealthAsync(string name, Action<IServiceCollection>? services = null, string path = "/health", HttpMethod? method = null)
    {
        await using var factory = PortalApiFactory.Create(services);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, new Uri(path, UriKind.Relative));
        var response = await client.SendAsync(request);
        return new Collected(name, response, await response.Content.ReadAsStringAsync());
    }

    public static async Task<IReadOnlyList<Collected>> AllPathsAsync()
    {
        var list = new List<Collected>
        {
            await GetHealthAsync("200"),
            await GetHealthAsync("503", s => PortalApiFactory.Replace<IHealthState>(s, new UnavailableHealthState())),
            await GetHealthAsync("404", path: "/inexistente"),
            await GetHealthAsync("405", method: HttpMethod.Post),
            await GetHealthAsync("500", s => PortalApiFactory.Replace<IInternalFailureSource>(s, new ThrowingFailureSource())),
        };

        await using var factory = PortalApiFactory.Create();
        using var client = factory.CreateClient();
        using var preflight = Integration.MethodsAndRoutesTests.Preflight(Oracles.AllowedOrigin);
        var response = await client.SendAsync(preflight);
        list.Add(new Collected("preflight", response, await response.Content.ReadAsStringAsync()));
        return list;
    }
}

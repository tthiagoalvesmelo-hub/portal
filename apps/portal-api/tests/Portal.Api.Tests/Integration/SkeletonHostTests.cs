using Microsoft.AspNetCore.Mvc.Testing;

namespace Portal.Api.Tests.Integration;

// Teste de fumaça do esqueleto D6 (TASK-0008): confirma que a WebApplicationFactory<Program>
// compõe o host de produção. Será substituído pelos testes CT-0001..CT-0025 na implementação.
public sealed class SkeletonHostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SkeletonHostTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FactoryComposesProductionHost()
    {
        using var client = _factory.CreateClient();

        Assert.NotNull(client.BaseAddress);
    }
}

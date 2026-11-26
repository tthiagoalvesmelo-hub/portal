namespace Portal.Api.Health;

/// <summary>Implementação de produção: não há dependência que possa falhar nesta demanda.</summary>
public sealed class NoInternalFailure : IInternalFailureSource
{
    public void ThrowIfFailed()
    {
    }
}

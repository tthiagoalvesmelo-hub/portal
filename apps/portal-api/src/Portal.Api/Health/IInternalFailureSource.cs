namespace Portal.Api.Health;

/// <summary>
/// Origem de falha interna consultada pela lógica de saúde. Em produção nunca falha;
/// o dublê que lança exceção existe só na composição de teste (DEC-ARQ-0011, L-12).
/// </summary>
public interface IInternalFailureSource
{
    void ThrowIfFailed();
}

namespace SistemaAnalisisOpiniones.Data;

public interface IExtractor
{
    string Source { get; }

    IAsyncEnumerable<object> ReadAsync(CancellationToken cancellationToken);
}

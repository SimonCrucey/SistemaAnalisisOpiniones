using System.Runtime.CompilerServices;
using SistemaAnalisisOpiniones.ETL.Extract;
using SistemaAnalisisOpiniones.Models.Csv;

namespace SistemaAnalisisOpiniones.Data;

public sealed class SurveyCsvExtractor(string path) : IExtractor
{
    private static readonly string[] RequiredHeaders =
    [
        "IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario",
        "Clasificación", "PuntajeSatisfacción", "Fuente"
    ];

    public string Source => "encuestas-csv";

    public async IAsyncEnumerable<object> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var file = await new CsvFileReader().ReadAsync(path, cancellationToken);
        var headers = file.Headers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (headers.Count != file.Headers.Count || !RequiredHeaders.All(headers.Contains))
        {
            throw new CsvFormatException("El CSV de encuestas tiene encabezados requeridos ausentes o duplicados.");
        }

        foreach (var row in file.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fields = file.Headers.Zip(row.Fields).ToDictionary(pair => pair.First, pair => pair.Second);
            yield return new { File = Path.GetFileName(path), Line = row.LineNumber, Fields = fields };
        }
    }
}

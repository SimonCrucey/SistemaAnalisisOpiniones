namespace SistemaAnalisisOpiniones.Models.Csv;

public sealed class CsvFile
{
    private readonly Dictionary<string, int> _headerIndexes;

    public CsvFile(
        string path,
        string fileName,
        IReadOnlyList<string> headers,
        IReadOnlyList<CsvRecord> records)
    {
        Path = path;
        FileName = fileName;
        Headers = headers;
        Records = records;
        _headerIndexes = headers
            .Select((header, index) => new { header, index })
            .GroupBy(item => item.header, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
    }

    public string Path { get; }

    public string FileName { get; }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<CsvRecord> Records { get; }

    public int GetIndex(string header)
    {
        if (_headerIndexes.TryGetValue(header, out var index))
        {
            return index;
        }

        throw new CsvFormatException(
            $"El archivo '{FileName}' no contiene el encabezado requerido '{header}'. Encabezados encontrados: {string.Join(", ", Headers)}.");
    }

    public string Get(CsvRecord record, string header)
    {
        var index = GetIndex(header);
        return index < record.Fields.Count ? record.Fields[index] : string.Empty;
    }
}

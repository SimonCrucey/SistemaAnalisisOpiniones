namespace SistemaAnalisisOpiniones.Models.Csv;

public sealed record CsvRecord(int LineNumber, IReadOnlyList<string> Fields, string RawText);

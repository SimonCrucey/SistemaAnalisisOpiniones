using System.Text;
using SistemaAnalisisOpiniones.Models.Csv;

namespace SistemaAnalisisOpiniones.ETL.Extract;

public sealed class CsvFileReader
{
    public CsvFile Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontró el archivo CSV requerido: {path}", path);
        }

        var bytes = File.ReadAllBytes(path);
        return ReadRecords(path, Encoding.UTF8.GetString(bytes));
    }

    public async Task<CsvFile> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        return ReadRecords(path, text, cancellationToken, strict: true);
    }

    private static CsvFile ReadRecords(
        string path, string text, CancellationToken cancellationToken = default, bool strict = false)
    {
        var records = Parse(text, Path.GetFileName(path), cancellationToken, strict);
        var headers = records[0].Fields.Select((header, index) => index == 0 ? header.TrimStart('\uFEFF') : header).ToArray();
        var rows = new List<CsvRecord>(records.Count - 1);

        for (var index = 1; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = records[index];
            if (record.Fields.Count != headers.Length)
            {
                throw new CsvFormatException(
                    $"El archivo '{Path.GetFileName(path)}' tiene {record.Fields.Count} columnas en la fila {record.LineNumber}, pero se esperaban {headers.Length}.");
            }

            rows.Add(record);
        }

        return new CsvFile(path, Path.GetFileName(path), headers, rows);
    }

    private static List<CsvRecord> Parse(
        string text, string fileName, CancellationToken cancellationToken, bool strict)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var raw = new StringBuilder();
        var inQuotes = false;
        var closedQuote = false;
        var line = 1;
        var recordStartLine = 1;
        var index = 0;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (inQuotes)
            {
                raw.Append(character);

                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        raw.Append('"');
                        index += 2;
                        continue;
                    }

                    inQuotes = false;
                    closedQuote = true;
                    index++;
                    continue;
                }

                if (character == '\n')
                {
                    line++;
                }

                field.Append(character);
                index++;
                continue;
            }

            if (strict && closedQuote && character is not (',' or '\r' or '\n'))
            {
                throw new CsvFormatException($"Texto después de una comilla de cierre en '{fileName}', línea {line}.");
            }

            if (character == '"')
            {
                if (field.Length == 0)
                {
                    inQuotes = true;
                    raw.Append(character);
                    index++;
                    continue;
                }

                if (strict)
                {
                    throw new CsvFormatException($"Comilla inesperada en '{fileName}', línea {line}.");
                }

                field.Append(character);
                raw.Append(character);
                index++;
                continue;
            }

            if (character == ',')
            {
                closedQuote = false;
                fields.Add(field.ToString());
                field.Clear();
                raw.Append(character);
                index++;
                continue;
            }

            if (character == '\r' || character == '\n')
            {
                closedQuote = false;
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index += 2;
                }
                else
                {
                    index++;
                }

                fields.Add(field.ToString());
                field.Clear();

                var recordText = raw.ToString();
                if (!IsBlankRecord(fields, recordText))
                {
                    records.Add(new CsvRecord(recordStartLine, fields.ToArray(), recordText));
                }

                fields.Clear();
                raw.Clear();
                line++;
                recordStartLine = line;
                continue;
            }

            field.Append(character);
            raw.Append(character);
            index++;
        }

        if (inQuotes)
        {
            throw new CsvFormatException($"El archivo '{fileName}' terminó con una comilla sin cerrar cerca de la línea {recordStartLine}.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            var recordText = raw.ToString();

            if (!IsBlankRecord(fields, recordText))
            {
                records.Add(new CsvRecord(recordStartLine, fields.ToArray(), recordText));
            }
        }

        if (records.Count == 0)
        {
            throw new CsvFormatException($"El archivo '{fileName}' está vacío o no contiene encabezados.");
        }

        return records;
    }

    private static bool IsBlankRecord(IReadOnlyList<string> fields, string rawText)
    {
        return fields.Count == 1 && fields[0].Length == 0 && string.IsNullOrWhiteSpace(rawText);
    }
}

using System.Globalization;

namespace SistemaAnalisisOpiniones.Data.Dimensions;

public sealed class DimensionTransformer
{
    public DimensionPlan Build(IReadOnlyList<StagedDimensionRecord> records, DimensionCatalog catalog, CancellationToken token)
    {
        var products = new Dictionary<string, ProductDimension>(StringComparer.OrdinalIgnoreCase);
        var clients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dates = new SortedDictionary<int, DateDimension>();
        var missingProducts = 0;
        var missingClients = 0;
        var absentClients = 0;
        var invalidDates = 0;
        var unknownSentiments = 0;
        var missingSentiments = 0;
        var sentimentCounts = new Dictionary<string, int>
        {
            ["Positivo"] = 0, ["Negativo"] = 0, ["Neutro"] = 0, ["No clasificado"] = 0
        };
        foreach (var row in records)
        {
            token.ThrowIfCancellationRequested();
            if (TryDate(row.Date, row.Source, out var day))
            {
                var key = day.Year * 10000 + day.Month * 100 + day.Day;
                dates[key] = new(key, day);
            }
            else invalidDates++;

            var label = row.Sentiment?.Trim().ToUpperInvariant();
            var sentiment = label switch
            {
                "POSITIVA" or "POSITIVO" => "Positivo",
                "NEGATIVA" or "NEGATIVO" => "Negativo",
                "NEUTRA" or "NEUTRO" => "Neutro",
                _ => "No clasificado"
            };
            if (string.IsNullOrEmpty(label)) missingSentiments++;
            else if (sentiment == "No clasificado" && label != "NO CLASIFICADO") unknownSentiments++;
            sentimentCounts[sentiment]++;

            if (row.Source == "comentarios-api" && row.Platform is null) continue;
            var fromCsv = row.Source != "resenas-sql";
            var productKey = fromCsv ? CsvKey(row.Product, 'P') : SqlKey(row.Product);
            if (productKey is not null && catalog.Products.TryGetValue(productKey, out var product))
                products[product.Id] = product;
            else missingProducts++;

            if (string.IsNullOrWhiteSpace(row.Client))
            {
                absentClients++;
                continue;
            }
            var clientKey = fromCsv ? CsvKey(row.Client, 'C') : SqlKey(row.Client);
            if (clientKey is not null && catalog.Clients.TryGetValue(clientKey, out var client)) clients.Add(client);
            else missingClients++;
        }

        return new(products.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(),
            [new("Encuesta", "CSV"), new("Reseña web", "Base relacional"), new("Red social", "API REST")],
            ["Positivo", "Negativo", "Neutro", "No clasificado"], dates.Values.ToArray(),
            clients.Order(StringComparer.Ordinal).ToArray(), missingProducts, missingClients, invalidDates,
            unknownSentiments, missingSentiments, absentClients, sentimentCounts,
            catalog.Aliases.Where(a => a.Entity == "Producto" ? products.ContainsKey(a.CsvKey) : clients.Contains(a.CsvKey)).ToArray());
    }

    internal static string? CsvKey(string? value, char prefix)
    {
        var code = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return null;
        var canonicalPrefix = prefix == 'P' ? "PROD:" : "CLI:";
        var digits = code.StartsWith(canonicalPrefix, StringComparison.Ordinal) ? code[canonicalPrefix.Length..]
            : code[0] == prefix ? code[1..] : code;
        if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            return FormattableString.Invariant($"CSV:{canonicalPrefix}{id:D3}");
        // Los códigos alfanuméricos se resuelven solo si el catálogo los contiene.
        return code.Length <= 96 && code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? "CSV:" + code : null;
    }

    private static string? SqlKey(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? FormattableString.Invariant($"SQL:{id}") : null;

    private static bool TryDate(string? value, string source, out DateOnly day)
    {
        var text = value?.Trim();
        if (source == "resenas-sql" && text is { Length: > 10 } &&
            DateTime.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            day = DateOnly.FromDateTime(timestamp);
            return true;
        }
        return DateOnly.TryParseExact(text, ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out day);
    }
}

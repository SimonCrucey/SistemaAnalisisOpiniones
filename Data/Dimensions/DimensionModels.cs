namespace SistemaAnalisisOpiniones.Data.Dimensions;

public sealed record StagedDimensionRecord(string Source, string? Product, string? Client, string? Date,
    string? Sentiment, string? OriginalId = null, string? Platform = null, string? Comment = null);
public sealed record CatalogAlias(string Entity, string SqlKey, string CsvKey);
public sealed record ProductDimension(string Id, string Name, string? Category);
public sealed record SourceDimension(string Name, string Origin);
public sealed record DateDimension(int Key, DateOnly Date)
{
    public int Quarter => (Date.Month - 1) / 3 + 1;
}

public sealed record DimensionPlan(
    IReadOnlyList<ProductDimension> Products, IReadOnlyList<SourceDimension> Sources,
    IReadOnlyList<string> Sentiments, IReadOnlyList<DateDimension> Dates, IReadOnlyList<string> Clients,
    int MissingProducts, int MissingClients, int InvalidDates, int UnknownSentiments, int MissingSentiments,
    int AbsentClients, IReadOnlyDictionary<string, int> SentimentCounts, IReadOnlyList<CatalogAlias> Aliases);
public sealed record DimensionResult(string Table, int Inserted, int Reused);

public sealed class DimensionConflictException(string dimension) : Exception($"Conflicto en {dimension}.");

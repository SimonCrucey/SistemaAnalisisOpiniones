namespace SistemaAnalisisOpiniones.Models.Csv;

public sealed class RawEtlData
{
    public required CsvFile ClientsFile { get; init; }

    public required CsvFile ProductsFile { get; init; }

    public required CsvFile SourcesFile { get; init; }

    public required CsvFile SocialFile { get; init; }

    public required CsvFile WebFile { get; init; }

    public required CsvFile SurveysFile { get; init; }

    public required IReadOnlyList<ClientCsvRow> Clients { get; init; }

    public required IReadOnlyList<ProductCsvRow> Products { get; init; }

    public required IReadOnlyList<SourceCsvRow> Sources { get; init; }

    public required IReadOnlyList<SocialCommentCsvRow> SocialComments { get; init; }

    public required IReadOnlyList<WebReviewCsvRow> WebReviews { get; init; }

    public required IReadOnlyList<SurveyCsvRow> Surveys { get; init; }
}

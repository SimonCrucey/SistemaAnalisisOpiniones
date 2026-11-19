namespace SistemaAnalisisOpiniones.Models.Csv;

public sealed record ClientCsvRow(
    int RowNumber,
    string ExternalId,
    string Name,
    string Email);

public sealed record ProductCsvRow(
    int RowNumber,
    string ExternalId,
    string Name,
    string Category);

public sealed record SourceCsvRow(
    int RowNumber,
    string ExternalId,
    string SourceType,
    string LoadDate);

public sealed record SocialCommentCsvRow(
    int RowNumber,
    string ExternalId,
    string ClientId,
    string ProductId,
    string Source,
    string Date,
    string Comment);

public sealed record WebReviewCsvRow(
    int RowNumber,
    string ExternalId,
    string ClientId,
    string ProductId,
    string Date,
    string Comment,
    string Rating);

public sealed record SurveyCsvRow(
    int RowNumber,
    string ExternalId,
    string ClientId,
    string ProductId,
    string Date,
    string Comment,
    string Classification,
    string Score,
    string Source);

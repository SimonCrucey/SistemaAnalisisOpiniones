using System.Data;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using SistemaAnalisisOpiniones.Data.Dimensions;
using SistemaAnalisisOpiniones.ETL.Extract;

namespace SistemaAnalisisOpiniones.Data;

public sealed class SqlReviewExtractor(string? connectionString, int timeoutSeconds, string? academicDirectory = null) : IExtractor
{
    public string Source => "resenas-sql";

    public async IAsyncEnumerable<object> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new SqlSourceConfigurationException();
        }

        var originals = academicDirectory is null ? null :
            await BuildOriginalIndexAsync(connectionString, academicDirectory, cancellationToken);

        var settings = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = timeoutSeconds;
        command.CommandText = """
            SELECT o.IdOpinion, o.IdCliente, o.IdProducto, o.IdFuente, o.Fecha,
                   o.Comentario, o.Clasificacion, o.PuntajeSatisfaccion, f.CodigoFuente
            FROM dbo.Opiniones AS o
            INNER JOIN dbo.FuenteDatos AS f ON f.IdFuente = o.IdFuente
            WHERE f.CodigoFuente = @source
            ORDER BY o.IdOpinion;
            """;
        command.Parameters.Add("@source", SqlDbType.VarChar, 100).Value = "PLATAFORMA:Web";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fingerprint = new ReviewSignature(reader.GetInt32(1), reader.GetInt32(2),
                reader.GetDateTime(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetDecimal(7));
            string? originalId = null;
            if (originals is not null && !originals.Remove(fingerprint, out originalId))
                throw new InvalidDataException("Una reseña de SQL carece de correspondencia única con el CSV académico.");
            yield return new ExtractedReview(reader.GetInt32(0), fingerprint.Client, fingerprint.Product,
                reader.GetInt32(3), fingerprint.Date, fingerprint.Comment, fingerprint.Classification,
                fingerprint.Score, reader.GetString(8), originalId);
        }
        if (originals is { Count: > 0 })
            throw new InvalidDataException("Faltan reseñas originales en la extracción SQL.");
    }

    private static async Task<Dictionary<ReviewSignature, string>> BuildOriginalIndexAsync(
        string connectionString, string directory, CancellationToken token)
    {
        var catalog = await new DimensionCatalogReader().ReadAsync(connectionString,
            Path.Combine(directory, "products.csv"), Path.Combine(directory, "clients.csv"), true, token);
        var clientIds = SqlIds(catalog.Aliases, "Cliente");
        var productIds = SqlIds(catalog.Aliases, "Producto");
        var csv = await new CsvFileReader().ReadAsync(Path.Combine(directory, "web_reviews.csv"), token);
        var index = new Dictionary<ReviewSignature, string>();
        foreach (var row in csv.Records)
        {
            token.ThrowIfCancellationRequested();
            var client = DimensionTransformer.CsvKey(csv.Get(row, "IdCliente"), 'C');
            var product = DimensionTransformer.CsvKey(csv.Get(row, "IdProducto"), 'P');
            if (client is null || product is null || !clientIds.TryGetValue(client, out var clientId) ||
                !productIds.TryGetValue(product, out var productId) ||
                !int.TryParse(csv.Get(row, "Rating"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating) ||
                rating is < 1 or > 5 ||
                !DateTime.TryParseExact(csv.Get(row, "Fecha").Trim(), ["yyyy-MM-dd", "yyyy-M-d"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new InvalidDataException("Una reseña original carece de datos o de una equivalencia verificable.");

            var fingerprint = new ReviewSignature(clientId, productId, date, csv.Get(row, "Comentario").Trim(),
                rating <= 2 ? "Negativa" : rating == 3 ? "Neutra" : "Positiva", rating * 20m);
            if (!index.TryAdd(fingerprint, csv.Get(row, "IdReview")))
                throw new InvalidDataException("Hay una equivalencia ambigua entre reseñas originales.");
        }
        return index;
    }

    private static Dictionary<string, int> SqlIds(IEnumerable<CatalogAlias> aliases, string entity) =>
        aliases.Where(a => a.Entity == entity).ToDictionary(a => a.CsvKey,
            a => int.Parse(a.SqlKey.AsSpan(4), CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase);

    private sealed record ReviewSignature(int Client, int Product, DateTime Date, string Comment,
        string Classification, decimal? Score);

    private sealed record ExtractedReview(int IdOpinion, int IdCliente, int IdProducto, int IdFuente,
        DateTime Fecha, string Comentario, string Clasificacion, decimal? PuntajeSatisfaccion, string CodigoFuente,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? IdReviewOriginal);
}

public sealed class SqlSourceConfigurationException : Exception;

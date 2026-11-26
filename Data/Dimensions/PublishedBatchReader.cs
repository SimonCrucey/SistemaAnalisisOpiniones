using System.Text.Json;

namespace SistemaAnalisisOpiniones.Data.Dimensions;

public sealed class PublishedBatchReader
{
    private static readonly string[] ExpectedSources = ["encuestas-csv", "resenas-sql", "comentarios-api"];
    private static readonly Dictionary<string, string> LegacyHashes = new(StringComparer.Ordinal)
    {
        ["comentarios-api"] = "5f512d82cbb72c84724c15e999190025b11f6dbd803f90a1cd89f0a415d45c55",
        ["encuestas-csv"] = "5acd242935d6530baa38be58456f2702f372a855ecfbfca87ffce1cf20f54ce3",
        ["resenas-sql"] = "edcf72d6763fd797948626ad6b62b14b7df415ea7ac0fe7caaad2080e2f98de3"
    };

    public Task<IReadOnlyList<StagedDimensionRecord>> ReadLegacyBaselineAsync(string path, CancellationToken token) =>
        ReadAsync(path, false, token, legacyBaseline: true);

    public async Task<IReadOnlyList<StagedDimensionRecord>> ReadAsync(string path, bool academic, CancellationToken token,
        bool legacyBaseline = false)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists || directory.Name.StartsWith(".pending-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selecciona un directorio de lote publicado.");

        await using var file = File.OpenRead(Path.Combine(directory.FullName, "manifest.json"));
        using var manifest = await JsonDocument.ParseAsync(file, cancellationToken: token);
        var root = manifest.RootElement;
        if (!legacyBaseline && root.GetProperty("environment").GetString() != (academic ? "academic" : "demo"))
            throw new InvalidDataException("El lote y el destino dimensional pertenecen a ambientes distintos.");
        if (academic)
        {
            var hashes = root.GetProperty("fileSha256");
            if (root.GetProperty("transactionDatabase").GetString() != "AnalisisOpiniones" ||
                AcademicFiles.ExpectedHashes.Any(e => !hashes.TryGetProperty(e.Key, out var hash) ||
                    hash.GetString() != e.Value))
                throw new InvalidDataException("El manifiesto no acredita las huellas del conjunto original.");
        }
        if (!root.GetProperty("completedUtc").TryGetDateTimeOffset(out _))
            throw new InvalidDataException("El manifiesto no contiene una fecha de finalización válida.");

        var sources = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("sources").EnumerateArray())
        {
            var name = entry.GetProperty("source").GetString();
            var count = entry.GetProperty("records").GetInt32();
            var elapsed = entry.GetProperty("elapsedMilliseconds").GetInt64();
            if (name is null || !ExpectedSources.Contains(name) || count < 0 || elapsed < 0 || !sources.TryAdd(name, count))
                throw new InvalidDataException("Fuente o conteo inválido en el manifiesto.");
        }
        if (sources.Count != ExpectedSources.Length)
            throw new InvalidDataException("El manifiesto debe contener las tres fuentes.");
        if (academic && (sources["encuestas-csv"] != 500 || sources["resenas-sql"] != 200 || sources["comentarios-api"] != 200))
            throw new InvalidDataException("Conteos académicos distintos de 500/200/200.");

        var records = new List<StagedDimensionRecord>();
        foreach (var source in ExpectedSources)
        {
            var count = 0;
            var commentIds = new HashSet<string>(StringComparer.Ordinal);
            var pathToRead = Path.Combine(directory.FullName, source + ".ndjson");
            if (academic || legacyBaseline)
            {
                var hash = await AcademicFiles.ComputeHashAsync(pathToRead, token);
                var expected = legacyBaseline ? LegacyHashes[source] :
                    root.GetProperty("stagedSha256").GetProperty(source).GetString();
                if (hash != expected)
                    throw new InvalidDataException($"La huella de staging no coincide para {source}.");
            }
            await foreach (var line in File.ReadLinesAsync(pathToRead, token))
            {
                using var row = JsonDocument.Parse(line);
                if (row.RootElement.GetProperty("source").GetString() != source)
                    throw new InvalidDataException($"El origen de una línea no corresponde a {source}.");

                var record = row.RootElement.GetProperty("record");
                records.Add(source switch
                {
                    "encuestas-csv" => ReadSurvey(record),
                    "resenas-sql" => ReadReview(record, academic),
                    _ => ReadComment(record, academic, commentIds)
                });
                count++;
            }
            if (count != sources[source])
                throw new InvalidDataException($"El conteo de {source} no coincide con el manifiesto.");
        }
        return records;
    }

    private static StagedDimensionRecord ReadSurvey(JsonElement record)
    {
        var fields = record.GetProperty("fields");
        return new("encuestas-csv", ReadText(fields, "IdProducto"), ReadText(fields, "IdCliente"),
            ReadText(fields, "Fecha"), ReadText(fields, "Clasificación"), ReadText(fields, "IdOpinion"),
            ReadText(fields, "Fuente"), ReadText(fields, "Comentario"));
    }

    private static StagedDimensionRecord ReadReview(JsonElement record, bool academic)
    {
        if (ReadText(record, "codigoFuente") != "PLATAFORMA:Web")
            throw new InvalidDataException("El registro SQL no corresponde a reseñas web.");
        var originalId = academic ? ReadText(record, "idReviewOriginal") : null;
        if (academic && string.IsNullOrWhiteSpace(originalId))
            throw new InvalidDataException("La reseña SQL carece del identificador original.");
        return new("resenas-sql", ReadId(record, "idProducto"), ReadId(record, "idCliente"),
            ReadText(record, "fecha"), ReadText(record, "clasificacion"), originalId);
    }

    private static StagedDimensionRecord ReadComment(JsonElement record, bool academic, HashSet<string> ids)
    {
        var id = ReadText(record, academic ? "idComment" : "id");
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            throw new InvalidDataException("El registro API carece de identificador único.");
        if (!academic)
            return new("comentarios-api", null, null, ReadText(record, "date"), null, id, null, ReadText(record, "comment"));

        var platform = ReadText(record, "fuente");
        if (platform is not ("Instagram" or "Facebook" or "Twitter"))
            throw new InvalidDataException("Plataforma social desconocida.");
        return new("comentarios-api", ReadText(record, "idProducto"), ReadText(record, "idCliente"),
            ReadText(record, "fecha"), null, id, platform, ReadText(record, "comentario"));
    }

    private static string? ReadText(JsonElement row, string name) => row.GetProperty(name).GetString();

    private static string? ReadId(JsonElement row, string name)
    {
        var value = row.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

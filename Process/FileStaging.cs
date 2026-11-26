using System.Text.Json;
using SistemaAnalisisOpiniones.Data;

namespace SistemaAnalisisOpiniones.Process;

public sealed class FileStaging(string root, AcademicProvenance? provenance)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task VerifySourcesAsync(CancellationToken token) => provenance is null
        ? Task.CompletedTask : AcademicFiles.VerifyDirectoryAsync(provenance.Directory, token);

    public string BeginRun()
    {
        var id = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        return Directory.CreateDirectory(Path.Combine(root, $".pending-{id}")).FullName;
    }

    public async Task<int> WriteAsync(string pending, IExtractor extractor, CancellationToken cancellationToken)
    {
        var name = extractor.Source;
        if (name.Length == 0 || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new InvalidOperationException("El identificador de la fuente debe usar letras ASCII, números o guiones.");
        }

        var path = Path.Combine(pending, $"{name}.ndjson");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await using var writer = new StreamWriter(file);
        var count = 0;
        await foreach (var record in extractor.ReadAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            var json = JsonSerializer.Serialize(new { Source = name, Record = record }, JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            count++;
        }

        return count;
    }

    public async Task<string> CompleteAsync(string pending, IReadOnlyList<SourceResult> results, CancellationToken cancellationToken)
    {
        Dictionary<string, string>? stagedHashes = null;
        if (provenance is not null)
        {
            var counts = results.ToDictionary(r => r.Source, r => r.Records);
            if (counts.Count != 3 || counts.GetValueOrDefault("encuestas-csv") != 500 ||
                counts.GetValueOrDefault("resenas-sql") != 200 || counts.GetValueOrDefault("comentarios-api") != 200)
                throw new InvalidDataException("El lote académico requiere 500 encuestas, 200 reseñas y 200 comentarios.");
            await VerifySourcesAsync(cancellationToken);
            stagedHashes = new(StringComparer.Ordinal);
            foreach (var result in results)
                stagedHashes.Add(result.Source, await AcademicFiles.ComputeHashAsync(
                    Path.Combine(pending, result.Source + ".ndjson"), cancellationToken));
        }
        var manifest = Path.Combine(pending, "manifest.json");
        await using (var stream = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, new { CompletedUtc = DateTimeOffset.UtcNow, Sources = results,
                Environment = provenance is null ? "demo" : "academic",
                TransactionDatabase = provenance?.TransactionDatabase, FileSha256 = provenance?.FileSha256,
                StagedSha256 = stagedHashes },
                JsonOptions, cancellationToken);
        }

        var destination = Path.Combine(root, Path.GetFileName(pending)[".pending-".Length..]);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.Move(pending, destination);
        return destination;
    }
}

public sealed record SourceResult(string Source, int Records, long ElapsedMilliseconds);

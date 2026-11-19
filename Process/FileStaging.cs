using System.Text.Json;
using SistemaAnalisisOpiniones.Data;

namespace SistemaAnalisisOpiniones.Process;

public sealed class FileStaging(string root)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        var manifest = Path.Combine(pending, "manifest.json");
        await using (var stream = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, new { CompletedUtc = DateTimeOffset.UtcNow, Sources = results },
                JsonOptions, cancellationToken);
        }

        var destination = Path.Combine(root, Path.GetFileName(pending)[".pending-".Length..]);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.Move(pending, destination);
        return destination;
    }
}

public sealed record SourceResult(string Source, int Records, long ElapsedMilliseconds);

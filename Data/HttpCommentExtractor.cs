using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SistemaAnalisisOpiniones.Data;

public sealed class HttpCommentExtractor(HttpClient client, Uri endpoint, TimeSpan timeout,
    bool academic = false) : IExtractor
{
    public string Source => "comentarios-api";

    public async IAsyncEnumerable<object> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        var hasAcademicHash = response.Headers.TryGetValues(AcademicFiles.HashHeader, out var hashes);
        if (academic != hasAcademicHash || (academic && !hashes!.SequenceEqual(
            [AcademicFiles.ExpectedHashes[AcademicFiles.Social]], StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("La procedencia de la API no coincide con el ambiente de extracción.");
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, cancellationToken: deadline.Token))
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("La API debe devolver un arreglo de objetos JSON.");
            }

            yield return item;
        }
    }
}

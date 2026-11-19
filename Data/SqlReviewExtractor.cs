using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace SistemaAnalisisOpiniones.Data;

public sealed class SqlReviewExtractor(string? connectionString, int timeoutSeconds) : IExtractor
{
    public string Source => "resenas-sql";

    public async IAsyncEnumerable<object> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new SqlSourceConfigurationException();
        }

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
            yield return new
            {
                IdOpinion = reader.GetInt32(0),
                IdCliente = reader.GetInt32(1),
                IdProducto = reader.GetInt32(2),
                IdFuente = reader.GetInt32(3),
                Fecha = reader.GetDateTime(4),
                Comentario = reader.GetString(5),
                Clasificacion = reader.GetString(6),
                PuntajeSatisfaccion = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7),
                CodigoFuente = reader.GetString(8)
            };
        }
    }
}

public sealed class SqlSourceConfigurationException : Exception;

using System.Data;
using Microsoft.Data.SqlClient;
using SistemaAnalisisOpiniones.ETL.Transform;
using SistemaAnalisisOpiniones.Models.Etl;

namespace SistemaAnalisisOpiniones.ETL.Load;

public sealed class SqlEtlWriter
{
    private const int CommandTimeoutSeconds = 120;
    private readonly string _connectionString;

    public SqlEtlWriter(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("La cadena de conexión es obligatoria.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public LoadResult Write(EtlPlan plan)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            var clientIds = SaveClients(connection, transaction, plan);
            var productIds = SaveProducts(connection, transaction, plan);
            var sourceIds = SaveSources(connection, transaction, plan);

            var opinionsInserted = 0;
            var opinionsDuplicated = 0;

            foreach (var opinion in plan.Opinions.Where(row => row.Estado == EtlStatus.Accepted))
            {
                var clientId = RequireId(clientIds, opinion.CodigoCliente, "cliente");
                var productId = RequireId(productIds, opinion.CodigoProducto, "producto");
                var sourceId = RequireId(sourceIds, opinion.CodigoFuente, "fuente");

                if (FindDuplicate(connection, transaction, clientId, productId, sourceId, opinion) is not null)
                {
                    opinionsDuplicated++;
                    continue;
                }

                InsertOpinion(connection, transaction, clientId, productId, sourceId, opinion);
                opinionsInserted++;
            }

            transaction.Commit();

            return new LoadResult
            {
                ClientsInserted = clientIds.Values.Count(id => id.Inserted),
                ProductsInserted = productIds.Values.Count(id => id.Inserted),
                SourcesInserted = sourceIds.Values.Count(id => id.Inserted),
                OpinionsInserted = opinionsInserted,
                OpinionsDuplicated = opinionsDuplicated
            };
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
            }

            throw;
        }
    }

    private static Dictionary<string, SavedId> SaveClients(
        SqlConnection connection,
        SqlTransaction transaction,
        EtlPlan plan)
    {
        var result = new Dictionary<string, SavedId>(StringComparer.Ordinal);

        foreach (var client in plan.Clients.Where(row => row.Estado == EtlStatus.Valid))
        {
            using var select = CreateCommand(
                connection,
                transaction,
                """
                SELECT TOP (2) IdCliente, Nombre
                FROM dbo.Clientes WITH (UPDLOCK, HOLDLOCK)
                WHERE UPPER(LTRIM(RTRIM(Email))) = @email;
                """);
            select.Parameters.Add("@email", SqlDbType.VarChar, 150).Value = client.EmailNormalizado.ToUpperInvariant();

            var matches = new List<(int Id, string Name)>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    matches.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Hay más de un cliente con el correo '{client.Email}'.");
            }

            if (matches.Count == 1)
            {
                if (!string.Equals(matches[0].Name.Trim(), client.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"El cliente con correo '{client.Email}' ya existe con otro nombre.");
                }

                result.Add(client.CodigoExterno, new SavedId(matches[0].Id, false));
                continue;
            }

            using var insert = CreateCommand(
                connection,
                transaction,
                """
                INSERT INTO dbo.Clientes (Nombre, Email)
                VALUES (@nombre, @email);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """);
            insert.Parameters.Add("@nombre", SqlDbType.VarChar, 100).Value = client.Nombre;
            insert.Parameters.Add("@email", SqlDbType.VarChar, 150).Value = client.Email;
            var id = (int)insert.ExecuteScalar()!;
            result.Add(client.CodigoExterno, new SavedId(id, true));
        }

        return result;
    }

    private static Dictionary<string, SavedId> SaveProducts(
        SqlConnection connection,
        SqlTransaction transaction,
        EtlPlan plan)
    {
        var result = new Dictionary<string, SavedId>(StringComparer.Ordinal);

        foreach (var product in plan.Products.Where(row => row.Estado == EtlStatus.Valid))
        {
            using var select = CreateCommand(
                connection,
                transaction,
                """
                SELECT TOP (2) IdProducto, Nombre, Categoria
                FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK)
                WHERE UPPER(LTRIM(RTRIM(Nombre))) = @nombre;
                """);
            select.Parameters.Add("@nombre", SqlDbType.VarChar, 150).Value = product.NombreNormalizado;

            var matches = new List<(int Id, string Name, string Category)>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    matches.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
                }
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Hay más de un producto llamado '{product.Nombre}'.");
            }

            if (matches.Count == 1)
            {
                if (!string.Equals(matches[0].Name.Trim(), product.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(matches[0].Category.Trim(), product.Categoria.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"El producto '{product.Nombre}' ya existe con otros datos.");
                }

                result.Add(product.CodigoExterno, new SavedId(matches[0].Id, false));
                continue;
            }

            using var insert = CreateCommand(
                connection,
                transaction,
                """
                INSERT INTO dbo.Productos (Nombre, Categoria)
                VALUES (@nombre, @categoria);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """);
            insert.Parameters.Add("@nombre", SqlDbType.VarChar, 150).Value = product.Nombre;
            insert.Parameters.Add("@categoria", SqlDbType.VarChar, 100).Value = product.Categoria;
            var id = (int)insert.ExecuteScalar()!;
            result.Add(product.CodigoExterno, new SavedId(id, true));
        }

        return result;
    }

    private static Dictionary<string, SavedId> SaveSources(
        SqlConnection connection,
        SqlTransaction transaction,
        EtlPlan plan)
    {
        var result = new Dictionary<string, SavedId>(StringComparer.Ordinal);

        foreach (var source in plan.Sources.Where(row => row.Estado == EtlStatus.Valid))
        {
            using var select = CreateCommand(
                connection,
                transaction,
                """
                SELECT TOP (2) IdFuente, TipoFuente, NombreFuente
                FROM dbo.FuenteDatos WITH (UPDLOCK, HOLDLOCK)
                WHERE CodigoFuente = @codigo;
                """);
            select.Parameters.Add("@codigo", SqlDbType.VarChar, 100).Value = source.CodigoExterno;

            var matches = new List<(int Id, string Type, string? Name)>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    matches.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2)));
                }
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Hay más de una fuente con el código '{source.CodigoExterno}'.");
            }

            if (matches.Count == 1)
            {
                if (!string.Equals(matches[0].Type.Trim(), source.TipoFuente.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(matches[0].Name, source.NombreFuente, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"La fuente '{source.CodigoExterno}' ya existe con otros datos.");
                }

                result.Add(source.CodigoExterno, new SavedId(matches[0].Id, false));
                continue;
            }

            using var insert = CreateCommand(
                connection,
                transaction,
                """
                INSERT INTO dbo.FuenteDatos (TipoFuente, FechaCarga, CodigoFuente, NombreFuente)
                VALUES (@tipo, @fecha, @codigo, @nombre);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """);
            insert.Parameters.Add("@tipo", SqlDbType.VarChar, 50).Value = source.TipoFuente;
            insert.Parameters.Add("@fecha", SqlDbType.DateTime).Value = source.FechaCarga ?? DateTime.Now;
            insert.Parameters.Add("@codigo", SqlDbType.VarChar, 100).Value = source.CodigoExterno;
            insert.Parameters.Add("@nombre", SqlDbType.VarChar, 150).Value = source.NombreFuente;
            var id = (int)insert.ExecuteScalar()!;
            result.Add(source.CodigoExterno, new SavedId(id, true));
        }

        return result;
    }

    private static int? FindDuplicate(
        SqlConnection connection,
        SqlTransaction transaction,
        int clientId,
        int productId,
        int sourceId,
        StagedOpinion opinion)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT IdOpinion, Comentario, Clasificacion, PuntajeSatisfaccion
            FROM dbo.Opiniones WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @cliente
              AND IdProducto = @producto
              AND IdFuente = @fuente
              AND CAST(Fecha AS DATE) = @fecha;
            """);
        command.Parameters.Add("@cliente", SqlDbType.Int).Value = clientId;
        command.Parameters.Add("@producto", SqlDbType.Int).Value = productId;
        command.Parameters.Add("@fuente", SqlDbType.Int).Value = sourceId;
        command.Parameters.Add("@fecha", SqlDbType.Date).Value = opinion.Fecha!.Value.Date;

        int? duplicateId = null;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var existingComment = CodeNormalizer.NormalizeComment(reader.GetString(1));
            var incomingComment = CodeNormalizer.NormalizeComment(opinion.Comentario);

            if (!string.Equals(existingComment, incomingComment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sameClassification = string.Equals(
                reader.GetString(2).Trim(),
                opinion.Clasificacion,
                StringComparison.OrdinalIgnoreCase);
            var existingScore = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            var sameScore = existingScore == opinion.Puntaje;

            if (!sameClassification || !sameScore)
            {
                throw new InvalidOperationException(
                    $"La opinión del cliente {clientId}, producto {productId}, fuente {sourceId} y fecha {opinion.Fecha:yyyy-MM-dd} ya existe con otra clasificación o puntaje.");
            }

            duplicateId ??= reader.GetInt32(0);
        }

        return duplicateId;
    }

    private static void InsertOpinion(
        SqlConnection connection,
        SqlTransaction transaction,
        int clientId,
        int productId,
        int sourceId,
        StagedOpinion opinion)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            """
            INSERT INTO dbo.Opiniones
                (IdCliente, IdProducto, IdFuente, Fecha, Comentario, Clasificacion, PuntajeSatisfaccion)
            VALUES
                (@cliente, @producto, @fuente, @fecha, @comentario, @clasificacion, @puntaje);
            """);
        command.Parameters.Add("@cliente", SqlDbType.Int).Value = clientId;
        command.Parameters.Add("@producto", SqlDbType.Int).Value = productId;
        command.Parameters.Add("@fuente", SqlDbType.Int).Value = sourceId;
        command.Parameters.Add("@fecha", SqlDbType.DateTime).Value = opinion.Fecha!.Value;
        command.Parameters.Add("@comentario", SqlDbType.VarChar, -1).Value = opinion.Comentario!;
        command.Parameters.Add("@clasificacion", SqlDbType.VarChar, 20).Value = opinion.Clasificacion!;
        command.Parameters.Add("@puntaje", SqlDbType.Decimal).Value = opinion.Puntaje!.Value;
        command.Parameters["@puntaje"].Precision = 5;
        command.Parameters["@puntaje"].Scale = 2;

        command.ExecuteNonQuery();
    }

    private static int RequireId(IReadOnlyDictionary<string, SavedId> ids, string? code, string kind)
    {
        if (string.IsNullOrWhiteSpace(code) || !ids.TryGetValue(code, out var saved))
        {
            throw new InvalidOperationException($"No se pudo resolver el identificador de {kind} '{code}'.");
        }

        return saved.Id;
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        SqlTransaction transaction,
        string commandText)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.CommandTimeout = CommandTimeoutSeconds;
        return command;
    }

    private sealed record SavedId(int Id, bool Inserted);
}

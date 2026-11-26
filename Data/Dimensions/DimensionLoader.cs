using System.Data;
using Microsoft.Data.SqlClient;

namespace SistemaAnalisisOpiniones.Data.Dimensions;

public sealed class DimensionLoader
{
    public async Task<IReadOnlyList<DimensionResult>> WriteAsync(string connectionString, DimensionPlan plan, CancellationToken token)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        if (connection.Database is not ("DW_OpinionesClientes" or "DW_OpinionesClientes_Demo_ITLA"))
            throw new InvalidOperationException("La conexión debe apuntar a uno de los almacenes dimensionales previstos.");

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            if (connection.Database == "DW_OpinionesClientes")
            {
                await using var preflight = new SqlCommand("""
                    IF OBJECT_ID(N'dbo.EquivalenciaProducto', N'U') IS NULL OR OBJECT_ID(N'dbo.EquivalenciaCliente', N'U') IS NULL
                        THROW 50100, 'Instala el esquema de equivalencias antes de cargar.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.FactOpiniones WITH (TABLOCKX, HOLDLOCK))
                        THROW 50101, 'La carga requiere FactOpiniones vacía.', 1;
                    IF EXISTS (SELECT 1 FROM dbo.DimProducto WHERE ProductoOrigenID LIKE N'SQL:%')
                       OR EXISTS (SELECT 1 FROM dbo.DimCliente WHERE ClienteOrigenID LIKE N'SQL:%')
                       OR EXISTS (SELECT 1 FROM dbo.DimFecha WHERE FechaKey IN (20261001, 20261002, 20261003))
                        THROW 50102, 'Reconcilia primero la carga académica anterior.', 1;
                    """, connection, transaction);
                await preflight.ExecuteNonQueryAsync(token);
            }
            var results = new List<DimensionResult>
            {
                await LoadAsync(connection, transaction, "DimProducto",
                    [("ProductoOrigenID", SqlDbType.NVarChar, 100), ("Nombre", SqlDbType.NVarChar, 200), ("Categoria", SqlDbType.NVarChar, 100)],
                    plan.Products.Select(p => new object?[] { p.Id, p.Name, p.Category }), token),
                await LoadAsync(connection, transaction, "DimFuente",
                    [("Nombre", SqlDbType.NVarChar, 40), ("TipoOrigen", SqlDbType.NVarChar, 40)],
                    plan.Sources.Select(s => new object?[] { s.Name, s.Origin }), token),
                await LoadAsync(connection, transaction, "DimSentimiento",
                    [("Nombre", SqlDbType.NVarChar, 20)], plan.Sentiments.Select(s => new object?[] { s }), token),
                await LoadAsync(connection, transaction, "DimFecha",
                    [("FechaKey", SqlDbType.Int, 0), ("Fecha", SqlDbType.Date, 0), ("Anio", SqlDbType.SmallInt, 0),
                     ("Trimestre", SqlDbType.TinyInt, 0), ("Mes", SqlDbType.TinyInt, 0), ("Dia", SqlDbType.TinyInt, 0)],
                    plan.Dates.Select(d => new object?[] { d.Key, d.Date.ToDateTime(TimeOnly.MinValue), (short)d.Date.Year,
                        (byte)d.Quarter, (byte)d.Date.Month, (byte)d.Date.Day }), token),
                await LoadAsync(connection, transaction, "DimCliente",
                    [("ClienteOrigenID", SqlDbType.NVarChar, 100)],
                    plan.Clients.Select(c => new object?[] { c }), token)
            };
            if (connection.Database == "DW_OpinionesClientes")
            {
                foreach (var group in plan.Aliases.GroupBy(a => a.Entity))
                {
                    var table = group.Key == "Producto" ? "EquivalenciaProducto" : "EquivalenciaCliente";
                    results.Add(await LoadAsync(connection, transaction, table,
                        [("SqlOrigenID", SqlDbType.NVarChar, 100), ("CsvOrigenID", SqlDbType.NVarChar, 100)],
                        group.Select(a => new object?[] { a.SqlKey, a.CsvKey }), token));
                }
            }
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return results;
        }
        catch
        {
            if (transaction.Connection is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // Tabla y columnas se definen únicamente en las llamadas internas de WriteAsync.
    private static async Task<DimensionResult> LoadAsync(SqlConnection connection, SqlTransaction transaction,
        string table, (string Name, SqlDbType Type, int Size)[] columns, IEnumerable<object?[]> candidates,
        CancellationToken token)
    {
        var names = string.Join(", ", columns.Select(c => $"[{c.Name}]"));
        var parameters = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        await using var select = new SqlCommand(
            $"SELECT {names} FROM dbo.[{table}] WITH (UPDLOCK, HOLDLOCK) WHERE [{columns[0].Name}] = @p0", connection, transaction);
        await using var insert = new SqlCommand($"INSERT dbo.[{table}] ({names}) VALUES ({parameters})", connection, transaction);

        select.Parameters.Add("@p0", columns[0].Type, columns[0].Size);
        for (var i = 0; i < columns.Length; i++)
            insert.Parameters.Add($"@p{i}", columns[i].Type, columns[i].Size);

        var inserted = 0;
        var reused = 0;
        foreach (var values in candidates)
        {
            select.Parameters[0].Value = values[0]!;
            await using (var row = await select.ExecuteReaderAsync(token))
            {
                if (await row.ReadAsync(token))
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        var stored = row.IsDBNull(i) ? null : row.GetValue(i);
                        if (!Equals(stored, values[i])) throw new DimensionConflictException(table);
                    }
                    reused++;
                    continue;
                }
            }
            for (var i = 0; i < values.Length; i++)
                insert.Parameters[i].Value = values[i] ?? DBNull.Value;
            await insert.ExecuteNonQueryAsync(token);
            inserted++;
        }
        return new(table, inserted, reused);
    }
}

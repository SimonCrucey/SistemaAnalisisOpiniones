using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SistemaAnalisisOpiniones.Data.Dimensions;

public enum DimensionMigrationMode { Preview, Rehearse, Apply }
public sealed record DimensionMigrationOptions(DimensionMigrationMode? Mode);
public sealed record MigrationAction(string Table, string Action, int Count);

public sealed class DimensionMigration
{
    public async Task<IReadOnlyList<MigrationAction>> RunAsync(string connectionString,
        IReadOnlyList<StagedDimensionRecord> legacy, DimensionPlan corrected, DimensionMigrationMode mode, CancellationToken token)
    {
        var historicalSql = legacy.Where(r => r.Source == "resenas-sql").ToArray();
        var apiDates = legacy.Where(r => r.Source == "comentarios-api").Select(r =>
            int.Parse(DateOnly.ParseExact(r.Date!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
        if (historicalSql.Length != 200 || !apiDates.SequenceEqual([20261001, 20261002, 20261003]) ||
            corrected.Dates.Any(d => apiDates.Contains(d.Key)))
            throw new InvalidDataException("Los lotes no respaldan las fechas obsoletas auditadas.");

        var productKeys = historicalSql.Select(r => "SQL:" + r.Product).ToHashSet(StringComparer.Ordinal);
        var clientKeys = historicalSql.Select(r => "SQL:" + r.Client).ToHashSet(StringComparer.Ordinal);
        var products = corrected.Products.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var productAliases = corrected.Aliases.Where(a => a.Entity == "Producto" && productKeys.Contains(a.SqlKey))
            .Select(a => new { a.SqlKey, a.CsvKey, products[a.CsvKey].Name, products[a.CsvKey].Category }).ToArray();
        var clientAliases = corrected.Aliases.Where(a => a.Entity == "Cliente" && clientKeys.Contains(a.SqlKey))
            .Select(a => new { a.SqlKey, a.CsvKey }).ToArray();
        if (productAliases.Length != productKeys.Count || clientAliases.Length != clientKeys.Count)
            throw new InvalidDataException("Faltan equivalencias unívocas para los registros históricos.");

        var json = JsonSerializer.Serialize(new { Products = productAliases, Clients = clientAliases, Dates = apiDates });
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token);
        if (connection.Database != "DW_OpinionesClientes")
            throw new InvalidOperationException("Destino inválido para la reconciliación.");

        await using var command = new SqlCommand("dbo.usp_ReconciliarDimensionesOriginales", connection)
        { CommandType = CommandType.StoredProcedure, CommandTimeout = 90 };
        command.Parameters.Add("@Plan", SqlDbType.NVarChar, -1).Value = json;
        command.Parameters.Add("@Mode", SqlDbType.TinyInt).Value = (byte)mode;
        await using var reader = await command.ExecuteReaderAsync(token);
        var result = new List<MigrationAction>();
        while (await reader.ReadAsync(token))
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return result;
    }
}

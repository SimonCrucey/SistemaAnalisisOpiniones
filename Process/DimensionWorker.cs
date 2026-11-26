using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SistemaAnalisisOpiniones.Data;
using SistemaAnalisisOpiniones.Data.Dimensions;

namespace SistemaAnalisisOpiniones.Process;

public sealed class DimensionWorker(IConfiguration configuration, PublishedBatchReader batches,
    DimensionCatalogReader catalogs, DimensionTransformer transformer, DimensionLoader loader,
    DimensionMigration migration, DimensionMigrationOptions migrationOptions,
    IHostApplicationLifetime lifetime, ILogger<DimensionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var batch = Required("Dimensions:BatchPath");
            logger.LogInformation("Lote dimensional seleccionado: {Batch}.", new DirectoryInfo(batch).Name);
            var source = Connection("TransactionDb");
            var target = Connection("DataWarehouse");
            var sourceDatabase = new SqlConnectionStringBuilder(source).InitialCatalog;
            var targetDatabase = new SqlConnectionStringBuilder(target).InitialCatalog;
            if ((sourceDatabase, targetDatabase) is not
                (("AnalisisOpiniones", "DW_OpinionesClientes") or
                 ("AnalisisOpiniones_Demo_ITLA", "DW_OpinionesClientes_Demo_ITLA")))
                throw new InvalidDataException("Las conexiones transaccional y dimensional deben pertenecer al mismo entorno previsto.");
            var academic = sourceDatabase == "AnalisisOpiniones";
            var records = await batches.ReadAsync(batch, academic, stoppingToken);
            logger.LogInformation("Registros de staging leídos: {Count}.", records.Count);
            var products = Required("Dimensions:ProductsPath");
            var clients = Required("Dimensions:ClientsPath");
            if (academic)
            {
                await AcademicFiles.VerifyAsync(products, "products.csv", stoppingToken);
                await AcademicFiles.VerifyAsync(clients, "clients.csv", stoppingToken);
            }
            var catalog = await catalogs.ReadAsync(source, products, clients, academic, stoppingToken);
            var plan = transformer.Build(records, catalog, stoppingToken);
            if (migrationOptions.Mode is { } mode)
            {
                if (!academic)
                    throw new InvalidDataException("La reconciliación solo corresponde al DW académico.");
                var baseline = Required("Dimensions:LegacyBatchPath");
                var legacy = await batches.ReadLegacyBaselineAsync(baseline, stoppingToken);
                foreach (var action in await migration.RunAsync(target, legacy, plan, mode, stoppingToken))
                    logger.LogInformation("{Table}: {Action} {Count}.", action.Table, action.Action, action.Count);
                logger.LogInformation("Reconciliación {Mode} finalizada en {Time} ms.", mode, clock.ElapsedMilliseconds);
                return;
            }
            logger.LogInformation("Candidatos: producto {Products}, fuente {Sources}, sentimiento {Sentiments}, fecha {Dates}, cliente {Clients}, evento 0.",
                plan.Products.Count, plan.Sources.Count, plan.Sentiments.Count, plan.Dates.Count, plan.Clients.Count);
            logger.LogInformation("Referencias sin catálogo: producto {Products}, cliente {Clients}. Fechas inválidas: {Dates}. Sentimientos ausentes: {Missing}. Desconocidos: {Unknown}.",
                plan.MissingProducts, plan.MissingClients, plan.InvalidDates, plan.MissingSentiments, plan.UnknownSentiments);
            logger.LogInformation("Clientes ausentes en origen: {Missing}. Equivalencias aplicables: {Aliases}.",
                plan.AbsentClients, plan.Aliases.Count);
            foreach (var platform in records.Where(r => r.Source == "comentarios-api" && r.Platform is not null)
                .GroupBy(r => r.Platform))
                logger.LogInformation("Plataforma social {Platform}: {Count} registros.", platform.Key, platform.Count());
            foreach (var sentiment in plan.SentimentCounts)
                logger.LogInformation("Sentimiento normalizado {Label}: {Count} registros.", sentiment.Key, sentiment.Value);
            foreach (var result in await loader.WriteAsync(target, plan, stoppingToken))
                logger.LogInformation("{Table}: {Inserted} insertados, {Reused} reutilizados.", result.Table, result.Inserted, result.Reused);
            logger.LogInformation("DimEvento: 0 procesados. Las fuentes disponibles no contienen eventos.");
            logger.LogInformation("Carga dimensional confirmada en {Time} ms.", clock.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Environment.ExitCode = 130;
            logger.LogWarning("Carga cancelada en {Time} ms.", clock.ElapsedMilliseconds);
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            var detail = error switch
            {
                DimensionConflictException conflict => conflict.Message,
                SqlException sql => $"SQL Server error {sql.Number}",
                InvalidDataException invalid => invalid.Message,
                JsonException or KeyNotFoundException => "Estructura JSON no válida.",
                IOException => "No se pudo leer un archivo requerido.",
                _ => error.GetType().Name
            };
            logger.LogError("Carga fallida en {Time} ms. {Detail}", clock.ElapsedMilliseconds, detail);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private string Required(string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Falta configuración: {key}.") : value;
    }

    private string Connection(string name) => Required($"ConnectionStrings:{name}");
}

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SistemaAnalisisOpiniones.Data;

namespace SistemaAnalisisOpiniones.Process;

public sealed class Worker(
    IEnumerable<IExtractor> extractors,
    FileStaging staging,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            var pending = staging.BeginRun();
            logger.LogInformation("Iniciando extracción en {Pending}", pending);
            var results = await Task.WhenAll(extractors.Select(extractor => ExtractAsync(pending, extractor, stoppingToken)));
            var destination = await staging.CompleteAsync(pending, results, stoppingToken);
            logger.LogInformation("Lote publicado en {Destination}. Total {Count} registros en {Milliseconds} ms.",
                destination, results.Sum(result => result.Records), elapsed.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Environment.ExitCode = 130;
            logger.LogInformation("Extracción cancelada.");
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            logger.LogError("Falló el lote ({ErrorType}). Revise los eventos de las fuentes y el acceso a staging.",
                exception.GetType().Name);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task<SourceResult> ExtractAsync(string pending, IExtractor extractor, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            var count = await staging.WriteAsync(pending, extractor, cancellationToken);
            logger.LogInformation("{Source}: {Count} registros en {Milliseconds} ms.",
                extractor.Source, count, elapsed.ElapsedMilliseconds);
            return new SourceResult(extractor.Source, count, elapsed.ElapsedMilliseconds);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = exception switch
            {
                Microsoft.Data.SqlClient.SqlException sql => $"SQL Server error {sql.Number}",
                SqlSourceConfigurationException => "configure ConnectionStrings__TransactionDb",
                HttpRequestException http => $"HTTP {http.StatusCode?.ToString() ?? "error de conexión"}",
                OperationCanceledException => "tiempo de espera agotado",
                System.Text.Json.JsonException => "respuesta JSON inválida",
                IOException or UnauthorizedAccessException or Models.Csv.CsvFormatException => exception.Message,
                System.Text.DecoderFallbackException => "archivo CSV con codificación UTF-8 inválida",
                _ => exception.GetType().Name
            };
            logger.LogError("Error en {Source}: {Detail} tras {Milliseconds} ms.", extractor.Source, detail, elapsed.ElapsedMilliseconds);
            throw;
        }
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using SistemaAnalisisOpiniones.Data;
using SistemaAnalisisOpiniones.Data.Dimensions;
using SistemaAnalisisOpiniones.Process;

var command = args.FirstOrDefault();
var migrationMode = command switch
{
    "--preview-dimension-migration" => DimensionMigrationMode.Preview,
    "--rehearse-dimension-migration" => DimensionMigrationMode.Rehearse,
    "--apply-dimension-migration" => DimensionMigrationMode.Apply,
    _ => (DimensionMigrationMode?)null
};
var loadDimensions = command == "--load-dimensions" || migrationMode.HasValue;
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = loadDimensions ? args[1..] : args,
    ContentRootPath = AppContext.BaseDirectory
});

if (loadDimensions)
{
    builder.Services.AddSingleton<PublishedBatchReader>();
    builder.Services.AddSingleton<DimensionCatalogReader>();
    builder.Services.AddSingleton<DimensionTransformer>();
    builder.Services.AddSingleton<DimensionLoader>();
    builder.Services.AddSingleton(new DimensionMigrationOptions(migrationMode));
    builder.Services.AddSingleton<DimensionMigration>();
    builder.Services.AddHostedService<DimensionWorker>();
    using var dimensionHost = builder.Build();
    await dimensionHost.RunAsync();
    return;
}

var options = builder.Configuration.GetSection("Extraction").Get<ExtractionOptions>() ?? new();
var endpoint = options.GetApiEndpoint();
var transactionDb = builder.Configuration.GetConnectionString("TransactionDb");
var database = string.IsNullOrWhiteSpace(transactionDb) ? "" : new SqlConnectionStringBuilder(transactionDb).InitialCatalog;
var academicDirectory = builder.Configuration["Extraction:AcademicDirectory"];
AcademicProvenance? provenance = null;
if (database.Equals("AnalisisOpiniones", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(academicDirectory))
{
    if (string.IsNullOrWhiteSpace(academicDirectory) || !database.Equals("AnalisisOpiniones", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Configura Extraction__AcademicDirectory y la conexión a AnalisisOpiniones.");
    if (!endpoint.IsLoopback)
        throw new InvalidOperationException("La API académica solo puede consumirse por loopback.");
    provenance = new(Path.GetFullPath(academicDirectory), "AnalisisOpiniones");
}
builder.Services.AddSingleton<IExtractor>(_ =>
    new SurveyCsvExtractor(provenance is null ? Path.GetFullPath(options.SurveyPath, AppContext.BaseDirectory)
        : Path.Combine(provenance.Directory, AcademicFiles.Surveys)));
builder.Services.AddSingleton<IExtractor>(_ => new SqlReviewExtractor(
    transactionDb,
    options.SqlCommandTimeoutSeconds, provenance?.Directory));
builder.Services.AddHttpClient("comments")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IExtractor>(services => new HttpCommentExtractor(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("comments"),
    endpoint,
    TimeSpan.FromSeconds(options.ApiTimeoutSeconds), provenance is not null));
builder.Services.AddSingleton(_ => new FileStaging(
    Path.GetFullPath(options.StagingPath, Directory.GetCurrentDirectory()), provenance));
builder.Services.AddHostedService<Worker>();

using var host = builder.Build();
await host.RunAsync();

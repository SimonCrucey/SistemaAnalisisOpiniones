using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SistemaAnalisisOpiniones.Data;
using SistemaAnalisisOpiniones.Process;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var options = builder.Configuration.GetSection("Extraction").Get<ExtractionOptions>() ?? new();
var endpoint = options.GetApiEndpoint();
builder.Services.AddSingleton<IExtractor>(_ =>
    new SurveyCsvExtractor(Path.GetFullPath(options.SurveyPath, AppContext.BaseDirectory)));
builder.Services.AddSingleton<IExtractor>(_ => new SqlReviewExtractor(
    builder.Configuration.GetConnectionString("TransactionDb"),
    options.SqlCommandTimeoutSeconds));
builder.Services.AddHttpClient("comments")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IExtractor>(services => new HttpCommentExtractor(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("comments"),
    endpoint,
    TimeSpan.FromSeconds(options.ApiTimeoutSeconds)));
builder.Services.AddSingleton(_ => new FileStaging(
    Path.GetFullPath(options.StagingPath, Directory.GetCurrentDirectory())));
builder.Services.AddHostedService<Worker>();

using var host = builder.Build();
await host.RunAsync();

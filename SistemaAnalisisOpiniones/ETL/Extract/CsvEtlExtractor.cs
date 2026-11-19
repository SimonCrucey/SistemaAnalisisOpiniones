using SistemaAnalisisOpiniones.Models.Csv;

namespace SistemaAnalisisOpiniones.ETL.Extract;

public sealed class CsvEtlExtractor
{
    public const string ClientsFileName = "clients.csv";
    public const string ProductsFileName = "products.csv";
    public const string SourcesFileName = "fuente_datos.csv";
    public const string SocialFileName = "social_comments.csv";
    public const string WebFileName = "web_reviews.csv";
    public const string SurveysFileName = "surveys_part1.csv";

    private readonly CsvFileReader _reader = new();

    public RawEtlData Load(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            throw new DirectoryNotFoundException($"No se encontró la carpeta de datos: {dataDirectory}");
        }

        var clientsFile = _reader.Read(Path.Combine(dataDirectory, ClientsFileName));
        RequireHeaders(clientsFile, "IdCliente", "Nombre", "Email");

        var productsFile = _reader.Read(Path.Combine(dataDirectory, ProductsFileName));
        RequireHeaders(productsFile, "IdProducto", "Nombre", "Categoría");

        var sourcesFile = _reader.Read(Path.Combine(dataDirectory, SourcesFileName));
        RequireHeaders(sourcesFile, "IdFuente", "TipoFuente", "FechaCarga");

        var socialFile = _reader.Read(Path.Combine(dataDirectory, SocialFileName));
        RequireHeaders(socialFile, "IdComment", "IdCliente", "IdProducto", "Fuente", "Fecha", "Comentario");

        var webFile = _reader.Read(Path.Combine(dataDirectory, WebFileName));
        RequireHeaders(webFile, "IdReview", "IdCliente", "IdProducto", "Fecha", "Comentario", "Rating");

        var surveysFile = _reader.Read(Path.Combine(dataDirectory, SurveysFileName));
        RequireHeaders(surveysFile, "IdOpinion", "IdCliente", "IdProducto", "Fecha", "Comentario", "Clasificación", "PuntajeSatisfacción", "Fuente");

        var clients = clientsFile.Records
            .Select(record => new ClientCsvRow(
                record.LineNumber,
                clientsFile.Get(record, "IdCliente"),
                clientsFile.Get(record, "Nombre"),
                clientsFile.Get(record, "Email")))
            .ToArray();

        var products = productsFile.Records
            .Select(record => new ProductCsvRow(
                record.LineNumber,
                productsFile.Get(record, "IdProducto"),
                productsFile.Get(record, "Nombre"),
                productsFile.Get(record, "Categoría")))
            .ToArray();

        var sources = sourcesFile.Records
            .Select(record => new SourceCsvRow(
                record.LineNumber,
                sourcesFile.Get(record, "IdFuente"),
                sourcesFile.Get(record, "TipoFuente"),
                sourcesFile.Get(record, "FechaCarga")))
            .ToArray();

        var social = socialFile.Records
            .Select(record => new SocialCommentCsvRow(
                record.LineNumber,
                socialFile.Get(record, "IdComment"),
                socialFile.Get(record, "IdCliente"),
                socialFile.Get(record, "IdProducto"),
                socialFile.Get(record, "Fuente"),
                socialFile.Get(record, "Fecha"),
                socialFile.Get(record, "Comentario")))
            .ToArray();

        var web = webFile.Records
            .Select(record => new WebReviewCsvRow(
                record.LineNumber,
                webFile.Get(record, "IdReview"),
                webFile.Get(record, "IdCliente"),
                webFile.Get(record, "IdProducto"),
                webFile.Get(record, "Fecha"),
                webFile.Get(record, "Comentario"),
                webFile.Get(record, "Rating")))
            .ToArray();

        var surveys = surveysFile.Records
            .Select(record => new SurveyCsvRow(
                record.LineNumber,
                surveysFile.Get(record, "IdOpinion"),
                surveysFile.Get(record, "IdCliente"),
                surveysFile.Get(record, "IdProducto"),
                surveysFile.Get(record, "Fecha"),
                surveysFile.Get(record, "Comentario"),
                surveysFile.Get(record, "Clasificación"),
                surveysFile.Get(record, "PuntajeSatisfacción"),
                surveysFile.Get(record, "Fuente")))
            .ToArray();

        return new RawEtlData
        {
            ClientsFile = clientsFile,
            ProductsFile = productsFile,
            SourcesFile = sourcesFile,
            SocialFile = socialFile,
            WebFile = webFile,
            SurveysFile = surveysFile,
            Clients = clients,
            Products = products,
            Sources = sources,
            SocialComments = social,
            WebReviews = web,
            Surveys = surveys
        };
    }

    private static void RequireHeaders(CsvFile file, params string[] headers)
    {
        foreach (var header in headers)
        {
            _ = file.GetIndex(header);
        }
    }
}


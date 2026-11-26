using SistemaAnalisisOpiniones.Data;
using SistemaAnalisisOpiniones.ETL.Extract;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var academicDirectory = app.Configuration["Social:AcademicDirectory"];
if (!string.IsNullOrWhiteSpace(academicDirectory))
{
    app.Use(async (context, next) =>
    {
        if (context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    });
    var path = Path.Combine(Path.GetFullPath(academicDirectory), AcademicFiles.Social);
    await AcademicFiles.VerifyAsync(path, AcademicFiles.Social, CancellationToken.None);
    var csv = await new CsvFileReader().ReadAsync(path, CancellationToken.None);
    await AcademicFiles.VerifyAsync(path, AcademicFiles.Social, CancellationToken.None);
    var comments = csv.Records.Select(row => new OriginalComment(
        csv.Get(row, "IdComment"), EmptyAsNull(csv.Get(row, "IdCliente")), csv.Get(row, "IdProducto"),
        csv.Get(row, "Fuente"), csv.Get(row, "Fecha"), csv.Get(row, "Comentario"))).ToArray();

    app.MapGet("/api/comentarios", (HttpContext context) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers[AcademicFiles.HashHeader] = AcademicFiles.ExpectedHashes[AcademicFiles.Social];
        return Results.Ok(comments);
    });
}
else
{
    app.MapGet("/api/comentarios", () => Results.Ok(new[]
    {
    new AcademicComment("demo-1", "Buen servicio.", new DateOnly(2026, 10, 1), "Ejemplo académico local"),
    new AcademicComment("demo-2", "El pedido llegó tarde.", new DateOnly(2026, 10, 2), "Ejemplo académico local"),
    new AcademicComment("demo-3", "Recibí el pedido.", new DateOnly(2026, 10, 3), "Ejemplo académico local")
    }));
}

app.Run();

static string? EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

internal sealed record AcademicComment(string Id, string Comment, DateOnly Date, string Dataset);
internal sealed record OriginalComment(string IdComment, string? IdCliente, string IdProducto, string Fuente,
    string Fecha, string Comentario);

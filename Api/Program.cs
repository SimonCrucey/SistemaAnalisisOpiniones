var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/comentarios", () => Results.Ok(new[]
{
    new AcademicComment("demo-1", "Buen servicio.", new DateOnly(2026, 10, 1), "Ejemplo académico local"),
    new AcademicComment("demo-2", "El pedido llegó tarde.", new DateOnly(2026, 10, 2), "Ejemplo académico local"),
    new AcademicComment("demo-3", "Recibí el pedido.", new DateOnly(2026, 10, 3), "Ejemplo académico local")
}));

app.Run();

internal sealed record AcademicComment(string Id, string Comment, DateOnly Date, string Dataset);

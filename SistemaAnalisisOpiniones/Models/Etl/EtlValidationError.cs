namespace SistemaAnalisisOpiniones.Models.Etl;

public sealed record EtlValidationError(
    string Archivo,
    int NumeroFila,
    string? IdExterno,
    string Columna,
    string? Valor,
    string Codigo,
    string Mensaje);

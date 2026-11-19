namespace SistemaAnalisisOpiniones.Models.Etl;

public sealed class StagedClient
{
    public int RowNumber { get; init; }

    public required string CodigoExterno { get; set; }

    public required string Nombre { get; init; }

    public required string Email { get; init; }

    public required string EmailNormalizado { get; set; }

    public string Estado { get; set; } = EtlStatus.Valid;

    public string? Motivo { get; set; }
}

public sealed class StagedProduct
{
    public int RowNumber { get; init; }

    public required string CodigoExterno { get; set; }

    public required string Nombre { get; init; }

    public required string Categoria { get; init; }

    public required string NombreNormalizado { get; init; }

    public string Estado { get; set; } = EtlStatus.Valid;

    public string? Motivo { get; set; }
}

public sealed class StagedSource
{
    public int? RowNumber { get; init; }

    public required string CodigoExterno { get; set; }

    public required string TipoFuente { get; init; }

    public required string NombreFuente { get; set; }

    public DateTime? FechaCarga { get; set; }

    public required string ArchivoOrigen { get; init; }

    public string Estado { get; set; } = EtlStatus.Valid;

    public string? Motivo { get; set; }
}

public sealed class StagedOpinion
{
    public required string Archivo { get; init; }

    public int NumeroFila { get; init; }

    public string? IdOpinionExterno { get; init; }

    public string? IdClienteExterno { get; init; }

    public string? IdProductoExterno { get; init; }

    public string? CodigoCliente { get; set; }

    public string? CodigoProducto { get; set; }

    public string? CodigoFuente { get; set; }

    public string? FuenteOriginal { get; init; }

    public string? FechaOriginal { get; init; }

    public DateTime? Fecha { get; set; }

    public string? Comentario { get; set; }

    public string? Clasificacion { get; set; }

    public decimal? Puntaje { get; set; }

    public int? Rating { get; set; }

    public string Estado { get; set; } = EtlStatus.Pending;

    public string? Motivo { get; set; }
}

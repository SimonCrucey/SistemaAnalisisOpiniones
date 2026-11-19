namespace SistemaAnalisisOpiniones.Models.Etl;

public sealed class LoadResult
{
    public int ClientsInserted { get; init; }

    public int ProductsInserted { get; init; }

    public int SourcesInserted { get; init; }

    public int OpinionsInserted { get; init; }

    public int OpinionsDuplicated { get; init; }
}

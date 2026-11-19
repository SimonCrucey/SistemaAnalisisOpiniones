namespace SistemaAnalisisOpiniones.Models.Etl;

public sealed class EtlPlan
{
    public List<StagedClient> Clients { get; } = [];

    public List<StagedProduct> Products { get; } = [];

    public List<StagedSource> Sources { get; } = [];

    public List<StagedOpinion> Opinions { get; } = [];

    public List<EtlValidationError> Errors { get; } = [];

    public int AcceptedOpinions => Opinions.Count(opinion => opinion.Estado == EtlStatus.Accepted);

    public int PendingOpinions => Opinions.Count(opinion => opinion.Estado == EtlStatus.Pending);

    public int RejectedOpinions => Opinions.Count(opinion => opinion.Estado == EtlStatus.Rejected);

    public int ValidClients => Clients.Count(client => client.Estado == EtlStatus.Valid);

    public int ValidProducts => Products.Count(product => product.Estado == EtlStatus.Valid);

    public int ValidSources => Sources.Count(source => source.Estado == EtlStatus.Valid);

    public void AddError(
        string archivo,
        int numeroFila,
        string? idExterno,
        string columna,
        string? valor,
        string codigo,
        string mensaje)
    {
        Errors.Add(new EtlValidationError(archivo, numeroFila, idExterno, columna, valor, codigo, mensaje));
    }
}

namespace SistemaAnalisisOpiniones.Process;

public sealed class ExtractionOptions
{
    public string SurveyPath { get; set; } = "Data/surveys_part1.csv";
    public string StagingPath { get; set; } = "staging";
    public string ApiEndpoint { get; set; } = "http://127.0.0.1:5187/api/comentarios";
    public int ApiTimeoutSeconds { get; set; } = 15;
    public int SqlCommandTimeoutSeconds { get; set; } = 30;

    public Uri GetApiEndpoint()
    {
        if (!Uri.TryCreate(ApiEndpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.IsLoopback && endpoint.Scheme == Uri.UriSchemeHttp)) ||
            endpoint.UserInfo.Length != 0)
        {
            throw new InvalidOperationException("Use HTTPS para la API o HTTP para un endpoint local, sin credenciales en la URL.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ApiTimeoutSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SqlCommandTimeoutSeconds);
        return endpoint;
    }
}

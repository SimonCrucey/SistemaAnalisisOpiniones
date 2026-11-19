namespace SistemaAnalisisOpiniones.Models.Csv;

public sealed class CsvFormatException : Exception
{
    public CsvFormatException(string message) : base(message)
    {
    }

    public CsvFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

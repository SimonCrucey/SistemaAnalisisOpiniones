using SistemaAnalisisOpiniones.ETL.Extract;
using SistemaAnalisisOpiniones.ETL.Load;
using SistemaAnalisisOpiniones.ETL.Transform;

namespace SistemaAnalisisOpiniones;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ReadOptions(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(HelpText);
                return 0;
            }

            var dataDirectory = ResolveDataDirectory(options.DataDirectory);
            Console.WriteLine($"Modo: {(options.Execute ? "EXECUTE" : "DRY_RUN")}");
            Console.WriteLine($"Carpeta de datos: {dataDirectory}");

            var raw = new CsvEtlExtractor().Load(dataDirectory);
            var plan = new EtlPlanBuilder().Build(raw);

            Console.WriteLine($"Clientes leídos: {plan.Clients.Count}");
            Console.WriteLine($"Productos leídos: {plan.Products.Count}");
            Console.WriteLine($"Fuentes leídas/creadas: {plan.Sources.Count}");
            Console.WriteLine($"Opiniones leídas: {plan.Opinions.Count}");
            Console.WriteLine($"Aceptadas: {plan.AcceptedOpinions}");
            Console.WriteLine($"Pendientes: {plan.PendingOpinions}");
            Console.WriteLine($"Rechazadas: {plan.RejectedOpinions}");
            Console.WriteLine($"Errores de validación: {plan.Errors.Count}");

            if (!options.Execute)
            {
                Console.WriteLine("Dry-run completado. No se abrió conexión ni se modificó SQL Server.");
                return 0;
            }

            var connectionString = Environment.GetEnvironmentVariable("ETL_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Defina ETL_CONNECTION_STRING antes de usar --execute.");
            }

            var result = new SqlEtlWriter(connectionString).Write(plan);

            Console.WriteLine($"Clientes insertados: {result.ClientsInserted}");
            Console.WriteLine($"Productos insertados: {result.ProductsInserted}");
            Console.WriteLine($"Fuentes insertadas: {result.SourcesInserted}");
            Console.WriteLine($"Opiniones insertadas: {result.OpinionsInserted}");
            Console.WriteLine($"Opiniones duplicadas omitidas: {result.OpinionsDuplicated}");
            Console.WriteLine("Carga finalizada correctamente.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ETL detenido: {exception.Message}");
            return 1;
        }
    }

    private static AppOptions ReadOptions(string[] args)
    {
        var execute = false;
        var dryRun = false;
        var showHelp = false;
        string? dataDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--execute":
                    execute = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--data":
                    if (index + 1 >= args.Length)
                    {
                        throw new ArgumentException("La opción --data requiere una ruta.");
                    }

                    dataDirectory = args[++index];
                    break;
                default:
                    throw new ArgumentException($"Argumento no reconocido: {args[index]}. Use --help.");
            }
        }

        if (execute && dryRun)
        {
            throw new ArgumentException("No se pueden usar --execute y --dry-run al mismo tiempo.");
        }

        return new AppOptions(execute, showHelp, dataDirectory);
    }

    private static string ResolveDataDirectory(string? configuredPath)
    {
        var path = configuredPath ?? "Data";
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var current = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        if (configuredPath is null && !File.Exists(Path.Combine(current, CsvEtlExtractor.ClientsFileName)))
        {
            return Path.Combine(AppContext.BaseDirectory, path);
        }

        return Directory.Exists(current)
            ? current
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    private sealed record AppOptions(bool Execute, bool ShowHelp, string? DataDirectory);

    private const string HelpText = """
        Uso:
          dotnet run -- [--dry-run | --execute] [--data <ruta>]

        Modos:
          --dry-run   Valida los CSV sin conectarse a SQL Server. Es el modo predeterminado.
          --execute   Carga los datos. Requiere la variable ETL_CONNECTION_STRING.

        Opciones:
          --data      Carpeta que contiene los seis CSV. Predeterminado: Data.
          --help      Muestra esta ayuda.
        """;
}

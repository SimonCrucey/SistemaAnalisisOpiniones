using System.Globalization;
using Microsoft.Data.SqlClient;
using SistemaAnalisisOpiniones.ETL.Extract;

namespace SistemaAnalisisOpiniones.Data.Dimensions;

public sealed record DimensionCatalog(IReadOnlyDictionary<string, ProductDimension> Products,
    IReadOnlyDictionary<string, string> Clients, IReadOnlyList<CatalogAlias> Aliases);

public sealed class DimensionCatalogReader
{
    public async Task<DimensionCatalog> ReadAsync(string connectionString, string productsPath, string clientsPath,
        bool academic, CancellationToken token)
    {
        var products = new Dictionary<string, ProductDimension>(StringComparer.OrdinalIgnoreCase);
        var clients = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientEmails = new List<(string Key, string Email)>();
        var sqlClients = new List<(string Key, string Email)>();
        var sqlProducts = new Dictionary<string, ProductDimension>(StringComparer.OrdinalIgnoreCase);
        var aliases = new List<CatalogAlias>();
        var reader = new CsvFileReader();
        var csvProducts = await reader.ReadAsync(productsPath, token);
        foreach (var column in new[] { "IdProducto", "Nombre", "Categoría" }) csvProducts.GetIndex(column);
        foreach (var row in csvProducts.Records)
        {
            var key = DimensionTransformer.CsvKey(csvProducts.Get(row, "IdProducto"), 'P');
            if (key is null) throw new InvalidDataException("Código de producto inválido en el catálogo CSV.");
            AddProduct(products, key, csvProducts.Get(row, "Nombre"), csvProducts.Get(row, "Categoría"));
        }

        var csvClients = await reader.ReadAsync(clientsPath, token);
        csvClients.GetIndex("IdCliente");
        foreach (var row in csvClients.Records)
        {
            var key = DimensionTransformer.CsvKey(csvClients.Get(row, "IdCliente"), 'C');
            if (key is null) throw new InvalidDataException("Código de cliente inválido en el catálogo CSV.");
            clients.Add(key, key);
            if (academic) clientEmails.Add((key, csvClients.Get(row, "Email").Trim().ToUpperInvariant()));
        }

        var settings = new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = academic
            ? "SELECT IdProducto, Nombre, Categoria FROM dbo.Productos; SELECT IdCliente, Email FROM dbo.Clientes;"
            : "SELECT IdProducto, Nombre, Categoria FROM dbo.Productos; SELECT IdCliente FROM dbo.Clientes;";
        await using var rows = await command.ExecuteReaderAsync(token);
        while (await rows.ReadAsync(token))
        {
            AddProduct(sqlProducts, "SQL:" + rows.GetInt32(0).ToString(CultureInfo.InvariantCulture), rows.GetString(1),
                rows.IsDBNull(2) ? null : rows.GetString(2));
        }
        await rows.NextResultAsync(token);
        while (await rows.ReadAsync(token))
        {
            var key = "SQL:" + rows.GetInt32(0).ToString(CultureInfo.InvariantCulture);
            if (academic) sqlClients.Add((key, rows.GetString(1).Trim().ToUpperInvariant()));
            else clients.Add(key, key);
        }

        if (academic)
        {
            var csvProductGroups = products.Values.ToLookup(ProductMatchKey);
            foreach (var group in sqlProducts.Values.ToLookup(ProductMatchKey))
            {
                var matches = csvProductGroups[group.Key].ToArray();
                if (group.Count() != 1 || matches.Length != 1) continue;
                var sqlKey = group.Single().Id;
                products.Add(sqlKey, matches[0]);
                aliases.Add(new("Producto", sqlKey, matches[0].Id));
            }

            var csvEmailGroups = clientEmails.ToLookup(c => c.Email);
            foreach (var group in sqlClients.ToLookup(c => c.Email))
            {
                var matches = csvEmailGroups[group.Key].ToArray();
                if (group.Key.Length == 0 || group.Count() != 1 || matches.Length != 1) continue;
                var sqlKey = group.Single().Key;
                clients.Add(sqlKey, matches[0].Key);
                aliases.Add(new("Cliente", sqlKey, matches[0].Key));
            }
        }
        else
        {
            foreach (var pair in sqlProducts) products.Add(pair.Key, pair.Value);
        }
        return new(products, clients, aliases.OrderBy(a => a.Entity, StringComparer.Ordinal)
            .ThenBy(a => a.SqlKey, StringComparer.Ordinal).ToArray());
    }

    private static (string Name, string Category) ProductMatchKey(ProductDimension p) =>
        (Normalize(p.Name), Normalize(p.Category ?? ""));

    private static string Normalize(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static void AddProduct(Dictionary<string, ProductDimension> products, string id, string name, string? category)
    {
        var candidate = new ProductDimension(id, name.Trim(), string.IsNullOrWhiteSpace(category) ? null : category.Trim());
        if (candidate.Name.Length is 0 or > 200 || candidate.Category?.Length > 100)
            throw new InvalidDataException("Nombre o categoría fuera del esquema de DimProducto.");
        if (products.TryGetValue(id, out var previous) && previous != candidate)
            throw new DimensionConflictException("catálogo de productos");
        products[id] = candidate;
    }
}

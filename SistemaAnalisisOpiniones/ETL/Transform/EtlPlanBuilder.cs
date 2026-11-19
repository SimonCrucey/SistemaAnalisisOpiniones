using SistemaAnalisisOpiniones.ETL.Extract;
using SistemaAnalisisOpiniones.Models.Csv;
using SistemaAnalisisOpiniones.Models.Etl;

namespace SistemaAnalisisOpiniones.ETL.Transform;

public sealed class EtlPlanBuilder
{
    private const string SyntheticArchive = "(constante)";

    public EtlPlan Build(RawEtlData raw)
    {
        var plan = new EtlPlan();
        var clients = BuildClients(raw, plan);
        var products = BuildProducts(raw, plan);
        var sources = BuildSources(raw, plan);

        BuildSocialOpinions(raw, clients, products, plan);
        BuildWebOpinions(raw, clients, products, plan);
        BuildSurveyOpinions(raw, clients, products, plan);

        return plan;
    }

    private static Dictionary<string, StagedClient> BuildClients(RawEtlData raw, EtlPlan plan)
    {
        var lookup = new Dictionary<string, StagedClient>(StringComparer.Ordinal);
        var emails = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw.Clients)
        {
            var normalized = CodeNormalizer.NormalizeEmail(row.Email);
            var staged = new StagedClient
            {
                RowNumber = row.RowNumber,
                CodigoExterno = row.ExternalId?.Trim() ?? string.Empty,
                Nombre = row.Name?.Trim() ?? string.Empty,
                Email = row.Email?.Trim() ?? string.Empty,
                EmailNormalizado = normalized,
            };

            if (!CodeNormalizer.TryNormalizeClientCode(row.ExternalId, out var code))
            {
                Reject(staged, plan, raw.ClientsFile.FileName, "IdCliente", row.ExternalId, "ID_CLIENTE_INVALIDO", "El identificador de cliente no es válido.");
            }
            else
            {
                staged.CodigoExterno = code;
                if (string.IsNullOrWhiteSpace(staged.Nombre))
                {
                    Reject(staged, plan, raw.ClientsFile.FileName, "Nombre", staged.Nombre, "NOMBRE_REQUERIDO", "El nombre del cliente es obligatorio.");
                }
                else if (staged.Nombre.Length > 100)
                {
                    Reject(staged, plan, raw.ClientsFile.FileName, "Nombre", staged.Nombre, "NOMBRE_LARGO", "El nombre del cliente supera VARCHAR(100).");
                }
                else if (!CodeNormalizer.IsValidEmail(row.Email, out var validEmail))
                {
                    Reject(staged, plan, raw.ClientsFile.FileName, "Email", row.Email, "EMAIL_INVALIDO", "El correo electrónico no es válido o supera VARCHAR(150).");
                }
                else
                {
                    staged.EmailNormalizado = validEmail;
                    if (lookup.ContainsKey(code))
                    {
                        Reject(staged, plan, raw.ClientsFile.FileName, "IdCliente", row.ExternalId, "CLIENTE_DUPLICADO", "El identificador de cliente está repetido en el archivo.");
                    }
                    else if (!emails.Add(validEmail))
                    {
                        Reject(staged, plan, raw.ClientsFile.FileName, "Email", row.Email, "EMAIL_DUPLICADO", "El correo electrónico está repetido en el archivo.");
                    }
                    else
                    {
                        lookup.Add(code, staged);
                    }
                }
            }

            plan.Clients.Add(staged);
        }

        return lookup;
    }

    private static Dictionary<string, StagedProduct> BuildProducts(RawEtlData raw, EtlPlan plan)
    {
        var lookup = new Dictionary<string, StagedProduct>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw.Products)
        {
            var staged = new StagedProduct
            {
                RowNumber = row.RowNumber,
                CodigoExterno = row.ExternalId?.Trim() ?? string.Empty,
                Nombre = row.Name?.Trim() ?? string.Empty,
                Categoria = row.Category?.Trim() ?? string.Empty,
                NombreNormalizado = CodeNormalizer.NormalizeNameKey(row.Name),
            };

            if (!CodeNormalizer.TryNormalizeProductCode(row.ExternalId, out var code))
            {
                Reject(staged, plan, raw.ProductsFile.FileName, "IdProducto", row.ExternalId, "ID_PRODUCTO_INVALIDO", "El identificador de producto no es válido.");
            }
            else
            {
                staged.CodigoExterno = code;
                if (string.IsNullOrWhiteSpace(staged.Nombre))
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "Nombre", staged.Nombre, "NOMBRE_REQUERIDO", "El nombre del producto es obligatorio.");
                }
                else if (staged.Nombre.Length > 150)
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "Nombre", staged.Nombre, "NOMBRE_LARGO", "El nombre del producto supera VARCHAR(150).");
                }
                else if (string.IsNullOrWhiteSpace(staged.Categoria))
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "Categoría", staged.Categoria, "CATEGORIA_REQUERIDA", "La categoría del producto es obligatoria.");
                }
                else if (staged.Categoria.Length > 100)
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "Categoría", staged.Categoria, "CATEGORIA_LARGA", "La categoría del producto supera VARCHAR(100).");
                }
                else if (lookup.ContainsKey(code))
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "IdProducto", row.ExternalId, "PRODUCTO_DUPLICADO", "El identificador de producto está repetido en el archivo.");
                }
                else if (!names.Add(staged.NombreNormalizado))
                {
                    Reject(staged, plan, raw.ProductsFile.FileName, "Nombre", row.Name, "PRODUCTO_NOMBRE_DUPLICADO", "El nombre normalizado del producto está repetido en el archivo.");
                }
                else
                {
                    lookup.Add(code, staged);
                }
            }

            plan.Products.Add(staged);
        }

        return lookup;
    }

    private static Dictionary<string, StagedSource> BuildSources(RawEtlData raw, EtlPlan plan)
    {
        var lookup = new Dictionary<string, StagedSource>(StringComparer.Ordinal);

        foreach (var row in raw.Sources)
        {
            var staged = new StagedSource
            {
                RowNumber = row.RowNumber,
                CodigoExterno = row.ExternalId?.Trim() ?? string.Empty,
                TipoFuente = row.SourceType?.Trim() ?? string.Empty,
                NombreFuente = row.ExternalId?.Trim() ?? string.Empty,
                ArchivoOrigen = raw.SourcesFile.FileName,
            };

            if (!CodeNormalizer.TryNormalizeSourceCode(row.ExternalId, out var code))
            {
                Reject(staged, plan, raw.SourcesFile.FileName, "IdFuente", row.ExternalId, "ID_FUENTE_INVALIDO", "El identificador de fuente no es válido.");
            }
            else
            {
                staged.CodigoExterno = code;
                staged.NombreFuente = code["FUENTE:".Length..];
                if (!CodeNormalizer.IsValidSourceType(staged.TipoFuente))
                {
                    Reject(staged, plan, raw.SourcesFile.FileName, "TipoFuente", staged.TipoFuente, "TIPO_FUENTE_INVALIDO", "El tipo de fuente no está permitido.");
                }
                else if (staged.TipoFuente.Length > 50)
                {
                    Reject(staged, plan, raw.SourcesFile.FileName, "TipoFuente", staged.TipoFuente, "TIPO_FUENTE_LARGO", "El tipo de fuente supera VARCHAR(50).");
                }
                else if (!CodeNormalizer.TryParseDate(row.LoadDate, out var date))
                {
                    Reject(staged, plan, raw.SourcesFile.FileName, "FechaCarga", row.LoadDate, "FECHA_INVALIDA", "La fecha de carga no tiene el formato yyyy-MM-dd.");
                }
                else if (lookup.ContainsKey(code))
                {
                    Reject(staged, plan, raw.SourcesFile.FileName, "IdFuente", row.ExternalId, "FUENTE_DUPLICADA", "El identificador de fuente está repetido en el archivo.");
                }
                else
                {
                    staged.FechaCarga = date;
                    lookup.Add(code, staged);
                }
            }

            plan.Sources.Add(staged);
        }

        foreach (var explicitSource in CreateExplicitSources())
        {
            if (lookup.ContainsKey(explicitSource.CodigoExterno))
            {
                continue;
            }

            lookup.Add(explicitSource.CodigoExterno, explicitSource);
            plan.Sources.Add(explicitSource);
        }

        return lookup;
    }

    private static void BuildSocialOpinions(
        RawEtlData raw,
        IReadOnlyDictionary<string, StagedClient> clients,
        IReadOnlyDictionary<string, StagedProduct> products,
        EtlPlan plan)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw.SocialComments)
        {
            var opinion = CreateOpinion(raw.SocialFile.FileName, row.RowNumber, row.ExternalId, row.ClientId, row.ProductId, row.Source, row.Date, row.Comment);
            ValidateExternalId(opinion, identifiers, plan);

            BindClient(opinion, clients, plan);
            BindProduct(opinion, products, plan);
            BindDate(opinion, plan);
            BindComment(opinion, plan);
            BindPlatform(opinion, plan, allowWeb: false, allowSurvey: false);

            if (opinion.Estado != EtlStatus.Rejected)
            {
                opinion.Estado = EtlStatus.Pending;
                opinion.Motivo = "SIN_CLASIFICACION";
            }

            plan.Opinions.Add(opinion);
        }
    }

    private static void BuildWebOpinions(
        RawEtlData raw,
        IReadOnlyDictionary<string, StagedClient> clients,
        IReadOnlyDictionary<string, StagedProduct> products,
        EtlPlan plan)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw.WebReviews)
        {
            var opinion = CreateOpinion(raw.WebFile.FileName, row.RowNumber, row.ExternalId, row.ClientId, row.ProductId, "Web", row.Date, row.Comment);
            opinion.CodigoFuente = CodeNormalizer.WebSourceCode;
            ValidateExternalId(opinion, identifiers, plan);
            BindClient(opinion, clients, plan);
            BindProduct(opinion, products, plan);
            BindDate(opinion, plan);
            BindComment(opinion, plan);

            if (CodeNormalizer.TryParseInt(row.Rating, out var rating) && rating is >= 1 and <= 5)
            {
                opinion.Rating = rating;
                opinion.Puntaje = rating * 20m;
                opinion.Clasificacion = rating switch
                {
                    1 or 2 => "Negativa",
                    3 => "Neutra",
                    _ => "Positiva"
                };
            }
            else
            {
                Reject(opinion, plan, "Rating", row.Rating, "RATING_INVALIDO", "El rating debe ser un entero entre 1 y 5.");
            }

            opinion.Estado = opinion.Motivo is null ? EtlStatus.Accepted : EtlStatus.Rejected;
            plan.Opinions.Add(opinion);
        }
    }

    private static void BuildSurveyOpinions(
        RawEtlData raw,
        IReadOnlyDictionary<string, StagedClient> clients,
        IReadOnlyDictionary<string, StagedProduct> products,
        EtlPlan plan)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw.Surveys)
        {
            var opinion = CreateOpinion(raw.SurveysFile.FileName, row.RowNumber, row.ExternalId, row.ClientId, row.ProductId, row.Source, row.Date, row.Comment);
            opinion.CodigoFuente = CodeNormalizer.SurveySourceCode;
            ValidateExternalId(opinion, identifiers, plan);
            BindClient(opinion, clients, plan);
            BindProduct(opinion, products, plan);
            BindDate(opinion, plan);
            BindComment(opinion, plan);

            if (!CodeNormalizer.TryNormalizePlatformCode(row.Source, out var sourceCode) || sourceCode != CodeNormalizer.SurveySourceCode)
            {
                Reject(opinion, plan, "Fuente", row.Source, "FUENTE_NO_SOPORTADA", "La fuente de la encuesta debe ser EncuestaInterna.");
            }

            if (CodeNormalizer.TryNormalizeClassification(row.Classification, out var classification))
            {
                opinion.Clasificacion = classification;
            }
            else
            {
                Reject(opinion, plan, "Clasificación", row.Classification, "CLASIFICACION_INVALIDA", "La clasificación debe ser Positiva, Neutra o Negativa.");
            }

            if (CodeNormalizer.TryParseInt(row.Score, out var score) && score is >= 1 and <= 5)
            {
                opinion.Puntaje = score * 20m;
            }
            else
            {
                Reject(opinion, plan, "PuntajeSatisfacción", row.Score, "PUNTAJE_INVALIDO", "El puntaje debe ser un entero entre 1 y 5.");
            }

            if (opinion.Motivo is null && !IsSurveyClassificationConsistent(opinion.Clasificacion, score))
            {
                Reject(
                    opinion,
                    plan,
                    "Clasificación/PuntajeSatisfacción",
                    $"{row.Classification}/{row.Score}",
                    "CLASIFICACION_PUNTAJE_INCONSISTENTE",
                    "La clasificación de la encuesta no corresponde al puntaje.");
            }

            opinion.Estado = opinion.Motivo is null ? EtlStatus.Accepted : EtlStatus.Rejected;
            plan.Opinions.Add(opinion);
        }
    }

    private static StagedOpinion CreateOpinion(
        string archive,
        int rowNumber,
        string? externalId,
        string? clientId,
        string? productId,
        string? source,
        string? date,
        string? comment)
    {
        var parsedDate = CodeNormalizer.TryParseDate(date, out var validDate) ? validDate : (DateTime?)null;

        return new StagedOpinion
        {
            Archivo = archive,
            NumeroFila = rowNumber,
            IdOpinionExterno = externalId?.Trim(),
            IdClienteExterno = clientId?.Trim(),
            IdProductoExterno = productId?.Trim(),
            FuenteOriginal = source?.Trim(),
            FechaOriginal = date?.Trim(),
            Fecha = parsedDate,
            Comentario = comment?.Trim()
        };
    }

    private static void ValidateExternalId(StagedOpinion opinion, HashSet<string> identifiers, EtlPlan plan)
    {
        if (string.IsNullOrWhiteSpace(opinion.IdOpinionExterno))
        {
            Reject(opinion, plan, "IdOpinion", opinion.IdOpinionExterno, "ID_OPINION_REQUERIDO", "El identificador de la opinión es obligatorio.");
            return;
        }

        if (!identifiers.Add(opinion.IdOpinionExterno))
        {
            Reject(opinion, plan, "IdOpinion", opinion.IdOpinionExterno, "DUPLICADO_EN_ARCHIVO", "El identificador de la opinión está repetido en el archivo.");
        }
    }

    private static void BindClient(StagedOpinion opinion, IReadOnlyDictionary<string, StagedClient> clients, EtlPlan plan)
    {
        if (string.IsNullOrWhiteSpace(opinion.IdClienteExterno))
        {
            Reject(opinion, plan, "IdCliente", opinion.IdClienteExterno, "CLIENTE_REQUERIDO", "La opinión no tiene cliente.");
            return;
        }

        if (!CodeNormalizer.TryNormalizeClientCode(opinion.IdClienteExterno, out var code))
        {
            Reject(opinion, plan, "IdCliente", opinion.IdClienteExterno, "CLIENTE_INVALIDO", "El identificador de cliente no es válido.");
            return;
        }

        opinion.CodigoCliente = code;
        if (!clients.ContainsKey(code))
        {
            Reject(opinion, plan, "IdCliente", opinion.IdClienteExterno, "CLIENTE_NO_MAPEADO", "El cliente no existe en clients.csv.");
        }
    }

    private static void BindProduct(StagedOpinion opinion, IReadOnlyDictionary<string, StagedProduct> products, EtlPlan plan)
    {
        if (string.IsNullOrWhiteSpace(opinion.IdProductoExterno))
        {
            Reject(opinion, plan, "IdProducto", opinion.IdProductoExterno, "PRODUCTO_REQUERIDO", "La opinión no tiene producto.");
            return;
        }

        if (!CodeNormalizer.TryNormalizeProductCode(opinion.IdProductoExterno, out var code))
        {
            Reject(opinion, plan, "IdProducto", opinion.IdProductoExterno, "PRODUCTO_INVALIDO", "El identificador de producto no es válido.");
            return;
        }

        opinion.CodigoProducto = code;
        if (!products.ContainsKey(code))
        {
            Reject(opinion, plan, "IdProducto", opinion.IdProductoExterno, "PRODUCTO_NO_MAPEADO", "El producto no existe en products.csv.");
        }
    }

    private static void BindDate(StagedOpinion opinion, EtlPlan plan)
    {
        if (opinion.Fecha.HasValue)
        {
            return;
        }

        Reject(opinion, plan, "Fecha", opinion.FechaOriginal, "FECHA_INVALIDA", "La fecha no tiene el formato yyyy-MM-dd.");
    }

    private static void BindComment(StagedOpinion opinion, EtlPlan plan)
    {
        if (string.IsNullOrWhiteSpace(opinion.Comentario))
        {
            Reject(opinion, plan, "Comentario", opinion.Comentario, "COMENTARIO_REQUERIDO", "El comentario es obligatorio.");
        }
    }

    private static void BindPlatform(StagedOpinion opinion, EtlPlan plan, bool allowWeb, bool allowSurvey)
    {
        if (!CodeNormalizer.TryNormalizePlatformCode(opinion.FuenteOriginal, out var code))
        {
            Reject(opinion, plan, "Fuente", opinion.FuenteOriginal, "FUENTE_NO_SOPORTADA", "La fuente no es una plataforma admitida.");
            return;
        }

        if (code == CodeNormalizer.WebSourceCode && !allowWeb)
        {
            Reject(opinion, plan, "Fuente", opinion.FuenteOriginal, "FUENTE_NO_SOPORTADA", "La fuente Web no corresponde a este archivo.");
            return;
        }

        if (code == CodeNormalizer.SurveySourceCode && !allowSurvey)
        {
            Reject(opinion, plan, "Fuente", opinion.FuenteOriginal, "FUENTE_NO_SOPORTADA", "La fuente EncuestaInterna no corresponde a este archivo.");
            return;
        }

        opinion.CodigoFuente = code;
    }

    private static bool IsSurveyClassificationConsistent(string? classification, int score)
    {
        var expected = score switch
        {
            1 or 2 => "Negativa",
            3 => "Neutra",
            4 or 5 => "Positiva",
            _ => null
        };

        return expected is not null
            && string.Equals(classification, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void Reject(StagedClient row, EtlPlan plan, string archive, string column, string? value, string code, string message)
    {
        row.Estado = EtlStatus.Rejected;
        row.Motivo = AppendReason(row.Motivo, message);
        plan.AddError(archive, row.RowNumber, row.CodigoExterno, column, value, code, message);
    }

    private static void Reject(StagedProduct row, EtlPlan plan, string archive, string column, string? value, string code, string message)
    {
        row.Estado = EtlStatus.Rejected;
        row.Motivo = AppendReason(row.Motivo, message);
        plan.AddError(archive, row.RowNumber, row.CodigoExterno, column, value, code, message);
    }

    private static void Reject(StagedSource row, EtlPlan plan, string archive, string column, string? value, string code, string message)
    {
        row.Estado = EtlStatus.Rejected;
        row.Motivo = AppendReason(row.Motivo, message);
        plan.AddError(archive, row.RowNumber ?? 0, row.CodigoExterno, column, value, code, message);
    }

    private static void Reject(StagedOpinion row, EtlPlan plan, string column, string? value, string code, string message)
    {
        row.Estado = EtlStatus.Rejected;
        row.Motivo = AppendReason(row.Motivo, message);
        plan.AddError(row.Archivo, row.NumeroFila, row.IdOpinionExterno, column, value, code, message);
    }

    private static string AppendReason(string? current, string message)
    {
        return string.IsNullOrWhiteSpace(current) ? message : $"{current}; {message}";
    }

    private static IEnumerable<StagedSource> CreateExplicitSources()
    {
        yield return CreateExplicitSource("PLATAFORMA:Instagram", "Red Social", "Instagram");
        yield return CreateExplicitSource("PLATAFORMA:Facebook", "Red Social", "Facebook");
        yield return CreateExplicitSource("PLATAFORMA:Twitter", "Red Social", "Twitter");
        yield return CreateExplicitSource(CodeNormalizer.WebSourceCode, "Web", "Web");
        yield return CreateExplicitSource(CodeNormalizer.SurveySourceCode, "EncuestaInterna", "EncuestaInterna");
    }

    private static StagedSource CreateExplicitSource(string code, string type, string name)
    {
        return new StagedSource
        {
            CodigoExterno = code,
            TipoFuente = type,
            NombreFuente = name,
            FechaCarga = null,
            ArchivoOrigen = SyntheticArchive,
        };
    }
}


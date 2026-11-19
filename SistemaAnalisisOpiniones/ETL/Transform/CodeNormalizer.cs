using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace SistemaAnalisisOpiniones.ETL.Transform;

public static partial class CodeNormalizer
{
    public const string WebSourceCode = "PLATAFORMA:Web";
    public const string SurveySourceCode = "PLATAFORMA:EncuestaInterna";

    private static readonly Dictionary<string, string> PlatformCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Instagram"] = "PLATAFORMA:Instagram",
        ["Facebook"] = "PLATAFORMA:Facebook",
        ["Twitter"] = "PLATAFORMA:Twitter",
        ["Web"] = WebSourceCode,
        ["EncuestaInterna"] = SurveySourceCode
    };

    private static readonly Dictionary<string, string> Classifications = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Positiva"] = "Positiva",
        ["Neutra"] = "Neutra",
        ["Negativa"] = "Negativa"
    };

    private static readonly HashSet<string> SourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Web",
        "Red Social",
        "CSV",
        "EncuestaInterna"
    };

    public static bool TryNormalizeClientCode(string? value, out string code)
    {
        return TryNormalizePrefixedCode(value, 'C', "CLI", out code);
    }

    public static bool TryNormalizeProductCode(string? value, out string code)
    {
        return TryNormalizePrefixedCode(value, 'P', "PROD", out code);
    }

    public static bool TryNormalizeSourceCode(string? value, out string code)
    {
        code = string.Empty;
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length < 2 || (trimmed[0] != 'F' && trimmed[0] != 'f'))
        {
            return false;
        }

        if (!int.TryParse(trimmed[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return false;
        }

        code = $"FUENTE:F{id:D3}";
        return true;
    }

    public static bool TryNormalizePlatformCode(string? value, out string code)
    {
        code = string.Empty;
        var trimmed = value?.Trim() ?? string.Empty;
        return PlatformCodes.TryGetValue(trimmed, out code!);
    }

    public static bool IsValidSourceType(string? value)
    {
        return value is not null && SourceTypes.Contains(value.Trim());
    }

    public static bool TryNormalizeClassification(string? value, out string classification)
    {
        classification = string.Empty;
        var trimmed = value?.Trim() ?? string.Empty;
        return Classifications.TryGetValue(trimmed, out classification!);
    }

    public static bool TryClassifyRating(int rating, out string classification)
    {
        classification = rating switch
        {
            1 or 2 => "Negativa",
            3 => "Neutra",
            4 or 5 => "Positiva",
            _ => string.Empty
        };

        return classification.Length > 0;
    }

    public static bool TryParseDate(string? value, out DateTime date)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return DateTime.TryParseExact(
            trimmed,
            ["yyyy-MM-dd", "yyyy-M-d"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    public static bool TryParseInt(string? value, out int number)
    {
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    public static string NormalizeEmail(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    public static string NormalizeNameKey(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return WhitespaceRegex().Replace(trimmed, " ").ToUpperInvariant();
    }

    public static string NormalizeComment(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return WhitespaceRegex().Replace(trimmed, " ");
    }

    public static bool IsValidEmail(string? value, out string normalized)
    {
        normalized = NormalizeEmail(value);
        if (normalized.Length == 0 || normalized.Length > 150)
        {
            return false;
        }

        return MailAddress.TryCreate(normalized, out var address)
            && string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizePrefixedCode(string? value, char prefix, string targetPrefix, out string code)
    {
        code = string.Empty;
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return false;
        }

        var digits = trimmed;
        if ((char.ToUpperInvariant(trimmed[0]) == prefix) && trimmed.Length > 1)
        {
            digits = trimmed[1..];
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return false;
        }

        code = $"{targetPrefix}:{id:D3}";
        return true;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

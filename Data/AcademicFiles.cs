using System.Security.Cryptography;

namespace SistemaAnalisisOpiniones.Data;

public static class AcademicFiles
{
    public const string Social = "social_comments.csv";
    public const string Surveys = "surveys_part1.csv";
    public const string HashHeader = "X-Source-Sha256";
    private static readonly Dictionary<string, string> Hashes = new(StringComparer.Ordinal)
    {
        ["clients.csv"] = "e2f7fd421add98f30b3e7b260cc8149314e78e0f97c8e24e4c3cbd4c3078dab0",
        ["products.csv"] = "4aa94f2c6937537507eb71429cd9e8e8fc62c6eecf05d87467e7f644d3500f1f",
        ["fuente_datos.csv"] = "e3da8346e6f57e97afbe89a2ddb8317337f069a5d2a7de442e61b950ab4a20eb",
        [Social] = "9e8362e3535e963b0ece6a4ecc0babbf71328e4b41e7ff01c774f351f24fba58",
        [Surveys] = "09529bdb362bed75b628e53cd3eda5772f3edbe331d6afb83e708ee4430d1d84",
        ["web_reviews.csv"] = "5cedc8aaafa18641ba0fca67910ece0ab076e7d740c310f1a91cd98020e47c9b"
    };

    public static IReadOnlyDictionary<string, string> ExpectedHashes => Hashes;

    public static async Task VerifyAsync(string path, string expectedName, CancellationToken token)
    {
        var actual = await ComputeHashAsync(path, token);
        if (!actual.Equals(Hashes[expectedName], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"La huella de {expectedName} no coincide con el original académico.");
    }

    public static async Task<string> ComputeHashAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    public static async Task VerifyDirectoryAsync(string directory, CancellationToken token)
    {
        foreach (var name in Hashes.Keys)
            await VerifyAsync(Path.Combine(directory, name), name, token);
    }
}

public sealed record AcademicProvenance(string Directory, string TransactionDatabase)
{
    public IReadOnlyDictionary<string, string> FileSha256 => AcademicFiles.ExpectedHashes;
}

using System.Security.Cryptography;
using System.Text;

namespace RetailStorePOS.Data.Modules.Products;

/// <summary>
/// Generates a unique, permanent "Product DNA" identifier using SHA-256 hashing.
/// The DNA is derived from the creation timestamp and normalized product name,
/// producing a collision-resistant 12-character hex code formatted as XXXX-XXXX-XXXX.
/// </summary>
public static class ProductDnaGenerator
{
    /// <summary>
    /// Generates a new Product DNA from the current UTC time and the given product name.
    /// </summary>
    /// <param name="productName">The product name at the time of creation.</param>
    /// <returns>A formatted DNA string like "A7F2-9B31-C4E8".</returns>
    public static string Generate(string productName)
    {
        return Generate(productName, DateTime.UtcNow);
    }

    /// <summary>
    /// Generates a Product DNA from a specific timestamp and product name.
    /// Used for backfilling existing products with their original creation date.
    /// </summary>
    /// <param name="productName">The product name.</param>
    /// <param name="createdAtUtc">The creation timestamp to use as seed.</param>
    /// <returns>A formatted DNA string like "A7F2-9B31-C4E8".</returns>
    public static string Generate(string productName, DateTime createdAtUtc)
    {
        var timestamp = createdAtUtc.ToString("yyyyMMddHHmmssfff");
        var normalized = (productName ?? string.Empty).Trim().ToLowerInvariant();
        var seed = $"{timestamp}_{normalized}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var hex = Convert.ToHexString(hashBytes).ToUpperInvariant();

        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}";
    }
}

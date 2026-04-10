using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App.Services;

public sealed class ProductSearchService
{
    private readonly ProductRepository _repository;
    private readonly object _sync = new();

    private Dictionary<string, Product> _barcodeIndex = new(StringComparer.Ordinal);
    private List<IndexedProduct> _sortedNameIndex = new();

    public ProductSearchService(ProductRepository repository)
    {
        _repository = repository;
    }

    public void BuildIndex()
    {
        RefreshIndex();
    }

    public void RefreshIndex()
    {
        var products = _repository.GetAllUnbounded();
        var barcodeIndex = new Dictionary<string, Product>(StringComparer.Ordinal);
        var sortedByName = new List<IndexedProduct>(products.Count);

        foreach (var product in products)
        {
            var normalizedName = Normalize(product.Name);
            if (string.IsNullOrEmpty(normalizedName))
            {
                continue;
            }

            sortedByName.Add(new IndexedProduct(product, normalizedName));

            var normalizedBarcode = Normalize(product.Barcode);
            if (!string.IsNullOrEmpty(normalizedBarcode) && !barcodeIndex.ContainsKey(normalizedBarcode))
            {
                barcodeIndex[normalizedBarcode] = product;
            }
        }

        sortedByName.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.NormalizedName, right.NormalizedName));

        lock (_sync)
        {
            _barcodeIndex = barcodeIndex;
            _sortedNameIndex = sortedByName;
        }
    }

    public List<Product> Search(string? query, int limit = 5)
    {
        var normalizedQuery = Normalize(query);

        lock (_sync)
        {
            if (limit <= 0 || _sortedNameIndex.Count == 0)
            {
                return new List<Product>();
            }

            if (string.IsNullOrEmpty(normalizedQuery))
            {
                return _sortedNameIndex.Take(limit).Select(item => item.Product).ToList();
            }

            if (_barcodeIndex.TryGetValue(normalizedQuery, out var barcodeHit))
            {
                return new List<Product> { barcodeHit };
            }

            var prefixResults = PrefixSearch(normalizedQuery, limit);
            if (prefixResults.Count > 0)
            {
                return prefixResults;
            }

            return ContainsSearch(normalizedQuery, limit);
        }
    }

    private List<Product> PrefixSearch(string normalizedQuery, int limit)
    {
        var results = new List<Product>(limit);
        var start = FindLowerBound(normalizedQuery);

        for (var index = start; index < _sortedNameIndex.Count && results.Count < limit; index++)
        {
            var candidate = _sortedNameIndex[index];
            if (!candidate.NormalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                break;
            }

            results.Add(candidate.Product);
        }

        return results;
    }

    private List<Product> ContainsSearch(string normalizedQuery, int limit)
    {
        var results = new List<Product>(limit);
        foreach (var candidate in _sortedNameIndex)
        {
            if (!candidate.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(candidate.Product);
            if (results.Count == limit)
            {
                break;
            }
        }

        return results;
    }

    private int FindLowerBound(string normalizedQuery)
    {
        var low = 0;
        var high = _sortedNameIndex.Count;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            var compare = StringComparer.Ordinal.Compare(_sortedNameIndex[mid].NormalizedName, normalizedQuery);
            if (compare < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private sealed record IndexedProduct(Product Product, string NormalizedName);
}

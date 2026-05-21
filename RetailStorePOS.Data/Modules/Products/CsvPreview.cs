using System.Collections.Generic;

namespace RetailStorePOS.Data.Modules.Products;

/// <summary>
/// Lightweight preview of a CSV file used by the column-mapping dialog: the
/// raw header row plus a handful of sample data rows so the user can verify
/// each column's contents at a glance.
/// </summary>
/// <param name="Headers">Column names in the order they appear in the file.</param>
/// <param name="Rows">Up to N sample rows, each keyed by header name.</param>
public sealed record CsvPreview(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

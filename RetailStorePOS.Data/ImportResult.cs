namespace RetailStorePOS.Data.Models;

public class ImportResult
{
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;
}

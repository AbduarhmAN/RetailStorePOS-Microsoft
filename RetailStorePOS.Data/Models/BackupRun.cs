namespace RetailStorePOS.Data.Models;

public sealed class BackupRun
{
    public long Id { get; set; }
    public DateTime BackupTime { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

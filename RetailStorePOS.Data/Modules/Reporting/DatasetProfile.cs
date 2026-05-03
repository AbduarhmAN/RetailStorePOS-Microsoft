namespace RetailStorePOS.Data.Modules.Reporting;

public class DatasetProfile
{
    public string ProfileKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkingDatabasePath { get; set; } = string.Empty;
    public string SeedSource { get; set; } = string.Empty;
    public string ImportSampleDirectory { get; set; } = string.Empty;
    public string ExpectedScale { get; set; } = string.Empty;
}

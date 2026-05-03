namespace RetailStorePOS.Data.Modules.Reporting;

public class ImportExportSample
{
    public string SampleKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMalformed { get; set; }
    public string Content { get; set; } = string.Empty;

    // Optional/Extended fields
    public string WorkflowKey { get; set; } = string.Empty;
    public SampleType SampleType { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public List<string> ExpectedFieldCoverage { get; set; } = new();
    public bool IncludesImageReferences { get; set; }
    public ExpectedSampleOutcome ExpectedOutcome { get; set; }
}

public enum SampleType
{
    ValidRoundTrip,
    InvalidInput
}

public enum ExpectedSampleOutcome
{
    ImportSucceeds,
    ImportRejected,
    ExportSucceeds
}

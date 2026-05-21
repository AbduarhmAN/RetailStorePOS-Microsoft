namespace RetailStorePOS.Data.Models;

public sealed record TelemetryRuntimeState
{
    public string? ActiveRunId { get; init; }
    public DateTime? ActiveRunStartedAt { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public string? LastActivitySource { get; init; }
}

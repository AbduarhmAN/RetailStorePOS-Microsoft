namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// Contract every persisted advanced-analytics snapshot must satisfy so the
/// shared <see cref="SignedSnapshotStore{T}"/> can read/write/validate it
/// generically.
/// </summary>
public interface ISignedSnapshot
{
    /// <summary>Schema version the producer wrote with.</summary>
    int SchemaVersion { get; }

    /// <summary>Identifier of the period the snapshot represents (e.g. "Last7Days").</summary>
    string PeriodKey { get; }

    /// <summary>UTC timestamp when the snapshot was captured.</summary>
    System.DateTime CapturedAtUtc { get; }
}

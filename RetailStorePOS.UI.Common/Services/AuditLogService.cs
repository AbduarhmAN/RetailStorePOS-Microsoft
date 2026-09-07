using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Writes audit-log records to the database. Every other security control
/// (login failures, permission changes, register sessions, license events)
/// depends on these writes succeeding — so when the DB write fails we make
/// noise three different ways instead of silently dropping the record:
/// <list type="bullet">
///   <item>Persist a fallback JSON record under
///         <c>%LOCALAPPDATA%\RetailStorePOS\audit_outbox\</c> so an operator
///         can replay it later.</item>
///   <item>Raise <see cref="WriteFailed"/> so any subscribed UI can flash a
///         banner.</item>
///   <item>Bubble the failure into telemetry via the optional
///         <c>exceptionReporter</c> hook (LoginRuntime wires this to
///         <c>TelemetryService.LogErrorAsync</c>).</item>
/// </list>
///
/// We still do NOT throw out of <see cref="Log"/> because every call site in
/// the app expects audit logging to be a side effect, not a control-flow gate.
/// Throwing here would risk taking down the very actions we are supposed to be
/// recording (e.g. a checkout that mid-write loses its audit row).
/// </summary>
public sealed class AuditLogService
{
    private const string OutboxFolderName = "audit_outbox";
    private const int MaxOutboxFiles = 500;

    private readonly Action<AuditLog> _writer;
    private readonly Action<Exception, string>? _exceptionReporter;

    /// <summary>
    /// Raised when an audit-log write to the database fails. The first argument
    /// is the timestamp of the attempted record, the second is the action key,
    /// the third is the underlying exception. Subscribers must handle the event
    /// without throwing; the service swallows any callback exception.
    /// </summary>
    public event Action<DateTime, string, Exception>? WriteFailed;

    /// <summary>
    /// Most recent failure (timestamp + exception). Cleared on the next
    /// successful write. UI can poll this to drive a "audit log impaired"
    /// indicator.
    /// </summary>
    public AuditWriteFailure? LastFailure { get; private set; }

    public AuditLogService(AuditLogRepository repository)
        : this(repository, exceptionReporter: null)
    {
    }

    public AuditLogService(
        AuditLogRepository repository,
        Action<Exception, string>? exceptionReporter)
        : this(
            writer: (repository ?? throw new ArgumentNullException(nameof(repository))).Create,
            exceptionReporter: exceptionReporter)
    {
    }

    /// <summary>
    /// Test-friendly seam: lets unit tests inject a writer delegate that
    /// throws to exercise the failure path without needing a virtual method
    /// or a custom repository class.
    /// </summary>
    internal AuditLogService(
        Action<AuditLog> writer,
        Action<Exception, string>? exceptionReporter)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _exceptionReporter = exceptionReporter;
    }

    public void Log(string action, string? details = null, long? userId = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            _writer(log);
            // Successful write — clear any stale failure marker.
            LastFailure = null;
        }
        catch (Exception ex)
        {
            HandleWriteFailure(log, ex);
        }
    }

    private void HandleWriteFailure(AuditLog log, Exception ex)
    {
        LastFailure = new AuditWriteFailure(log.CreatedAt, log.Action, ex.GetType().Name, ex.Message);

        // Best-effort fallback persistence so we do not lose the record.
        try
        {
            PersistOutboxRecord(log, ex);
        }
        catch (Exception persistEx)
        {
            // Outbox write failure is logged through the exception reporter
            // but we do not loop back into the audit DB — that's the path
            // that just failed.
            ReportException(persistEx, "AuditLogService.OutboxPersist");
        }

        ReportException(ex, $"AuditLogService.Write[{log.Action}]");

        // Notify subscribers (defensive — never let a faulty handler crash
        // the original caller).
        var handler = WriteFailed;
        if (handler is not null)
        {
            try
            {
                handler(log.CreatedAt, log.Action, ex);
            }
            catch (Exception handlerEx)
            {
                ReportException(handlerEx, "AuditLogService.WriteFailedHandler");
            }
        }
    }

    private void ReportException(Exception ex, string operation)
    {
        if (_exceptionReporter is null)
        {
            return;
        }

        try
        {
            _exceptionReporter(ex, operation);
        }
        catch
        {
            // Last line of defense — never throw out of Log().
        }
    }

    private static void PersistOutboxRecord(AuditLog log, Exception ex)
    {
        var folder = AppDataPaths.Combine(OutboxFolderName);
        Directory.CreateDirectory(folder);

        // Keep the outbox bounded so a long-running DB outage does not fill
        // the disk. We never delete records that the operator has not yet
        // seen — once the count exceeds the cap we drop the OLDEST file,
        // which is the one most likely to have been resolved by hand already.
        TrimOutbox(folder, MaxOutboxFiles);

        var fileName = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:yyyyMMddHHmmssfff}_{1}.json",
            log.CreatedAt,
            Guid.NewGuid().ToString("N"));

        var path = Path.Combine(folder, fileName);

        var record = new AuditOutboxRecord
        {
            CreatedAt = log.CreatedAt,
            Action = log.Action,
            Details = log.Details,
            UserId = log.UserId,
            Error = new AuditOutboxError
            {
                Type = ex.GetType().FullName,
                Message = ex.Message,
            },
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(record, AuditLogJsonContext.Default.AuditOutboxRecord));
    }

    private static void TrimOutbox(string folder, int maxFiles)
    {
        try
        {
            var files = new DirectoryInfo(folder).GetFiles("*.json");
            if (files.Length < maxFiles)
            {
                return;
            }

            Array.Sort(files, (a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));
            for (var i = 0; i <= files.Length - maxFiles; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (IOException) { /* best-effort */ }
                catch (UnauthorizedAccessException) { /* best-effort */ }
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}

/// <summary>Snapshot of the most recent audit-log write failure.</summary>
public sealed record AuditWriteFailure(
    DateTime AttemptedAtUtc,
    string Action,
    string ExceptionType,
    string ExceptionMessage);

internal sealed class AuditOutboxRecord
{
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public long? UserId { get; set; }
    public AuditOutboxError Error { get; set; } = new();
}

internal sealed class AuditOutboxError
{
    public string? Type { get; set; }
    public string Message { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AuditOutboxRecord), TypeInfoPropertyName = nameof(AuditOutboxRecord))]
internal sealed partial class AuditLogJsonContext : JsonSerializerContext
{
}

using System.IO;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Services;

namespace RetailStorePOS.App;

public enum ProductImportJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed record ProductImportJobSnapshot(
    Guid Id,
    string FileName,
    ProductImportJobStatus Status,
    int TotalRows,
    int ProcessedRows,
    int CreatedRows,
    int UpdatedRows,
    int SkippedRows,
    double ProgressPercent,
    string StatusText,
    string? DetailMessage,
    DateTime StartedAt,
    DateTime? FinishedAt)
{
    public int ImportedRows => CreatedRows + UpdatedRows;

    public bool IsActive => Status is ProductImportJobStatus.Queued or ProductImportJobStatus.Running;
}

public sealed class ProductImportCoordinator
{
    private const int CompletedVisibilityMs = 5000;

    private readonly object _gate = new();
    private readonly ProductImportService _importService;
    private ProductImportJobSnapshot? _currentJob;

    public ProductImportCoordinator(ProductImportService importService)
    {
        _importService = importService;
    }

    public event EventHandler<ProductImportJobSnapshot?>? JobChanged;

    public ProductImportJobSnapshot? CurrentJob
    {
        get
        {
            lock (_gate)
            {
                return _currentJob;
            }
        }
    }

    public bool TryStartImport(string filePath, out string? errorMessage)
    {
        ProductImportJobSnapshot snapshot;

        lock (_gate)
        {
            if (_currentJob?.IsActive == true)
            {
                errorMessage = "A CSV import is already running.";
                return false;
            }

            snapshot = new ProductImportJobSnapshot(
                Guid.NewGuid(),
                Path.GetFileName(filePath),
                ProductImportJobStatus.Queued,
                0,
                0,
                0,
                0,
                0,
                0,
                "Preparing import...",
                null,
                DateTime.Now,
                null);

            _currentJob = snapshot;
        }

        errorMessage = null;
        NotifyJobChanged(snapshot);
        _ = RunImportAsync(snapshot.Id, filePath);
        return true;
    }

    private async Task RunImportAsync(Guid jobId, string filePath)
    {
        try
        {
            var progress = new Progress<ProductImportProgress>(value => ApplyProgress(jobId, value));
            var result = await Task.Run(() => _importService.ImportFromCsv(filePath, progress, CancellationToken.None));

            AppServices.RaiseProductsUpdated();

            UpdateJob(jobId, current => current with
            {
                Status = ProductImportJobStatus.Completed,
                TotalRows = Math.Max(current.TotalRows, current.ProcessedRows),
                ProcessedRows = Math.Max(current.ProcessedRows, current.TotalRows),
                ProgressPercent = 100,
                StatusText = result.HasErrors ? "Import completed with warnings." : "Import completed.",
                DetailMessage = result.Errors.FirstOrDefault(),
                FinishedAt = DateTime.Now
            });

            _ = ClearJobLaterAsync(jobId);
        }
        catch (Exception ex)
        {
            AppServices.ReportException(ex, "ProductImportCoordinator.RunImportAsync");
            UpdateJob(jobId, current => current with
            {
                Status = ProductImportJobStatus.Failed,
                StatusText = "Import failed.",
                DetailMessage = ex.Message,
                FinishedAt = DateTime.Now
            });

            _ = ClearJobLaterAsync(jobId);
        }
    }

    private void ApplyProgress(Guid jobId, ProductImportProgress progress)
    {
        UpdateJob(jobId, current => current with
        {
            Status = ProductImportJobStatus.Running,
            TotalRows = progress.TotalRows,
            ProcessedRows = progress.ProcessedRows,
            CreatedRows = progress.CreatedCount,
            UpdatedRows = progress.UpdatedCount,
            SkippedRows = progress.SkippedCount,
            ProgressPercent = progress.ProgressPercent,
            StatusText = progress.TotalRows == 0
                ? "Preparing import..."
                : progress.ProcessedRows >= progress.TotalRows
                    ? "Finalizing import..."
                    : "Importing products..."
        });
    }

    private void UpdateJob(Guid jobId, Func<ProductImportJobSnapshot, ProductImportJobSnapshot> update)
    {
        ProductImportJobSnapshot? updatedSnapshot;

        lock (_gate)
        {
            if (_currentJob == null || _currentJob.Id != jobId)
            {
                return;
            }

            updatedSnapshot = update(_currentJob);
            _currentJob = updatedSnapshot;
        }

        NotifyJobChanged(updatedSnapshot);
    }

    private async Task ClearJobLaterAsync(Guid jobId)
    {
        await Task.Delay(CompletedVisibilityMs);

        ProductImportJobSnapshot? clearedSnapshot;

        lock (_gate)
        {
            if (_currentJob == null || _currentJob.Id != jobId || _currentJob.IsActive)
            {
                return;
            }

            _currentJob = null;
            clearedSnapshot = null;
        }

        NotifyJobChanged(clearedSnapshot);
    }

    private void NotifyJobChanged(ProductImportJobSnapshot? snapshot)
    {
        JobChanged?.Invoke(this, snapshot);
    }
}

using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App.Services;

public sealed class AuditLogService
{
    private readonly AuditLogRepository _repository;

    public AuditLogService(AuditLogRepository repository)
    {
        _repository = repository;
    }

    public void Log(string action, string? details = null, long? userId = null)
    {
        // If userId is not provided, we could try to get it from context, 
        // but here we'll assume the caller provides it or we log as system/anonymous.

        // In a real app, we might fire-and-forget this to not block UI, 
        // but for now we'll keep it synchronous for simplicity and reliability.
        try
        {
            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };
            _repository.Create(log);
        }
        catch
        {
            // Fail silently on audit logging errors to avoid crashing main flows?
            // Or log to file? For now, we'll swallow or rethrow depending on strictness.
            // Given the requirements, we should probably not crash the app if audit fails, 
            // but in high security apps, we WOULD crash. 
            // We'll swallow for now to be safe for MVP stability.
        }
    }
}

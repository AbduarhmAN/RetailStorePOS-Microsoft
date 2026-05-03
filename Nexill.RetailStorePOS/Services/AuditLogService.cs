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
        }
    }
}

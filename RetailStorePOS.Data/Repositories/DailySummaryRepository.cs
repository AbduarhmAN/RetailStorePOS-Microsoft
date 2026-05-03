using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class DailySummaryRepository
{
    private readonly SqliteConnectionFactory _factory;

    public DailySummaryRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    // Scaffolding methods to be implemented
    public void Create(DailySummary summary)
    {
        // To be implemented
    }

    public DailySummary? GetByDate(DateTime date)
    {
        // To be implemented
        return null;
    }
}

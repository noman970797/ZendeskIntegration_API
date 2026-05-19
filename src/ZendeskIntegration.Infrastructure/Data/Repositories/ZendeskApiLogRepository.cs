using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Data.Repositories;

public class ZendeskApiLogRepository : IZendeskApiLogRepository
{
    private readonly ZendeskDbContext _db;

    public ZendeskApiLogRepository(ZendeskDbContext db)
    {
        _db = db;
    }

    public async Task<ZendeskApiLog> AddAsync(ZendeskApiLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        _db.ZendeskApiLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }
}

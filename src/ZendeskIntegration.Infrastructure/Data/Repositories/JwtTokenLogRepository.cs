using Microsoft.EntityFrameworkCore;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Data.Repositories;

public class JwtTokenLogRepository : IJwtTokenLogRepository
{
    private readonly ZendeskDbContext _db;

    public JwtTokenLogRepository(ZendeskDbContext db)
    {
        _db = db;
    }

    public async Task<JwtTokenLog> AddAsync(JwtTokenLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        _db.JwtTokenLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<List<JwtTokenLog>> GetByUserIdAsync(string externalUserId)
    {
        return await _db.JwtTokenLogs
            .AsNoTracking()
            .Where(l => l.ExternalUserId == externalUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Data.Repositories;

public class AttachmentLogRepository : IAttachmentLogRepository
{
    private readonly ZendeskDbContext _db;

    public AttachmentLogRepository(ZendeskDbContext db) => _db = db;

    public async Task<AttachmentLog> AddAsync(AttachmentLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        _db.AttachmentLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<AttachmentLog?> GetByTokenAsync(string token) =>
        await _db.AttachmentLogs.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UploadToken == token);

    public async Task UpdateAsync(AttachmentLog log)
    {
        _db.AttachmentLogs.Update(log);
        await _db.SaveChangesAsync();
    }
}

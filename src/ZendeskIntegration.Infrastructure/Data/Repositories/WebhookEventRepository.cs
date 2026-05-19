using Microsoft.EntityFrameworkCore;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Data.Repositories;

public class WebhookEventRepository : IWebhookEventRepository
{
    private readonly ZendeskDbContext _db;

    public WebhookEventRepository(ZendeskDbContext db) => _db = db;

    public async Task<WebhookEvent> AddAsync(WebhookEvent evt)
    {
        evt.ReceivedAt = DateTime.UtcNow;
        _db.WebhookEvents.Add(evt);
        await _db.SaveChangesAsync();
        return evt;
    }

    public async Task<List<WebhookEvent>> GetByZendeskTicketIdAsync(long zendeskTicketId) =>
        await _db.WebhookEvents.AsNoTracking()
            .Where(e => e.ZendeskTicketId == zendeskTicketId)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();

    public async Task UpdateAsync(WebhookEvent evt)
    {
        _db.WebhookEvents.Update(evt);
        await _db.SaveChangesAsync();
    }
}

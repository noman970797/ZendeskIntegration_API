using Microsoft.EntityFrameworkCore;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly ZendeskDbContext _db;

    public TicketRepository(ZendeskDbContext db)
    {
        _db = db;
    }

    public async Task<SupportTicket> AddAsync(SupportTicket ticket)
    {
        ticket.CreatedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task<SupportTicket?> GetByIdAsync(int id)
    {
        return await _db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(List<SupportTicket> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        var query = _db.SupportTickets.AsNoTracking().OrderByDescending(t => t.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task UpdateAsync(SupportTicket ticket)
    {
        ticket.UpdatedAt = DateTime.UtcNow;
        _db.SupportTickets.Update(ticket);
        await _db.SaveChangesAsync();
    }
}

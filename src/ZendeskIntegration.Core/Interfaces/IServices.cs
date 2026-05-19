using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Models;

namespace ZendeskIntegration.Core.Interfaces;

/// <summary>
/// Generates Zendesk-compatible JWT tokens for authenticated messaging.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a signed JWT token for the given user, persisting an audit log entry to SQL Server.
    /// </summary>
    Task<GenerateTokenResponse> GenerateTokenAsync(GenerateTokenRequest request, string? ipAddress = null, string? userAgent = null);
}

/// <summary>
/// Creates and manages support tickets via the Zendesk REST API.
/// </summary>
public interface IZendeskTicketService
{
    /// <summary>
    /// Creates a ticket in Zendesk and persists the result to SQL Server.
    /// </summary>
    Task<CreateTicketResponse> CreateTicketAsync(CreateTicketRequest request, string? createdBy = null);

    /// <summary>
    /// Retrieves a paged list of tickets from the local SQL Server database.
    /// </summary>
    Task<TicketListResponse> GetTicketsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Retrieves a single ticket by its local database ID.
    /// </summary>
    Task<SupportTicket?> GetTicketByIdAsync(int id);
}

/// <summary>
/// Repository contract for JWT audit log persistence.
/// </summary>
public interface IJwtTokenLogRepository
{
    Task<JwtTokenLog> AddAsync(JwtTokenLog log);
    Task<List<JwtTokenLog>> GetByUserIdAsync(string externalUserId);
}

/// <summary>
/// Repository contract for ticket persistence.
/// </summary>
public interface ITicketRepository
{
    Task<SupportTicket> AddAsync(SupportTicket ticket);
    Task<SupportTicket?> GetByIdAsync(int id);
    Task<(List<SupportTicket> Items, int Total)> GetPagedAsync(int page, int pageSize);
    Task UpdateAsync(SupportTicket ticket);
}

/// <summary>
/// Repository contract for Zendesk API call logging.
/// </summary>
public interface IZendeskApiLogRepository
{
    Task<ZendeskApiLog> AddAsync(ZendeskApiLog log);
}

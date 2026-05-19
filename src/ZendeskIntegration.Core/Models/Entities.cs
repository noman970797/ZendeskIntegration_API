namespace ZendeskIntegration.Core.Models;

/// <summary>
/// Persisted record of every JWT token generated — stored in SQL Server for audit purposes.
/// </summary>
public class JwtTokenLog
{
    public int Id { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of token for tracing without storing raw JWT
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persisted ticket record mirroring what was sent to Zendesk.
/// </summary>
public class SupportTicket
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty; // JSON array stored as string
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string Priority { get; set; } = "normal";
    public string Type { get; set; } = "problem";
    public string Status { get; set; } = "pending";

    // Zendesk response fields
    public long? ZendeskTicketId { get; set; }
    public string? ZendeskTicketUrl { get; set; }
    public bool SyncedToZendesk { get; set; } = false;
    public DateTime? ZendeskCreatedAt { get; set; }
    public string? ZendeskRawResponse { get; set; } // Full API response for debugging

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Tracks API call attempts to Zendesk for observability and retry logic.
/// </summary>
public class ZendeskApiLog
{
    public int Id { get; set; }
    public string Operation { get; set; } = string.Empty; // "CreateTicket", "UpdateTicket", etc.
    public string HttpMethod { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int? HttpStatusCode { get; set; }
    public bool Success { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public int? RelatedTicketId { get; set; } // FK to SupportTicket.Id
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

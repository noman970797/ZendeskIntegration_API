namespace ZendeskIntegration.Core.DTOs;

public class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string Priority { get; set; } = "normal";
    public string Type { get; set; } = "problem";

    /// <summary>
    /// Optional upload tokens from prior calls to POST /api/attachments/upload.
    /// Each token attaches a file to this ticket in Zendesk.
    /// </summary>
    public List<string> AttachmentTokens { get; set; } = new();
}

public class CreateTicketResponse
{
    public bool Success { get; set; }
    public long? ZendeskTicketId { get; set; }
    public string? TicketUrl { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TicketListResponse
{
    public List<TicketSummary> Tickets { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TicketSummary
{
    public int Id { get; set; }
    public long? ZendeskTicketId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool SyncedToZendesk { get; set; }
}

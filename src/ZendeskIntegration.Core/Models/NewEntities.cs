namespace ZendeskIntegration.Core.Models;

/// <summary>
/// Tracks every file uploaded to Zendesk and the token returned.
/// The token is used when creating a ticket to attach the file.
/// </summary>
public class AttachmentLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadToken { get; set; } = string.Empty;

    /// <summary>FK to SupportTicket.Id — populated once the ticket is created</summary>
    public int? RelatedTicketId { get; set; }

    public bool TokenUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedBy { get; set; }
}

/// <summary>
/// Persists every inbound Zendesk webhook event for audit and replay.
/// </summary>
public class WebhookEvent
{
    public int Id { get; set; }

    /// <summary>Zendesk ticket ID included in the event payload</summary>
    public long? ZendeskTicketId { get; set; }

    /// <summary>Logical event type derived from payload (e.g. "ticket_updated", "agent_reply")</summary>
    public string EventType { get; set; } = string.Empty;

    public string? TicketStatus { get; set; }
    public string? TicketPriority { get; set; }
    public string? AssigneeEmail { get; set; }
    public string? LatestCommentAuthor { get; set; }

    /// <summary>Full raw JSON body received from Zendesk</summary>
    public string RawPayload { get; set; } = string.Empty;

    public bool ProcessedSuccessfully { get; set; } = false;
    public string? ProcessingError { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? SourceIpAddress { get; set; }

    /// <summary>FK to local SupportTicket if matched</summary>
    public int? LocalTicketId { get; set; }
}

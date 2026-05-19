using System.Text.Json.Serialization;

namespace ZendeskIntegration.Core.DTOs;

/// <summary>
/// Root payload Zendesk POSTs to your webhook endpoint.
/// The exact shape depends on the trigger configured in Zendesk Admin.
/// </summary>
public class ZendeskWebhookPayload
{
    [JsonPropertyName("ticket_id")]
    public long? TicketId { get; set; }

    [JsonPropertyName("ticket_title")]
    public string? TicketTitle { get; set; }

    [JsonPropertyName("ticket_status")]
    public string? TicketStatus { get; set; }

    [JsonPropertyName("ticket_priority")]
    public string? TicketPriority { get; set; }

    [JsonPropertyName("ticket_type")]
    public string? TicketType { get; set; }

    [JsonPropertyName("ticket_url")]
    public string? TicketUrl { get; set; }

    [JsonPropertyName("latest_comment_author_name")]
    public string? LatestCommentAuthorName { get; set; }

    [JsonPropertyName("latest_comment_author_email")]
    public string? LatestCommentAuthorEmail { get; set; }

    [JsonPropertyName("latest_comment")]
    public string? LatestComment { get; set; }

    [JsonPropertyName("is_public_comment")]
    public bool? IsPublicComment { get; set; }

    [JsonPropertyName("assignee_name")]
    public string? AssigneeName { get; set; }

    [JsonPropertyName("assignee_email")]
    public string? AssigneeEmail { get; set; }

    [JsonPropertyName("requester_name")]
    public string? RequesterName { get; set; }

    [JsonPropertyName("requester_email")]
    public string? RequesterEmail { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Raw JSON for debugging / future-proofing</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}

public class WebhookProcessResult
{
    public bool Processed { get; set; }
    public string? EventType { get; set; }
    public long? TicketId { get; set; }
    public string? Message { get; set; }
}

using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Models;

namespace ZendeskIntegration.Core.Interfaces;

/// <summary>
/// Uploads files to Zendesk and returns the upload token needed for ticket attachment.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Uploads a file to Zendesk Upload API and persists the token to SQL Server.
    /// Returns the upload token to include in CreateTicketRequest.AttachmentTokens.
    /// </summary>
    Task<UploadAttachmentResponse> UploadAsync(UploadAttachmentRequest request, string? uploadedBy = null);
}

/// <summary>
/// Processes inbound webhook events from Zendesk.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Validates the webhook signature, parses the payload, updates local records,
    /// and persists the event to SQL Server.
    /// </summary>
    Task<WebhookProcessResult> ProcessAsync(
        ZendeskWebhookPayload payload,
        string rawBody,
        string? signature,
        string? sourceIp);

    /// <summary>
    /// Validates the HMAC-SHA256 signature sent in the X-Zendesk-Webhook-Signature header.
    /// Returns true if valid, false if tampered or missing.
    /// </summary>
    bool ValidateSignature(string rawBody, string signature);
}

/// <summary>Repository for attachment log persistence.</summary>
public interface IAttachmentLogRepository
{
    Task<AttachmentLog> AddAsync(AttachmentLog log);
    Task<AttachmentLog?> GetByTokenAsync(string token);
    Task UpdateAsync(AttachmentLog log);
}

/// <summary>Repository for webhook event persistence.</summary>
public interface IWebhookEventRepository
{
    Task<WebhookEvent> AddAsync(WebhookEvent evt);
    Task<List<WebhookEvent>> GetByZendeskTicketIdAsync(long zendeskTicketId);
    Task UpdateAsync(WebhookEvent evt);
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;
using ZendeskIntegration.Infrastructure.Data;

namespace ZendeskIntegration.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly ZendeskOptions _options;
    private readonly IWebhookEventRepository _eventRepo;
    private readonly ZendeskDbContext _db;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IOptions<ZendeskOptions> options,
        IWebhookEventRepository eventRepo,
        ZendeskDbContext db,
        ILogger<WebhookService> logger)
    {
        _options = options.Value;
        _eventRepo = eventRepo;
        _db = db;
        _logger = logger;
    }

    public bool ValidateSignature(string rawBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret))
        {
            // If no signing secret is configured, skip validation (not recommended for production)
            _logger.LogWarning("WebhookSigningSecret is not configured — signature validation is DISABLED.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Webhook received with no signature header.");
            return false;
        }

        try
        {
            // Zendesk signs using HMAC-SHA256; the header value is Base64-encoded
            var keyBytes = Encoding.UTF8.GetBytes(_options.WebhookSigningSecret);
            var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
            var hash = HMACSHA256.HashData(keyBytes, bodyBytes);
            var computed = Convert.ToBase64String(hash);

            // Constant-time comparison to prevent timing attacks
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(signature));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating webhook signature");
            return false;
        }
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        ZendeskWebhookPayload payload,
        string rawBody,
        string? signature,
        string? sourceIp)
    {
        // Derive a logical event type from the payload
        var eventType = DeriveEventType(payload);

        _logger.LogInformation(
            "Processing webhook event {EventType} for Zendesk ticket {TicketId}",
            eventType, payload.TicketId);

        // Persist raw event immediately — even if downstream processing fails,
        // we have a record for audit / replay
        var evt = await _eventRepo.AddAsync(new WebhookEvent
        {
            ZendeskTicketId = payload.TicketId,
            EventType = eventType,
            TicketStatus = payload.TicketStatus,
            TicketPriority = payload.TicketPriority,
            AssigneeEmail = payload.AssigneeEmail,
            LatestCommentAuthor = payload.LatestCommentAuthorName,
            RawPayload = rawBody,
            SourceIpAddress = sourceIp,
            ProcessedSuccessfully = false,
        });

        try
        {
            // ── Update local SupportTicket if we have a matching record ──────
            if (payload.TicketId.HasValue)
            {
                var localTicket = await _db.SupportTickets
                    .FirstOrDefaultAsync(t => t.ZendeskTicketId == payload.TicketId.Value);

                if (localTicket is not null)
                {
                    if (!string.IsNullOrWhiteSpace(payload.TicketStatus))
                        localTicket.Status = payload.TicketStatus;

                    localTicket.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    evt.LocalTicketId = localTicket.Id;
                    _logger.LogInformation(
                        "Updated local ticket {LocalId} status to {Status}",
                        localTicket.Id, localTicket.Status);
                }
                else
                {
                    _logger.LogInformation(
                        "No local ticket found for ZendeskTicketId {ZendeskId} — event stored only.",
                        payload.TicketId.Value);
                }
            }

            // ── Mark event as processed ───────────────────────────────────
            evt.ProcessedSuccessfully = true;
            await _eventRepo.UpdateAsync(evt);

            return new WebhookProcessResult
            {
                Processed = true,
                EventType = eventType,
                TicketId = payload.TicketId,
                Message = $"Event '{eventType}' processed successfully.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event {EventId}", evt.Id);

            evt.ProcessedSuccessfully = false;
            evt.ProcessingError = ex.Message;
            await _eventRepo.UpdateAsync(evt);

            return new WebhookProcessResult
            {
                Processed = false,
                EventType = eventType,
                TicketId = payload.TicketId,
                Message = $"Processing failed: {ex.Message}",
            };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string DeriveEventType(ZendeskWebhookPayload payload)
    {
        // Infer event type from status or comment presence
        if (payload.TicketStatus == "solved" || payload.TicketStatus == "closed")
            return "ticket_closed";

        if (!string.IsNullOrWhiteSpace(payload.LatestComment))
            return "agent_reply";

        if (!string.IsNullOrWhiteSpace(payload.TicketStatus))
            return "status_changed";

        return payload.EventType ?? "ticket_updated";
    }
}

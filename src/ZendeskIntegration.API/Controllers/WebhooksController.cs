using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;

namespace ZendeskIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IWebhookService webhookService, ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>
    /// Receives real-time ticket event notifications from Zendesk.
    /// Register this URL in Zendesk Admin > Apps and Integrations > Webhooks.
    ///
    /// Zendesk sends a POST with JSON body and X-Zendesk-Webhook-Signature header.
    /// This endpoint validates the signature before processing.
    /// </summary>
    /// <remarks>
    /// Zendesk requires a 200 response within 5 seconds.
    /// Processing is fire-and-store: the event is saved to SQL Server immediately,
    /// then processed. A failure in processing does NOT return a non-200 status
    /// (which would cause Zendesk to retry and duplicate events).
    ///
    /// Setup in Zendesk:
    /// Admin Center > Apps and Integrations > Webhooks > Create webhook
    /// Endpoint URL: https://your-domain.com/api/webhooks/zendesk
    /// Request method: POST
    /// Request format: JSON
    /// Authentication: HMAC-SHA256 (copy the signing secret to Zendesk:WebhookSigningSecret)
    /// </remarks>
    [HttpPost("zendesk")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveZendeskEvent()
    {
        // Read raw body for signature validation — MUST be done before model binding
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // ── Signature validation ──────────────────────────────────────────
        var signature = Request.Headers["X-Zendesk-Webhook-Signature"].FirstOrDefault() ?? string.Empty;

        if (!_webhookService.ValidateSignature(rawBody, signature))
        {
            _logger.LogWarning("Webhook signature validation failed. Possible tampered request.");
            return Unauthorized(new { error = "Invalid webhook signature." });
        }

        // ── Deserialize payload ───────────────────────────────────────────
        ZendeskWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ZendeskWebhookPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload. Body={Body}", rawBody[..Math.Min(500, rawBody.Length)]);
            // Still return 200 to prevent Zendesk retrying a malformed payload
            return Ok(new { processed = false, error = "Payload parse error" });
        }

        if (payload is null)
            return Ok(new { processed = false, error = "Empty payload" });

        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        // ── Process (non-blocking from Zendesk's perspective) ─────────────
        var result = await _webhookService.ProcessAsync(payload, rawBody, signature, sourceIp);

        _logger.LogInformation(
            "Webhook processed. EventType={Type} TicketId={Id} Success={Ok}",
            result.EventType, result.TicketId, result.Processed);

        // Always return 200 — Zendesk interprets non-200 as failure and retries
        return Ok(new
        {
            processed = result.Processed,
            eventType = result.EventType,
            ticketId = result.TicketId,
            message = result.Message,
        });
    }
}

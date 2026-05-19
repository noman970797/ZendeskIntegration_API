namespace ZendeskIntegration.Core.Models;

public class ZendeskOptions
{
    public const string SectionName = "Zendesk";
    public string Subdomain { get; set; } = string.Empty;
    public string AgentEmail { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtKeyId { get; set; } = string.Empty;
    public string JwtAlgorithm { get; set; } = "HS256";
    public int JwtExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Signing secret for verifying inbound webhook calls from Zendesk.
    /// Found in Admin Center > Apps and Integrations > Webhooks > [webhook] > Signing secret.
    /// </summary>
    public string WebhookSigningSecret { get; set; } = string.Empty;

    /// <summary>Max attachment size in bytes. Zendesk hard limit = 50 MB. Default = 20 MB.</summary>
    public long MaxAttachmentSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Comma-separated allowed MIME types for uploads.</summary>
    public string AllowedAttachmentMimeTypes { get; set; } =
        "image/jpeg,image/png,image/gif,application/pdf,text/plain," +
        "application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document," +
        "application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet," +
        "application/zip,video/mp4";

    public string BaseUrl => $"https://{Subdomain}.zendesk.com";
    public string TicketsApiUrl => $"{BaseUrl}/api/v2/tickets.json";
    public string UploadsApiUrl => $"{BaseUrl}/api/v2/uploads.json";

    public IEnumerable<string> GetAllowedMimeTypes() =>
        AllowedAttachmentMimeTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

namespace ZendeskIntegration.Core.DTOs;

public class UploadAttachmentRequest
{
    /// <summary>Raw file bytes</summary>
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();

    /// <summary>Original filename including extension</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. image/png, application/pdf</summary>
    public string ContentType { get; set; } = "application/octet-stream";
}

public class UploadAttachmentResponse
{
    public bool Success { get; set; }

    /// <summary>Token returned by Zendesk — pass this into CreateTicketRequest.AttachmentTokens</summary>
    public string? UploadToken { get; set; }

    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? ErrorMessage { get; set; }
}

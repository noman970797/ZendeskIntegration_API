namespace ZendeskIntegration.Core.DTOs;

public class GenerateTokenRequest
{
    public string ExternalUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class GenerateTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Algorithm { get; set; } = string.Empty;
}

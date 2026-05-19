using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;

namespace ZendeskIntegration.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly HttpClient _http;
    private readonly ZendeskOptions _options;
    private readonly IAttachmentLogRepository _repo;
    private readonly ILogger<AttachmentService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AttachmentService(
        HttpClient http,
        IOptions<ZendeskOptions> options,
        IAttachmentLogRepository repo,
        ILogger<AttachmentService> logger)
    {
        _http = http;
        _options = options.Value;
        _repo = repo;
        _logger = logger;

        ConfigureHttpClient();
    }

    public async Task<UploadAttachmentResponse> UploadAsync(
        UploadAttachmentRequest request,
        string? uploadedBy = null)
    {
        // ── Validate ──────────────────────────────────────────────────────
        if (request.FileBytes.Length == 0)
            return Fail("File is empty.");

        if (request.FileBytes.Length > _options.MaxAttachmentSizeBytes)
            return Fail($"File exceeds maximum size of {_options.MaxAttachmentSizeBytes / (1024 * 1024)} MB.");

        var allowed = _options.GetAllowedMimeTypes().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count > 0 && !allowed.Contains(request.ContentType))
            return Fail($"Content type '{request.ContentType}' is not permitted.");

        // ── Build upload URL with filename query parameter ─────────────────
        // Zendesk Upload API: POST /api/v2/uploads.json?filename={name}
        var encodedName = Uri.EscapeDataString(request.FileName);
        var uploadUrl = $"{_options.UploadsApiUrl}?filename={encodedName}";

        using var content = new ByteArrayContent(request.FileBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        _logger.LogInformation("Uploading {FileName} ({Size} bytes) to Zendesk", request.FileName, request.FileBytes.Length);

        HttpResponseMessage? response = null;
        string responseBody = string.Empty;

        try
        {
            response = await _http.PostAsync(uploadUrl, content);
            responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Zendesk upload failed. Status={Status} Body={Body}", (int)response.StatusCode, responseBody);
                return Fail($"Zendesk Upload API returned {(int)response.StatusCode}: {responseBody}");
            }

            // ── Parse Zendesk upload response ──────────────────────────────
            // Response: { "upload": { "token": "abc...", "attachment": { ... } } }
            var uploadResp = JsonSerializer.Deserialize<ZendeskUploadResponse>(responseBody, JsonOpts);
            var token = uploadResp?.Upload?.Token;

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogError("Zendesk returned success but no upload token. Body={Body}", responseBody);
                return Fail("Zendesk returned no upload token.");
            }

            // ── Persist to SQL Server ──────────────────────────────────────
            await _repo.AddAsync(new AttachmentLog
            {
                FileName = request.FileName,
                ContentType = request.ContentType,
                FileSizeBytes = request.FileBytes.Length,
                UploadToken = token,
                UploadedBy = uploadedBy,
            });

            _logger.LogInformation("Upload successful. Token={Token}", token);

            return new UploadAttachmentResponse
            {
                Success = true,
                UploadToken = token,
                FileName = request.FileName,
                FileSizeBytes = request.FileBytes.Length,
                ContentType = request.ContentType,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error uploading to Zendesk");
            return Fail($"Network error: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ConfigureHttpClient()
    {
        if (string.IsNullOrWhiteSpace(_options.AgentEmail) || string.IsNullOrWhiteSpace(_options.ApiToken))
            throw new InvalidOperationException("Zendesk:AgentEmail and Zendesk:ApiToken must be configured.");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.AgentEmail}/token:{_options.ApiToken}"));

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    private static UploadAttachmentResponse Fail(string error) =>
        new() { Success = false, ErrorMessage = error };

    // ── Zendesk API response models ───────────────────────────────────────

    private class ZendeskUploadResponse
    {
        public ZendeskUploadData? Upload { get; set; }
    }

    private class ZendeskUploadData
    {
        public string? Token { get; set; }
        public ZendeskAttachmentData? Attachment { get; set; }
    }

    private class ZendeskAttachmentData
    {
        public long? Id { get; set; }
        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }
        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }
        public long? Size { get; set; }
    }
}

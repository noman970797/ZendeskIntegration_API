using System.Diagnostics;
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

public class ZendeskTicketService : IZendeskTicketService
{
    private readonly HttpClient _http;
    private readonly ZendeskOptions _options;
    private readonly ITicketRepository _ticketRepo;
    private readonly IZendeskApiLogRepository _apiLogRepo;
    private readonly IAttachmentLogRepository _attachmentRepo;
    private readonly ILogger<ZendeskTicketService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ZendeskTicketService(
        HttpClient http,
        IOptions<ZendeskOptions> options,
        ITicketRepository ticketRepo,
        IZendeskApiLogRepository apiLogRepo,
        IAttachmentLogRepository attachmentRepo,
        ILogger<ZendeskTicketService> logger)
    {
        _http = http;
        _options = options.Value;
        _ticketRepo = ticketRepo;
        _apiLogRepo = apiLogRepo;
        _attachmentRepo = attachmentRepo;
        _logger = logger;
        ConfigureHttpClient();
    }

    public async Task<CreateTicketResponse> CreateTicketAsync(
        CreateTicketRequest request,
        string? createdBy = null)
    {
        var localTicket = new SupportTicket
        {
            Subject = request.Subject,
            Description = request.Description,
            Tags = JsonSerializer.Serialize(request.Tags),
            RequesterName = request.RequesterName,
            RequesterEmail = request.RequesterEmail,
            Priority = request.Priority,
            Type = request.Type,
            Status = "pending",
            SyncedToZendesk = false,
            CreatedBy = createdBy,
        };
        localTicket = await _ticketRepo.AddAsync(localTicket);

        var payload = BuildZendeskPayload(request);
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        string responseBody = string.Empty;
        int? statusCode = null;

        try
        {
            _logger.LogInformation(
                "Creating Zendesk ticket LocalId={Id} with {Count} attachment(s)",
                localTicket.Id, request.AttachmentTokens.Count);

            var httpResponse = await _http.PostAsync(_options.TicketsApiUrl, content);
            responseBody = await httpResponse.Content.ReadAsStringAsync();
            statusCode = (int)httpResponse.StatusCode;
            sw.Stop();

            if (httpResponse.IsSuccessStatusCode)
            {
                var zResp = JsonSerializer.Deserialize<ZendeskCreateTicketResponse>(responseBody, JsonOpts);

                localTicket.ZendeskTicketId = zResp?.Ticket?.Id;
                localTicket.ZendeskTicketUrl = zResp?.Ticket?.Url;
                localTicket.ZendeskCreatedAt = zResp?.Ticket?.CreatedAt;
                localTicket.ZendeskRawResponse = responseBody;
                localTicket.SyncedToZendesk = true;
                localTicket.Status = zResp?.Ticket?.Status ?? "open";
                await _ticketRepo.UpdateAsync(localTicket);

                foreach (var token in request.AttachmentTokens)
                {
                    var att = await _attachmentRepo.GetByTokenAsync(token);
                    if (att is not null)
                    {
                        att.TokenUsed = true;
                        att.RelatedTicketId = localTicket.Id;
                        await _attachmentRepo.UpdateAsync(att);
                    }
                }

                _logger.LogInformation("Ticket created. ZendeskId={ZId}", zResp?.Ticket?.Id);
                await LogApiCallAsync("CreateTicket", "POST", _options.TicketsApiUrl,
                    statusCode, true, json, responseBody, null, sw.ElapsedMilliseconds, localTicket.Id);

                return new CreateTicketResponse
                {
                    Success = true,
                    ZendeskTicketId = zResp?.Ticket?.Id,
                    TicketUrl = zResp?.Ticket?.Url,
                    Status = zResp?.Ticket?.Status,
                    CreatedAt = zResp?.Ticket?.CreatedAt,
                };
            }
            else
            {
                var err = $"Zendesk API returned {statusCode}: {responseBody}";
                _logger.LogError("Ticket creation failed: {Error}", err);
                await LogApiCallAsync("CreateTicket", "POST", _options.TicketsApiUrl,
                    statusCode, false, json, responseBody, err, sw.ElapsedMilliseconds, localTicket.Id);
                return new CreateTicketResponse { Success = false, ErrorMessage = err };
            }
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Network error creating ticket");
            await LogApiCallAsync("CreateTicket", "POST", _options.TicketsApiUrl,
                null, false, json, null, ex.Message, sw.ElapsedMilliseconds, localTicket.Id);
            return new CreateTicketResponse { Success = false, ErrorMessage = $"Network error: {ex.Message}" };
        }
    }

    public async Task<TicketListResponse> GetTicketsAsync(int page = 1, int pageSize = 20)
    {
        var (items, total) = await _ticketRepo.GetPagedAsync(page, pageSize);
        return new TicketListResponse
        {
            Tickets = items.Select(t => new TicketSummary
            {
                Id = t.Id,
                ZendeskTicketId = t.ZendeskTicketId,
                Subject = t.Subject,
                Status = t.Status,
                Priority = t.Priority,
                CreatedAt = t.CreatedAt,
                SyncedToZendesk = t.SyncedToZendesk,
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<SupportTicket?> GetTicketByIdAsync(int id) =>
        await _ticketRepo.GetByIdAsync(id);

    private void ConfigureHttpClient()
    {
        if (string.IsNullOrWhiteSpace(_options.AgentEmail) || string.IsNullOrWhiteSpace(_options.ApiToken))
            throw new InvalidOperationException("Zendesk:AgentEmail and Zendesk:ApiToken must be configured.");

        var creds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.AgentEmail}/token:{_options.ApiToken}"));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private static object BuildZendeskPayload(CreateTicketRequest req)
    {
        object comment = req.AttachmentTokens.Count > 0
            ? new { body = req.Description, uploads = req.AttachmentTokens }
            : (object)new { body = req.Description };

        var ticketBody = new Dictionary<string, object?>
        {
            ["subject"] = req.Subject,
            ["comment"] = comment,
            ["priority"] = req.Priority,
            ["type"] = req.Type,
            ["tags"] = req.Tags,
        };

        if (!string.IsNullOrWhiteSpace(req.RequesterEmail))
            ticketBody["requester"] = new { name = req.RequesterName ?? req.RequesterEmail, email = req.RequesterEmail };

        return new { ticket = ticketBody };
    }

    private async Task LogApiCallAsync(
        string operation, string method, string endpoint,
        int? statusCode, bool success,
        string? requestBody, string? responseBody, string? errorMessage,
        long durationMs, int? relatedTicketId)
    {
        try
        {
            await _apiLogRepo.AddAsync(new ZendeskApiLog
            {
                Operation = operation, HttpMethod = method, Endpoint = endpoint,
                HttpStatusCode = statusCode, Success = success,
                RequestBody = requestBody, ResponseBody = responseBody,
                ErrorMessage = errorMessage, DurationMs = durationMs,
                RelatedTicketId = relatedTicketId,
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to persist ZendeskApiLog"); }
    }

    private class ZendeskCreateTicketResponse { public ZendeskTicketData? Ticket { get; set; } }

    private class ZendeskTicketData
    {
        public long Id { get; set; }
        public string? Url { get; set; }
        public string? Status { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}

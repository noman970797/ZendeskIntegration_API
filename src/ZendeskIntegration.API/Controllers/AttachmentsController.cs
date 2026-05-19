using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;

namespace ZendeskIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(IAttachmentService attachmentService, ILogger<AttachmentsController> logger)
    {
        _attachmentService = attachmentService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a file to Zendesk and returns an upload token.
    /// Pass the token in CreateTicketRequest.AttachmentTokens to attach the file to a ticket.
    /// </summary>
    /// <remarks>
    /// Workflow:
    /// 1. POST /api/attachments/upload with multipart/form-data
    /// 2. Copy the returned uploadToken
    /// 3. POST /api/tickets with attachmentTokens: ["the-token"]
    ///
    /// Maximum file size: 20 MB (configurable via Zendesk:MaxAttachmentSizeBytes)
    /// </remarks>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge)]
    [RequestSizeLimit(52_428_800)] // 50 MB hard ceiling matches Zendesk limit
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided or file is empty." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var uploadedBy = HttpContext.User.Identity?.Name ?? "api";

        var result = await _attachmentService.UploadAsync(new UploadAttachmentRequest
        {
            FileBytes = ms.ToArray(),
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
        }, uploadedBy);

        if (result.Success)
        {
            _logger.LogInformation("Attachment uploaded. Token={Token}", result.UploadToken);
            return Ok(result);
        }

        if (result.ErrorMessage?.Contains("exceeds maximum size") == true)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge,
                new ProblemDetails { Title = result.ErrorMessage });

        if (result.ErrorMessage?.Contains("not permitted") == true)
            return BadRequest(new ProblemDetails { Title = result.ErrorMessage });

        return StatusCode(StatusCodes.Status502BadGateway,
            new ProblemDetails { Title = "Upload failed", Detail = result.ErrorMessage });
    }

}

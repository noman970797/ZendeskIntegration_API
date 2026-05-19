using Microsoft.AspNetCore.Mvc;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;

namespace ZendeskIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IJwtService jwtService, ILogger<AuthController> logger)
    {
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a Zendesk Messaging JWT token for the given user.
    /// The token is signed using the configured algorithm (HS256 or RS256).
    /// An audit entry is persisted to SQL Server.
    /// </summary>
    /// <remarks>
    /// Use the returned token value in the Zendesk Web Widget `authenticateEndUser` call.
    ///
    /// Sample request:
    ///
    ///     POST /api/auth/token
    ///     {
    ///         "externalUserId": "user-001",
    ///         "name": "Jane Smith",
    ///         "email": "jane@example.com"
    ///     }
    ///
    /// </remarks>
    [HttpPost("token")]
    [ProducesResponseType(typeof(GenerateTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
            return BadRequest(new ProblemDetails { Title = "ExternalUserId is required." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new ProblemDetails { Title = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ProblemDetails { Title = "Name is required." });

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            var result = await _jwtService.GenerateTokenAsync(request, ipAddress, userAgent);

            _logger.LogInformation("Token generated for {UserId}", request.ExternalUserId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration error during token generation");
            return Problem(
                title: "Configuration error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

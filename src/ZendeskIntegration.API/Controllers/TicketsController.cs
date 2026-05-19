using Microsoft.AspNetCore.Mvc;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;

namespace ZendeskIntegration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly IZendeskTicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(IZendeskTicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new support ticket in Zendesk and persists the result to SQL Server.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tickets
    ///     {
    ///         "subject": "Login issue on mobile",
    ///         "description": "User cannot log in using the iOS app since version 4.2.",
    ///         "tags": ["mobile", "ios", "login"],
    ///         "requesterName": "Jane Smith",
    ///         "requesterEmail": "jane@example.com",
    ///         "priority": "high",
    ///         "type": "problem"
    ///     }
    ///
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreateTicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new ProblemDetails { Title = "Subject is required." });

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new ProblemDetails { Title = "Description is required." });

        var createdBy = HttpContext.User.Identity?.Name ?? "api";

        var result = await _ticketService.CreateTicketAsync(request, createdBy);

        if (result.Success)
        {
            _logger.LogInformation("Ticket created. ZendeskId={ZendeskId}", result.ZendeskTicketId);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        _logger.LogError("Ticket creation failed: {Error}", result.ErrorMessage);
        return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
        {
            Title = "Zendesk API error",
            Detail = result.ErrorMessage,
        });
    }

    /// <summary>
    /// Returns a paged list of tickets from the local SQL Server database.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var result = await _ticketService.GetTicketsAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single ticket by its local database ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(int id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(id);
        if (ticket is null)
            return NotFound(new ProblemDetails { Title = $"Ticket {id} not found." });

        return Ok(ticket);
    }
}

using GymSaaS.Application.Features.Support.Commands;
using GymSaaS.Application.Features.Support.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupportController(IMediator mediator)
    {
        _mediator = mediator;
    }

public record SupportMessageRequest(string Message);

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }
    [HttpPost("tickets/{id:int}/messages")]
    public async Task<IActionResult> AddMessage(int id, [FromBody] SupportMessageRequest request)
    {
        var result = await _mediator.Send(new AddSupportTicketMessageCommand(id, request.Message));
        return Ok(new { success = result });
    }

    [HttpPost("tickets/{id:int}/close")]
    public async Task<IActionResult> CloseTicket(int id)
    {
        var result = await _mediator.Send(new CloseSupportTicketCommand(id));
        return Ok(new { success = result });
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets()
    {
        var result = await _mediator.Send(new GetSupportTicketsQuery());
        return Ok(new { success = true, data = result });
    }
}

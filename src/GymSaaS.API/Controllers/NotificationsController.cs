using GymSaaS.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetNotificationsQuery(pageNumber, pageSize));
        return Ok(new { success = true, data = result });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var result = await _mediator.Send(new MarkNotificationReadCommand(id));
        return Ok(new { success = result });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand());
        return Ok(new { success = result });
    }
}

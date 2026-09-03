using GymSaaS.Application.Features.Players.Commands;
using GymSaaS.Application.Features.Players.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlayersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlayersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }
    [HttpGet]
    public async Task<IActionResult> GetPlayers()
    {
        var result = await _mediator.Send(new GetPlayersQuery());
        return Ok(new { success = true, data = result });
    }
}

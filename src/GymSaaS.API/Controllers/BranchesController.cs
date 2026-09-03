using GymSaaS.Application.Features.Branches.Commands;
using GymSaaS.Application.Features.Branches.Queries;
using GymSaaS.Application.Features.Facilities.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public BranchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }
    [HttpGet]
    public async Task<IActionResult> GetBranches()
    {
        var result = await _mediator.Send(new GetBranchesQuery());
        return Ok(new { success = true, data = result });
    }
}

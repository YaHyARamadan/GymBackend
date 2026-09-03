using GymSaaS.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet("supervisor-overview")]
    public async Task<IActionResult> GetSupervisorOverview()
    {
        var result = await _mediator.Send(new GetSupervisorDashboardQuery());
        return Ok(new { success = true, data = result });
    }
}

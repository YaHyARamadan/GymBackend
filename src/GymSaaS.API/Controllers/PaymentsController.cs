using GymSaaS.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("revenue-overview")]
    public async Task<IActionResult> GetRevenueOverview()
    {
        var result = await _mediator.Send(new GetSupervisorRevenueOverviewQuery());
        return Ok(new { success = true, data = result });
    }
}

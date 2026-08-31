using GymSaaS.Application.Features.Owners.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OwnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OwnersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("onboarding")]
    public async Task<IActionResult> CompleteOnboarding([FromBody] CompleteOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = result, message = "تم إكمال التجهيز الأولي بنجاح." });
    }
}

using GymSaaS.Application.Features.Owners.Commands;
using GymSaaS.Application.Features.Owners.Queries;
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

public record UpdateOwnerRequest(
    string Name, string Email, string? Phone,
    bool ContractSigned, bool OnboardingCompleted);
public record ResetOwnerPasswordRequest(string NewPassword);

    [HttpPost("onboarding")]
    public async Task<IActionResult> CompleteOnboarding([FromBody] CompleteOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = result, message = "تم إكمال التجهيز الأولي بنجاح." });
    }
    [HttpGet]
    public async Task<IActionResult> GetOwners([FromQuery] int? facilityId = null)
    {
        var result = await _mediator.Send(new GetOwnersQuery(facilityId));
        return Ok(new { success = true, data = result });
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateOwner(int id, [FromBody] UpdateOwnerRequest request)
    {
        var result = await _mediator.Send(new UpdateOwnerCommand(
            id, request.Name, request.Email, request.Phone,
            request.ContractSigned, request.OnboardingCompleted));
        return Ok(new { success = result });
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetOwnerPassword(int id, [FromBody] ResetOwnerPasswordRequest request)
    {
        var result = await _mediator.Send(new ResetOwnerPasswordCommand(id, request.NewPassword));
        return Ok(new { success = result });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _mediator.Send(new GetOwnerProfileQuery());
        return Ok(new { success = true, data = result });
    }
}

using GymSaaS.Application.Features.Facilities.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacilitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FacilitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(CreateFacility), new { id = result.Id }, new { success = true, data = result });
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> LockFacility(int id)
    {
        var result = await _mediator.Send(new LockFacilityCommand(id));
        return Ok(new { success = result, message = "تم قفل المنشأة بنجاح." });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> UnlockFacility(int id, [FromBody] UnlockRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var result = await _mediator.Send(new UnlockFacilityCommand(id, request.AmountPaid, key));
        return Ok(new { success = result, message = "تم فك قفل المنشأة وتسجيل الدفعة بنجاح." });
    }
}

public record UnlockRequest(decimal AmountPaid);

using GymSaaS.Application.Features.AddOns.Commands;
using GymSaaS.Application.Features.AddOns.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddOnsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AddOnsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAddOn([FromBody] CreateAddOnFeatureCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("activate")]
    public async Task<IActionResult> ActivateAddOn([FromBody] ActivateFacilityAddOnCommand command, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var cmd = command with { IdempotencyKey = key };
        var result = await _mediator.Send(cmd);
        return Ok(new { success = result, message = "تم تفعيل الميزة الإضافية وتسجيل الدفعة بنجاح." });
    }
    [HttpGet]
    public async Task<IActionResult> GetAddOns()
    {
        var result = await _mediator.Send(new GetAddOnsQuery());
        return Ok(new { success = true, data = result });
    }

    [HttpGet("facility/{facilityId:int}")]
    public async Task<IActionResult> GetFacilityAddOns(int facilityId)
    {
        var result = await _mediator.Send(new GetFacilityAddOnsQuery(facilityId));
        return Ok(new { success = true, data = result });
    }
}

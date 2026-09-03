using GymSaaS.Application.Features.Facilities.Commands;
using GymSaaS.Application.Features.Facilities.Queries;
using GymSaaS.Application.Features.Branches.Queries;
using GymSaaS.Application.Features.Players.Commands;
using GymSaaS.Application.Features.Players.Queries;
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFacility(int id, [FromBody] UpdateFacilityRequest request)
    {
        var result = await _mediator.Send(new UpdateFacilityCommand(
            id, request.Name, request.Description, request.LicenseType, request.LicenseEndDate));
        return Ok(new { success = result });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFacility(int id)
    {
        var result = await _mediator.Send(new DeleteFacilityCommand(id));
        return Ok(new { success = result });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> UnlockFacility(int id, [FromBody] UnlockRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        var result = await _mediator.Send(new UnlockFacilityCommand(id, request.AmountPaid, key));
        return Ok(new { success = result, message = "تم فك قفل المنشأة وتسجيل الدفعة بنجاح." });
    }

    [HttpGet]
    public async Task<IActionResult> GetFacilities()
    {
        var result = await _mediator.Send(new GetFacilitiesQuery());
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFacility(int id)
    {
        var result = await _mediator.Send(new GetFacilityQuery(id));
        return Ok(new { success = true, data = result });
    }
    [HttpGet("{id:int}/branches")]
    public async Task<IActionResult> GetFacilityBranches(int id)
    {
        var result = await _mediator.Send(new GetFacilityBranchesQuery(id));
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:int}/players")]
    public async Task<IActionResult> GetPlayers(int id)
    {
        var result = await _mediator.Send(new GetFacilityPlayersManagementQuery(id));
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:int}/subscriptions")]
    public async Task<IActionResult> GetSubscriptions(int id)
    {
        var result = await _mediator.Send(new GetFacilitySubscriptionsQuery(id));
        return Ok(new { success = true, data = result });
    }

    [HttpPost("{id:int}/players")]
    public async Task<IActionResult> CreatePlayer(int id, [FromBody] CreateFacilityPlayerRequest request)
    {
        var result = await _mediator.Send(new CreateFacilityPlayerCommand(
            id, request.Name, request.Email, request.Phone, request.DateOfBirth, request.BranchId));
        return Ok(new { success = result });
    }

    [HttpPut("{id:int}/players/{playerId:int}")]
    public async Task<IActionResult> UpdatePlayer(
        int id, int playerId, [FromBody] UpdateFacilityPlayerRequest request)
    {
        var result = await _mediator.Send(new UpdateFacilityPlayerCommand(
            id, playerId, request.Name, request.Email, request.Phone,
            request.DateOfBirth, request.BranchId, request.IsActive));
        return Ok(new { success = result });
    }

    [HttpPost("{id:int}/players/{playerId:int}/subscription")]
    public async Task<IActionResult> AssignSubscription(
        int id, int playerId, [FromBody] AssignSubscriptionRequest request)
    {
        var result = await _mediator.Send(new AssignPlayerSubscriptionCommand(
            id, playerId, request.PlanName, request.Price,
            request.DurationInDays, request.StartDate));
        return Ok(new { success = result });
    }

    [HttpGet("{id:int}/subscription")]
    public async Task<IActionResult> GetPlatformSubscription(int id)
    {
        var result = await _mediator.Send(new GetPlatformSubscriptionQuery(id));
        return Ok(new { success = true, data = result });
    }
}

public record UnlockRequest(decimal AmountPaid);
public record UpdateFacilityRequest(
    string Name,
    string? Description,
    GymSaaS.Domain.Enums.LicenseType LicenseType,
    DateTime? LicenseEndDate);

public record CreateFacilityPlayerRequest(
    string Name, string Email, string? Phone, DateTime? DateOfBirth, int BranchId);
public record UpdateFacilityPlayerRequest(
    string Name, string Email, string? Phone, DateTime? DateOfBirth, int BranchId, bool IsActive);
public record AssignSubscriptionRequest(
    string PlanName, decimal Price, int DurationInDays, DateTime? StartDate);

using GymSaaS.Application.Features.Employees;
using GymSaaS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetEmployees([FromQuery] int? facilityId = null)
    {
        var result = await _mediator.Send(new GetEmployeesQuery(facilityId));
        return Ok(new { success = true, data = result });
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [HttpPatch("{role}/{id:int}")]
    public async Task<IActionResult> UpdateEmployee(string role, int id, [FromBody] UpdateEmployeeRequest request)
    {
        if (!Enum.TryParse<ActorType>(role, true, out var actorType))
            return BadRequest(new { success = false, message = "Invalid employee role." });

        var result = await _mediator.Send(new UpdateEmployeeCommand(
            actorType, id, request.Name, request.Email, request.Phone,
            request.BranchId, request.BranchIds, request.Specialization));
        return Ok(new { success = true, data = result });
    }

    [HttpPatch("{role}/{id:int}/status")]
    public async Task<IActionResult> SetStatus(string role, int id, [FromBody] EmployeeStatusRequest request)
    {
        if (!Enum.TryParse<ActorType>(role, true, out var actorType))
            return BadRequest(new { success = false, message = "Invalid employee role." });

        var result = await _mediator.Send(new SetEmployeeStatusCommand(actorType, id, request.IsActive));
        return Ok(new { success = result });
    }

    [HttpPost("{role}/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(string role, int id, [FromBody] ResetEmployeePasswordRequest request)
    {
        if (!Enum.TryParse<ActorType>(role, true, out var actorType))
            return BadRequest(new { success = false, message = "Invalid employee role." });

        var result = await _mediator.Send(new ResetEmployeePasswordCommand(actorType, id, request.NewPassword));
        return Ok(new { success = result });
    }
}

public record EmployeeStatusRequest(bool IsActive);
public record ResetEmployeePasswordRequest(string NewPassword);
public record UpdateEmployeeRequest(string Name, string Email, string? Phone, int? BranchId, IReadOnlyList<int>? BranchIds, string? Specialization);

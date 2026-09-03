using GymSaaS.Application.Features.Auth.Commands;
using GymSaaS.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login/supervisor")]
    public async Task<IActionResult> LoginSupervisor([FromBody] LoginSupervisorCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("verify-totp")]
    public async Task<IActionResult> VerifyTotp([FromBody] VerifyTotpCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("login/owner")]
    public async Task<IActionResult> LoginOwner([FromBody] LoginOwnerCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("login/staff")]
    public async Task<IActionResult> LoginStaff([FromBody] LoginStaffCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [Authorize]
    [HttpPost("impersonate")]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { success = true, data = result });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _mediator.Send(new LogoutCommand());
        Response.Cookies.Delete("gymsaas_token");
        return Ok(new { success = true });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentSession()
    {
        var result = await _mediator.Send(new GetCurrentSessionQuery());
        return Ok(new { success = true, data = result });
    }
}

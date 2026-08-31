using GymSaaS.Application.Features.Contracts.Commands;
using GymSaaS.Application.Features.Contracts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContractsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentContract()
    {
        var result = await _mediator.Send(new GetCurrentContractQuery());
        return Ok(new { success = true, data = result });
    }

    [HttpPost("sign")]
    public async Task<IActionResult> SignContract([FromBody] SignContractRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _mediator.Send(new SignContractCommand(request.ContractId, request.SignatureText, ip));
        return Ok(new { success = result, message = "تم توقيع العقد بنجاح." });
    }
}

public record SignContractRequest(int ContractId, string SignatureText);

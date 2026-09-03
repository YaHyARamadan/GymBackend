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

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int? facilityId = null,
        [FromQuery] GymSaaS.Domain.Enums.PaymentType? paymentType = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetPaymentRecordsQuery(
            from, to, facilityId, paymentType, pageNumber, pageSize));
        return Ok(new { success = true, data = result });
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int? facilityId = null)
    {
        var result = await _mediator.Send(new GetPaymentReportQuery(from, to, facilityId));
        return Ok(new { success = true, data = result });
    }

    [HttpGet("revenue-overview")]
    public async Task<IActionResult> GetRevenueOverview()
    {
        var result = await _mediator.Send(new GetSupervisorRevenueOverviewQuery());
        return Ok(new { success = true, data = result });
    }
}

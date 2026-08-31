using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Payments.Queries;

public record GetSupervisorRevenueOverviewQuery : IRequest<RevenueOverviewDto>;

public record RevenueOverviewDto(
    decimal TotalRevenue,
    decimal PrimarySubscriptionsRevenue,
    decimal AddOnFeaturesRevenue,
    List<PaymentRecordDto> RecentPayments
);

public record PaymentRecordDto(
    int Id,
    int FacilityId,
    string FacilityName,
    decimal Amount,
    PaymentType PaymentType,
    string? AddOnFeatureName,
    DateTime RecordedAt,
    string? Notes
);

public class GetSupervisorRevenueOverviewQueryHandler : IRequestHandler<GetSupervisorRevenueOverviewQuery, RevenueOverviewDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetSupervisorRevenueOverviewQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<RevenueOverviewDto> Handle(GetSupervisorRevenueOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("فقط السوبرفايزر يمكنه الاطلاع على تقرير الإيرادات.");

        // Aggregate on the database instead of pulling every PaymentRecord into memory —
        // the previous ToListAsync() with no Where/Take loaded the entire table just to sum
        // it and then take the first 50, which only gets slower as payment history grows.
        var paymentsQuery = _dbContext.Set<PaymentRecord>();

        decimal primaryRev = await paymentsQuery
            .Where(p => p.PaymentType == PaymentType.PlatformSubscription)
            .SumAsync(p => p.Amount, cancellationToken);
        decimal addOnRev = await paymentsQuery
            .Where(p => p.PaymentType == PaymentType.AddOnFeature)
            .SumAsync(p => p.Amount, cancellationToken);
        decimal totalRev = primaryRev + addOnRev;

        var recentDtos = await paymentsQuery
            .Include(p => p.Facility)
            .Include(p => p.AddOnFeature)
            .OrderByDescending(p => p.RecordedAt)
            .Take(50)
            .Select(p => new PaymentRecordDto(
                p.Id,
                p.FacilityId,
                p.Facility.Name,
                p.Amount,
                p.PaymentType,
                p.AddOnFeature != null ? p.AddOnFeature.Name : null,
                p.RecordedAt,
                p.Notes
            ))
            .ToListAsync(cancellationToken);

        return new RevenueOverviewDto(totalRev, primaryRev, addOnRev, recentDtos);
    }
}

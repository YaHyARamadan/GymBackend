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

        var payments = await _dbContext.Set<PaymentRecord>()
            .Include(p => p.Facility)
            .Include(p => p.AddOnFeature)
            .OrderByDescending(p => p.RecordedAt)
            .ToListAsync(cancellationToken);

        decimal primaryRev = payments.Where(p => p.PaymentType == PaymentType.PlatformSubscription).Sum(p => p.Amount);
        decimal addOnRev = payments.Where(p => p.PaymentType == PaymentType.AddOnFeature).Sum(p => p.Amount);
        decimal totalRev = primaryRev + addOnRev;

        var recentDtos = payments.Take(50).Select(p => new PaymentRecordDto(
            p.Id,
            p.FacilityId,
            p.Facility.Name,
            p.Amount,
            p.PaymentType,
            p.AddOnFeature?.Name,
            p.RecordedAt,
            p.Notes
        )).ToList();

        return new RevenueOverviewDto(totalRev, primaryRev, addOnRev, recentDtos);
    }
}

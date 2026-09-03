using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Queries;

public record GetPlatformSubscriptionQuery(int FacilityId) : IRequest<PlatformSubscriptionReadDto>;

public record PlatformSubscriptionReadDto(
    int Id,
    int FacilityId,
    FacilityStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    decimal AmountPaid,
    DateTime CreatedAt
);

public class GetPlatformSubscriptionQueryHandler : IRequestHandler<GetPlatformSubscriptionQuery, PlatformSubscriptionReadDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetPlatformSubscriptionQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<PlatformSubscriptionReadDto> Handle(
        GetPlatformSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can access platform subscriptions.");

        var facility = await _dbContext.Set<Facility>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (facility is null || facility.LicenseType == LicenseType.Sold)
            throw new NotFoundException("Facility", request.FacilityId);

        var subscription = await _dbContext.Set<PlatformSubscription>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.FacilityId == request.FacilityId)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new PlatformSubscriptionReadDto(
                s.Id,
                s.FacilityId,
                s.Status,
                s.StartDate,
                s.EndDate,
                s.AmountPaid,
                s.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            throw new NotFoundException("PlatformSubscription", request.FacilityId);

        return subscription;
    }
}

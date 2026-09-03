using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Players.Queries;

public record GetFacilityPlayersManagementQuery(int FacilityId) : IRequest<IReadOnlyList<FacilityPlayerDto>>;
public record GetFacilitySubscriptionsQuery(int FacilityId) : IRequest<IReadOnlyList<FacilitySubscriptionDto>>;

public record FacilityPlayerDto(
    int Id, string Name, string Email, string? Phone, DateTime? DateOfBirth,
    int BranchId, int? SubscriptionId, string? SubscriptionName,
    DateTime? SubscriptionStartDate, DateTime? SubscriptionEndDate, bool IsActive, DateTime CreatedAt);

public record FacilitySubscriptionDto(
    int Id, string PlanName, decimal Price, int DurationInDays,
    DateTime StartDate, DateTime? EndDate, int FacilityId, DateTime CreatedAt);

public class GetFacilityPlayersManagementQueryHandler :
    IRequestHandler<GetFacilityPlayersManagementQuery, IReadOnlyList<FacilityPlayerDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilityPlayersManagementQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<FacilityPlayerDto>> Handle(
        GetFacilityPlayersManagementQuery request, CancellationToken cancellationToken)
    {
        EnsureSupervisor();
        EnsureFacility(request.FacilityId);

        return await _dbContext.Set<Player>().IgnoreQueryFilters()
            .Where(p => p.FacilityId == request.FacilityId)
            .Include(p => p.Subscription)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new FacilityPlayerDto(
                p.Id, p.Name, p.Email, p.Phone, p.DateOfBirth, p.BranchId,
                p.SubscriptionId, p.Subscription != null ? p.Subscription.PlanName : null,
                p.SubscriptionStartDate, p.SubscriptionEndDate, p.IsActive, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private void EnsureSupervisor()
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can manage facility players.");
    }

    private void EnsureFacility(int facilityId)
    {
        if (!_dbContext.Set<Facility>().IgnoreQueryFilters().Any(f => f.Id == facilityId))
            throw new NotFoundException("Facility", facilityId);
    }
}

public class GetFacilitySubscriptionsQueryHandler :
    IRequestHandler<GetFacilitySubscriptionsQuery, IReadOnlyList<FacilitySubscriptionDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilitySubscriptionsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<FacilitySubscriptionDto>> Handle(
        GetFacilitySubscriptionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can list facility subscriptions.");

        if (!await _dbContext.Set<Facility>().IgnoreQueryFilters()
            .AnyAsync(f => f.Id == request.FacilityId, cancellationToken))
            throw new NotFoundException("Facility", request.FacilityId);

        return await _dbContext.Set<Subscription>().IgnoreQueryFilters()
            .Where(s => s.FacilityId == request.FacilityId)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new FacilitySubscriptionDto(
                s.Id, s.PlanName, s.Price, s.DurationInDays, s.StartDate,
                s.EndDate, s.FacilityId, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

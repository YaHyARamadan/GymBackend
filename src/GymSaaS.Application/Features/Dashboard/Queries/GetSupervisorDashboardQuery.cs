using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Dashboard.Queries;

public record GetSupervisorDashboardQuery : IRequest<SupervisorDashboardDto>;

public record SupervisorDashboardDto(
    int TotalFacilities,
    int ActiveFacilities,
    int FrozenFacilities,
    int ExpiredFacilities,
    int TotalBranches,
    int TotalPlayers,
    int TotalEmployees,
    int OpenSupportTickets,
    int UnreadNotifications,
    decimal TotalRevenue,
    int ActiveAddOns);

public class GetSupervisorDashboardQueryHandler : IRequestHandler<GetSupervisorDashboardQuery, SupervisorDashboardDto>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetSupervisorDashboardQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<SupervisorDashboardDto> Handle(
        GetSupervisorDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor || !int.TryParse(_currentUserService.UserId, out var supervisorId))
            throw new ForbiddenAccessException("Only the supervisor can access the platform dashboard.");

        var facilities = _dbContext.Set<Facility>().IgnoreQueryFilters();
        var totalEmployees =
            await _dbContext.Set<BranchManager>().IgnoreQueryFilters().CountAsync(cancellationToken) +
            await _dbContext.Set<Coach>().IgnoreQueryFilters().CountAsync(cancellationToken) +
            await _dbContext.Set<Receptionist>().IgnoreQueryFilters().CountAsync(cancellationToken);

        return new SupervisorDashboardDto(
            await facilities.CountAsync(cancellationToken),
            await facilities.CountAsync(f => f.Status == FacilityStatus.Active, cancellationToken),
            await facilities.CountAsync(f => f.Status == FacilityStatus.Frozen, cancellationToken),
            await facilities.CountAsync(f => f.Status == FacilityStatus.Expired, cancellationToken),
            await _dbContext.Set<Branch>().IgnoreQueryFilters().CountAsync(cancellationToken),
            await _dbContext.Set<Player>().IgnoreQueryFilters().CountAsync(cancellationToken),
            totalEmployees,
            await _dbContext.Set<SupportTicket>().IgnoreQueryFilters()
                .CountAsync(t => t.Status != "Closed", cancellationToken),
            await _dbContext.Set<Notification>().CountAsync(n =>
                n.RecipientId == supervisorId.ToString() &&
                n.RecipientActorType == ActorType.Supervisor && !n.IsRead, cancellationToken),
            await _dbContext.Set<PaymentRecord>().IgnoreQueryFilters()
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m,
            await _dbContext.Set<FacilityAddOnSubscription>().IgnoreQueryFilters()
                .CountAsync(s => s.Status == AddOnFeatureStatus.Active, cancellationToken));
    }
}

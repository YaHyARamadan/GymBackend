using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Facilities.Commands;

public record DeleteFacilityCommand(int FacilityId) : IRequest<bool>;

public class DeleteFacilityCommandHandler : IRequestHandler<DeleteFacilityCommand, bool>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DeleteFacilityCommandHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteFacilityCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can delete facilities.");

        var exists = await _dbContext.Set<Facility>().IgnoreQueryFilters()
            .AnyAsync(f => f.Id == request.FacilityId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Facility", request.FacilityId);

        var ticketIds = await _dbContext.Set<SupportTicket>().IgnoreQueryFilters()
            .Where(t => t.FacilityId == request.FacilityId).Select(t => t.Id)
            .ToListAsync(cancellationToken);
        if (ticketIds.Count > 0)
            await _dbContext.Set<SupportTicketMessage>()
                .Where(m => ticketIds.Contains(m.SupportTicketId))
                .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Set<SupportTicket>().IgnoreQueryFilters()
            .Where(t => t.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<ContractApproval>()
            .Where(a => a.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Player>().IgnoreQueryFilters()
            .Where(p => p.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Coach>().IgnoreQueryFilters()
            .Where(e => e.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Receptionist>().IgnoreQueryFilters()
            .Where(e => e.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<BranchManager>().IgnoreQueryFilters()
            .Where(e => e.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Owner>().IgnoreQueryFilters()
            .Where(e => e.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<PaymentRecord>().IgnoreQueryFilters()
            .Where(p => p.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<FacilityAddOnSubscription>().IgnoreQueryFilters()
            .Where(s => s.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<PlatformSubscription>().IgnoreQueryFilters()
            .Where(s => s.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Subscription>().IgnoreQueryFilters()
            .Where(s => s.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Branch>().IgnoreQueryFilters()
            .Where(b => b.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Notification>()
            .Where(n => n.FacilityId == request.FacilityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Set<Facility>().IgnoreQueryFilters()
            .Where(f => f.Id == request.FacilityId).ExecuteDeleteAsync(cancellationToken);

        return true;
    }
}

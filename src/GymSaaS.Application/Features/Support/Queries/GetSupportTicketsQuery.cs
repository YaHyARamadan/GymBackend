using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Support.Queries;

public record GetSupportTicketsQuery : IRequest<IReadOnlyList<SupportTicketReadDto>>;

public record SupportTicketReadDto(
    int Id,
    int FacilityId,
    int OwnerId,
    string Subject,
    string Status,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    IReadOnlyList<SupportTicketMessageReadDto> Messages
);

public record SupportTicketMessageReadDto(
    int Id,
    string SenderId,
    ActorType SenderActorType,
    string Message,
    DateTime SentAt
);

public class GetSupportTicketsQueryHandler : IRequestHandler<GetSupportTicketsQuery, IReadOnlyList<SupportTicketReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetSupportTicketsQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<SupportTicketReadDto>> Handle(
        GetSupportTicketsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<SupportTicket>()
            .AsNoTracking()
            .Include(t => t.Messages)
            .AsQueryable();

        if (_currentUserService.IsSupervisor)
        {
            // Supervisor can review tickets from every facility.
            query = query.IgnoreQueryFilters();
        }
        else
        {
            if (_currentUserService.ActorType != ActorType.Owner ||
                !_currentUserService.FacilityId.HasValue ||
                !int.TryParse(_currentUserService.UserId, out var ownerId))
            {
                throw new ForbiddenAccessException("Only an owner or supervisor can access support tickets.");
            }

            query = query.Where(t =>
                t.FacilityId == _currentUserService.FacilityId.Value &&
                t.OwnerId == ownerId);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketReadDto(
                t.Id,
                t.FacilityId,
                t.OwnerId,
                t.Subject,
                t.Status,
                t.CreatedAt,
                t.ClosedAt,
                t.Messages
                    .OrderBy(m => m.SentAt)
                    .Select(m => new SupportTicketMessageReadDto(
                        m.Id,
                        m.SenderId,
                        m.SenderActorType,
                        m.Message,
                        m.SentAt))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}

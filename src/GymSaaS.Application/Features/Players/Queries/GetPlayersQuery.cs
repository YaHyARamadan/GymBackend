using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Players.Queries;

public record GetPlayersQuery : IRequest<IReadOnlyList<PlayerReadDto>>;

public record PlayerReadDto(
    int Id,
    string Name,
    string Email,
    string? Phone,
    DateTime? DateOfBirth,
    int BranchId,
    int? SubscriptionId,
    DateTime? SubscriptionStartDate,
    DateTime? SubscriptionEndDate,
    bool IsActive,
    DateTime CreatedAt
);

public class GetPlayersQueryHandler : IRequestHandler<GetPlayersQuery, IReadOnlyList<PlayerReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetPlayersQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<PlayerReadDto>> Handle(
        GetPlayersQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue)
            throw new ForbiddenAccessException("A facility-scoped session is required.");

        var query = _dbContext.Set<Player>()
            .AsNoTracking()
            .Where(p => p.FacilityId == _currentUserService.FacilityId.Value);

        if (_currentUserService.BranchId.HasValue)
            query = query.Where(p => p.BranchId == _currentUserService.BranchId.Value);

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new PlayerReadDto(
                p.Id,
                p.Name,
                p.Email,
                p.Phone,
                p.DateOfBirth,
                p.BranchId,
                p.SubscriptionId,
                p.SubscriptionStartDate,
                p.SubscriptionEndDate,
                p.IsActive,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

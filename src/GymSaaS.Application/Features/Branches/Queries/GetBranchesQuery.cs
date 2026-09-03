using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Branches.Queries;

public record GetBranchesQuery : IRequest<IReadOnlyList<BranchReadDto>>;

public record BranchReadDto(
    int Id,
    string Name,
    string? Address,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt
);

public class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetBranchesQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<BranchReadDto>> Handle(
        GetBranchesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.FacilityId.HasValue)
            throw new ForbiddenAccessException("A facility-scoped session is required.");

        var query = _dbContext.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.FacilityId == _currentUserService.FacilityId.Value);

        if (_currentUserService.BranchId.HasValue)
            query = query.Where(b => b.Id == _currentUserService.BranchId.Value);

        return await query
            .OrderBy(b => b.Name)
            .Select(b => new BranchReadDto(
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.IsActive,
                b.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

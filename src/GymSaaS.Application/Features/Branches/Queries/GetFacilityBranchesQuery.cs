using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Entities;
using GymSaaS.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Application.Features.Branches.Queries;

public record GetFacilityBranchesQuery(int FacilityId) : IRequest<IReadOnlyList<BranchReadDto>>;

public class GetFacilityBranchesQueryHandler : IRequestHandler<GetFacilityBranchesQuery, IReadOnlyList<BranchReadDto>>
{
    private readonly DbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetFacilityBranchesQueryHandler(DbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<BranchReadDto>> Handle(
        GetFacilityBranchesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsSupervisor)
            throw new ForbiddenAccessException("Only the supervisor can access another facility.");

        var facilityExists = await _dbContext.Set<Facility>()
            .IgnoreQueryFilters()
            .AnyAsync(f => f.Id == request.FacilityId, cancellationToken);

        if (!facilityExists)
            throw new NotFoundException("Facility", request.FacilityId);

        return await _dbContext.Set<Branch>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.FacilityId == request.FacilityId)
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
